using Hangfire;
using Hangfire.SqlServer;
using Malume.Api.Hubs;
using Malume.Api.Middleware;
using Malume.Api.Security;
using Malume.Api.Services;
using Malume.Api.Startup;
using Malume.Application;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Jobs;
using Malume.Application.Abstractions.Realtime;
using Malume.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddHostedService<DatabaseInitializerHostedService>();

bool hangfireEnabled = builder.Configuration.GetValue<bool?>("Hangfire:Enabled") ?? true;
if (hangfireEnabled)
{
    builder.Services.AddHangfire((sp, cfg) => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"),
            new SqlServerStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));
    builder.Services.AddHangfireServer();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();

builder.Services.AddAuthorization(MalumePolicies.AddMalumePolicies);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<ITripRealtimeNotifier, SignalRTripRealtimeNotifier>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Malume API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [securityScheme] = []
    });
});

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["https://localhost:7002", "http://localhost:5239"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorPortal", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

WebApplication app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("BlazorPortal");
app.UseAuthentication();
app.UseAuthorization();

if (hangfireEnabled)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseHangfireDashboard("/hangfire");
    }

    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IComplianceExpiryScanner>(
        "compliance-expiry-scanner",
        s => s.ScanAsync(CancellationToken.None),
        "0 22 * * *");

    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IEscrowPayoutJob>(
        "escrow-milestone-payout",
        j => j.ProcessDueMilestonesAsync(null, CancellationToken.None),
        "0 4 * * *");
}

app.MapControllers();
app.MapHub<TripTelemetryHub>("/hubs/trips");

app.Run();

public partial class Program;

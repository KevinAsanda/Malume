using Malume.Web.Portal;
using Malume.Web.Portal.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

string? configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"];
string apiBaseUrl = !string.IsNullOrWhiteSpace(configuredApiBaseUrl)
    ? configuredApiBaseUrl
    : builder.HostEnvironment.IsDevelopment()
        ? "https://localhost:7119"
        : builder.HostEnvironment.BaseAddress;

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());

builder.Services.AddHttpClient("MalumeApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("MalumeApi"));
builder.Services.AddScoped<MalumeApiClient>();

await builder.Build().RunAsync();

using Malume.Web.Portal;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

string? configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"];
string apiBaseUrl = !string.IsNullOrWhiteSpace(configuredApiBaseUrl)
    ? configuredApiBaseUrl
    : builder.HostEnvironment.IsDevelopment()
        ? "https://localhost:7119"
        : builder.HostEnvironment.BaseAddress;

builder.Services.AddHttpClient("MalumeApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("MalumeApi"));

await builder.Build().RunAsync();

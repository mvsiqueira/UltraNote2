using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltraNote.Client;
using UltraNote.Web;
using UltraNote.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base URL: configured in wwwroot/appsettings[.Production].json ("ApiBaseUrl"),
// falling back to the app's own origin.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    apiBaseUrl = builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped<GoogleAuthService>();
builder.Services.AddScoped<BearerTokenHandler>();

// API client with the Google bearer token attached to every request.
builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped<IUltraNoteApi>(sp =>
    new UltraNoteApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("api")));

await builder.Build().RunAsync();

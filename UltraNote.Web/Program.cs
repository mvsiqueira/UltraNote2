using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltraNote.Client;
using UltraNote.Web;
using UltraNote.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base URL: derived from the page's own host whenever it matches "note.<domain>"
// (→ "note-api.<domain>"), so the web app always calls the API on the SAME site it was
// loaded from. This matters because the session cookie (see UltraNote.Api's
// GoogleAuth.CookieScheme) is SameSite=Lax — same-site subresource requests (embedded
// <img>, background API calls) carry it fine, but a cross-SITE one (e.g. web on
// ultrasoftinc.com.br calling an API on ultrasoft.app.br) would not, breaking inline
// images/attachments on whichever production domain wasn't visited first. Falls back to
// wwwroot/appsettings[.Production].json's "ApiBaseUrl" for local dev (loopback host) or
// any domain without a matching "note-api." counterpart.
var currentUri = new Uri(builder.HostEnvironment.BaseAddress);
string apiBaseUrl;
if (currentUri.Host.StartsWith("note.", StringComparison.OrdinalIgnoreCase))
{
    apiBaseUrl = $"{currentUri.Scheme}://note-api.{currentUri.Host["note.".Length..]}";
}
else
{
    var configured = builder.Configuration["ApiBaseUrl"];
    apiBaseUrl = string.IsNullOrWhiteSpace(configured) ? builder.HostEnvironment.BaseAddress : configured;
}

// Singleton (not scoped): the IHttpClientFactory builds BearerTokenHandler in its own DI
// scope, so a scoped GoogleAuthService would be a DIFFERENT instance than the UI's — the
// handler would never see the token set at login. Singleton guarantees one shared instance.
builder.Services.AddSingleton<GoogleAuthService>();
builder.Services.AddScoped<BearerTokenHandler>();

// API client with the Google bearer token attached to every request.
builder.Services.AddHttpClient("api", c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped<IUltraNoteApi>(sp =>
    new UltraNoteApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("api")));

await builder.Build().RunAsync();

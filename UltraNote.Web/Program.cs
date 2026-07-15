using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltraNote.Client;
using UltraNote.Web;
using UltraNote.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base URL: derived from the page's own host/path, so the web app always calls the
// API on the SAME origin it was loaded from (matters for the session cookie — see
// UltraNote.Api's GoogleAuth.CookieScheme — which subresource requests like embedded
// <img>/<a> need on-origin to be sent).
//
//   .../ultranote/*  → .../ultranote/api-note/*  (unified scheme: every production domain
//                                                  now serves the app under the same
//                                                  /ultranote/ prefix — see index.html's
//                                                  base-href script and nginx.conf — so the
//                                                  API sits right alongside it, same origin,
//                                                  one relative rule everywhere.)
//
// Everything else falls back to the myQNAPcloud legacy root path (no /ultranote/ prefix —
// only reachable if someone visits the bare domain instead of the documented /ultranote
// URL; harmless to keep) or wwwroot/appsettings[.Production].json's "ApiBaseUrl" for local
// dev. The note.<domain>/note-api.<domain> Cloudflare routes have been retired — see
// DEPLOY-QNAP.md.
var currentUri = new Uri(builder.HostEnvironment.BaseAddress);
string apiBaseUrl;
if (currentUri.AbsolutePath.StartsWith("/ultranote/", StringComparison.OrdinalIgnoreCase))
{
    apiBaseUrl = $"{builder.HostEnvironment.BaseAddress}api-note/";
}
else if (currentUri.Host.EndsWith(".myqnapcloud.com", StringComparison.OrdinalIgnoreCase))
{
    apiBaseUrl = $"{currentUri.GetLeftPart(UriPartial.Authority)}/api-note/";
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

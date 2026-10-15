using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using UltraNote.Api.Auth;

namespace UltraNote.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        // Mints the session cookie from an already-verified Bearer token. The client calls
        // this once at login and again on every silent token refresh. The cookie is both the
        // attachment-access credential and the persistent login (see GET /auth/me).
        api.MapPost("/auth/session", async (HttpContext http, AuthOptions options) =>
        {
            var email = http.User.FindFirst("email")?.Value
                        ?? http.User.FindFirst(ClaimTypes.Email)?.Value;
            if (email is null) return Results.Unauthorized();

            var claims = new List<Claim> { new(ClaimTypes.Email, email) };
            var name = http.User.FindFirst("name")?.Value ?? http.User.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrWhiteSpace(name)) claims.Add(new Claim(ClaimTypes.Name, name));

            var identity = new ClaimsIdentity(claims, GoogleAuth.CookieScheme);
            await http.SignInAsync(GoogleAuth.CookieScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(Math.Max(1, options.SessionDays)),
                });
            return Results.Ok();
        })
        .WithTags("Auth");

        // Who is logged in? Lets the web app restore its session from the cookie alone when
        // the cached Google ID token has expired (they only last ~1h). The allowlist policy
        // applied to the whole /api group has already vetted the caller by the time this runs.
        api.MapGet("/auth/me", (HttpContext http) =>
        {
            var email = http.User.FindFirst("email")?.Value
                        ?? http.User.FindFirst(ClaimTypes.Email)?.Value;
            var name = http.User.FindFirst("name")?.Value
                       ?? http.User.FindFirst(ClaimTypes.Name)?.Value;
            return Results.Ok(new { email, name });
        })
        .WithTags("Auth");

        // Explicit logout: drops the session cookie so the next visit asks for login again.
        api.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(GoogleAuth.CookieScheme);
            return Results.Ok();
        })
        .WithTags("Auth");

        return api;
    }
}

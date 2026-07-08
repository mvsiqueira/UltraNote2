using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using UltraNote.Api.Auth;

namespace UltraNote.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        // Mints the attachment-access cookie from an already-verified Bearer token.
        // The client calls this once at login and again on every silent token refresh.
        api.MapPost("/auth/session", async (HttpContext http) =>
        {
            var email = http.User.FindFirst("email")?.Value
                        ?? http.User.FindFirst(ClaimTypes.Email)?.Value;
            if (email is null) return Results.Unauthorized();

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], GoogleAuth.CookieScheme);
            await http.SignInAsync(GoogleAuth.CookieScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
                });
            return Results.Ok();
        })
        .WithTags("Auth");

        return api;
    }
}

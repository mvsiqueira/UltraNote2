using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace UltraNote.Api.Auth;

/// <summary>Options bound from the "Auth" configuration section.</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When false, the API is open (handy for local dev). Set true in production.</summary>
    public bool Enabled { get; set; }

    /// <summary>OIDC authority that issues the tokens. For "Sign in with Google" this is Google.</summary>
    public string Authority { get; set; } = "https://accounts.google.com";

    /// <summary>The Google OAuth Client ID — used as the expected audience of the ID token.</summary>
    public string GoogleClientId { get; set; } = string.Empty;

    /// <summary>Only these e-mails may use the API.</summary>
    public string[] AllowedEmails { get; set; } = [];

    /// <summary>
    /// How long the session cookie keeps the user logged in (sliding). Google ID tokens only
    /// last ~1h and silent re-issue is unreliable, so this cookie is what makes a login
    /// "stick" across visits. The allowlist is still checked on every request.
    /// </summary>
    public int SessionDays { get; set; } = 30;
}

public static class GoogleAuth
{
    public const string EmailAllowlistPolicy = "EmailAllowlist";

    /// <summary>
    /// Cookie scheme used only so attachment URLs embedded directly in note HTML
    /// (&lt;img src&gt;, &lt;a href&gt;) can authenticate — those requests are plain
    /// browser resource loads that never carry the app's Bearer token. Minted by
    /// POST /api/auth/session once the client already holds a valid Bearer token. It also
    /// doubles as the persistent login: the web app restores its session from it via
    /// GET /api/auth/me when the cached Google ID token has expired.
    /// </summary>
    public const string CookieScheme = "AttachmentCookie";

    public static IServiceCollection AddGoogleAuth(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        services.AddSingleton(options);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = options.Authority;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    // Google ID tokens are issued for the OAuth client id (audience).
                    ValidateAudience = !string.IsNullOrWhiteSpace(options.GoogleClientId),
                    ValidAudience = options.GoogleClientId,
                    // Google issues with both forms depending on the flow.
                    ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                };
            })
            .AddCookie(CookieScheme, cookie =>
            {
                cookie.Cookie.Name = "un_session";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(Math.Max(1, options.SessionDays));
                cookie.SlidingExpiration = true;
                // API only — never redirect to an HTML login page, just fail with the status code.
                cookie.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(auth =>
        {
            auth.AddPolicy(EmailAllowlistPolicy, policy =>
            {
                // Either scheme proves the same identity; the cookie is just minted from
                // an already-verified Bearer token (see POST /api/auth/session).
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, CookieScheme);
                policy.RequireAssertion(ctx =>
                {
                    // Empty allowlist => any authenticated Google account is accepted.
                    if (options.AllowedEmails.Length == 0)
                        return ctx.User.Identity?.IsAuthenticated == true;

                    var email = ctx.User.FindFirst("email")?.Value
                                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    return email is not null &&
                           options.AllowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
                });
            });
        });

        return services;
    }
}

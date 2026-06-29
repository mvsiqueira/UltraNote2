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
}

public static class GoogleAuth
{
    public const string EmailAllowlistPolicy = "EmailAllowlist";

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
            });

        services.AddAuthorization(auth =>
        {
            auth.AddPolicy(EmailAllowlistPolicy, policy =>
                policy.RequireAssertion(ctx =>
                {
                    // Empty allowlist => any authenticated Google account is accepted.
                    if (options.AllowedEmails.Length == 0)
                        return ctx.User.Identity?.IsAuthenticated == true;

                    var email = ctx.User.FindFirst("email")?.Value
                                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    return email is not null &&
                           options.AllowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
                }));
        });

        return services;
    }
}

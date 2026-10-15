using System.Net.Http.Headers;

namespace UltraNote.Web.Auth;

/// <summary>Attaches the Google ID token as a Bearer header on every API request.</summary>
public class BearerTokenHandler(GoogleAuthService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(auth.IdToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.IdToken);
        var response = await base.SendAsync(request, cancellationToken);

        // Cookie-only session (no live Google token) rejected: expired/revoked — back to login.
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            auth.OnSessionRejected();
        return response;
    }
}

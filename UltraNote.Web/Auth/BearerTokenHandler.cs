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
        return await base.SendAsync(request, cancellationToken);
    }
}

using System.Text.Json;
using Microsoft.JSInterop;

namespace UltraNote.Web.Auth;

/// <summary>
/// Drives "Sign in with Google" via Google Identity Services (GIS). The browser obtains a
/// Google ID token; we keep it and attach it as a Bearer to API calls. The API validates it
/// (audience = GoogleClientId) and checks the e-mail allowlist.
/// </summary>
public class GoogleAuthService(IJSRuntime js, IConfiguration config) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<GoogleAuthService>? _selfRef;

    public string ClientId { get; } = config["GoogleClientId"] ?? string.Empty;

    /// <summary>When no client id is configured (dev), auth is bypassed — same idea as the API's Auth:Enabled.</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ClientId);

    public string? IdToken { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public bool IsAuthenticated => !IsEnabled || IdToken is not null;

    public event Action? OnChange;

    /// <summary>Loads GIS and renders the sign-in button into the given element id.</summary>
    public async Task RenderButtonAsync(string buttonElementId)
    {
        if (!IsEnabled) return;
        _module ??= await js.InvokeAsync<IJSObjectReference>(
            "import", "./google-auth.js");
        _selfRef ??= DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("init", _selfRef, ClientId, buttonElementId);
    }

    [JSInvokable]
    public void OnCredential(string idToken)
    {
        IdToken = idToken;
        (Email, Name) = DecodeClaims(idToken);
        OnChange?.Invoke();
    }

    public async Task SignOutAsync()
    {
        IdToken = null;
        Email = null;
        Name = null;
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("signOut"); } catch (JSDisconnectedException) { }
        }
        OnChange?.Invoke();
    }

    private static (string? email, string? name) DecodeClaims(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return (null, null);

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload += (payload.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };

        try
        {
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = doc.RootElement;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            return (email, name);
        }
        catch
        {
            return (null, null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        }
    }
}

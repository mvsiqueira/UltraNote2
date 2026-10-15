using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace UltraNote.Web.Auth;

/// <summary>
/// "Sign in with Google" via Google Identity Services (GIS), with login persistence:
/// the ID token is cached in localStorage (survives refresh) and silently renewed via
/// auto-select before it expires.
/// </summary>
public class GoogleAuthService(IJSRuntime js, IConfiguration config, IHttpClientFactory httpFactory) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<GoogleAuthService>? _selfRef;
    private bool _gisInited;
    private CancellationTokenSource? _refreshCts;

    public string ClientId { get; } = config["GoogleClientId"] ?? string.Empty;

    /// <summary>No client id (dev) => auth bypassed, same as the API's Auth:Enabled=false.</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ClientId);

    public string? IdToken { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }

    /// <summary>
    /// Logged in through the API's session cookie alone (no live Google ID token) — the
    /// persistent-login path used when the cached token has expired.
    /// </summary>
    public bool HasCookieSession { get; private set; }

    public bool IsAuthenticated => !IsEnabled || IdToken is not null || HasCookieSession;

    /// <summary>True until InitAsync has decided whether a login is needed — keeps the login screen from flashing on load.</summary>
    public bool Initializing { get; private set; } = true;

    /// <summary>True when the last sign-in attempt used a Google account not on the API's allowlist.</summary>
    public bool AccessDenied { get; private set; }
    public string? DeniedEmail { get; private set; }

    public event Action? OnChange;

    /// <summary>
    /// Restores the session without UI, trying in order: a still-valid cached Google token,
    /// then the API's session cookie (30-day persistent login). GIS is only loaded after
    /// that, so a slow/blocked Google script never delays or breaks an already-valid session.
    /// </summary>
    public async Task InitAsync()
    {
        if (!IsEnabled) { Initializing = false; return; }
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./google-auth.js");
        _selfRef ??= DotNetObjectReference.Create(this);

        var stored = await _module.InvokeAsync<string?>("getStored");
        if (!string.IsNullOrEmpty(stored)) await UseToken(stored);
        if (IdToken is null) await TryRestoreCookieSessionAsync();

        if (IsAuthenticated)
        {
            Initializing = false;
            OnChange?.Invoke();
        }

        if (!_gisInited)
        {
            try
            {
                await _module.InvokeVoidAsync("init", _selfRef, ClientId, true);
                _gisInited = true;
            }
            catch when (IsAuthenticated)
            {
                // Already logged in via cookie; GIS is only needed for a later silent refresh.
            }
        }

        Initializing = false;
        OnChange?.Invoke();
    }

    // The cached ID token is gone/expired, but the API's session cookie may still be good.
    private async Task TryRestoreCookieSessionAsync()
    {
        try
        {
            var client = httpFactory.CreateClient("api");
            var req = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            req.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode) return;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            Name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            HasCookieSession = true;
        }
        catch
        {
            // Offline / API down: fall through to the normal login screen.
        }
    }

    /// <summary>
    /// An API call came back 401 while we only had the cookie: it expired or was revoked
    /// (or the account left the allowlist). Drop back to the login screen.
    /// </summary>
    public void OnSessionRejected()
    {
        if (!HasCookieSession || IdToken is not null) return;
        HasCookieSession = false;
        Email = null;
        Name = null;
        OnChange?.Invoke();
    }

    /// <summary>Shows the sign-in button and tries a silent auto-select sign-in.</summary>
    public async Task RenderButtonAndSilentAsync(string buttonElementId)
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("renderButton", buttonElementId);
        await _module.InvokeVoidAsync("promptSilent");
    }

    [JSInvokable]
    public Task OnCredential(string idToken) => UseToken(idToken);

    public async Task SignOutAsync()
    {
        _refreshCts?.Cancel();

        // Drop the server-side session cookie too, otherwise the next load would just log
        // straight back in from it. Best effort — local state is cleared regardless.
        try
        {
            var client = httpFactory.CreateClient("api");
            var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
            req.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            await client.SendAsync(req);
        }
        catch { }

        IdToken = null;
        HasCookieSession = false;
        Email = null;
        Name = null;
        AccessDenied = false;
        DeniedEmail = null;
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("signOut"); } catch (JSDisconnectedException) { }
        }
        OnChange?.Invoke();
    }

    private async Task UseToken(string token)
    {
        var (email, name, exp) = Decode(token);
        // Reject already-expired tokens (30s skew); a fresh one will be fetched silently.
        if (exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30)
        {
            if (_module is not null) await _module.InvokeVoidAsync("signOut");
            return;
        }

        // Gate on the API's allowlist before granting UI access — a valid Google token
        // alone isn't enough (the account may not be on Auth:AllowedEmails).
        var result = await EstablishSessionAsync(token);
        if (result == SessionResult.Denied)
        {
            AccessDenied = true;
            DeniedEmail = email;
            IdToken = null;
            Email = null;
            Name = null;
            _refreshCts?.Cancel();
            if (_module is not null) await _module.InvokeVoidAsync("signOut");
            OnChange?.Invoke();
            return;
        }
        // A transient failure (network/CORS) only gets the benefit of the doubt when it's
        // refreshing the SAME account's already-established session — never for a first
        // login or an account switch, otherwise a failed check would silently grant access.
        var refreshingSameAccount = IdToken is not null && string.Equals(Email, email, StringComparison.OrdinalIgnoreCase);
        if (result == SessionResult.Error && !refreshingSameAccount)
        {
            OnChange?.Invoke();
            return;
        }

        AccessDenied = false;
        DeniedEmail = null;
        IdToken = token;
        Email = email;
        Name = name;
        ScheduleRefresh(exp);
        OnChange?.Invoke();
    }

    private enum SessionResult { Ok, Denied, Error }

    // Verifies the token against the API's allowlist and, on success, mints the
    // attachment-access cookie (UltraNote.Api's GoogleAuth.CookieScheme) so <img>/<a>
    // links embedded in note HTML — which never carry the Bearer header — can authenticate.
    private async Task<SessionResult> EstablishSessionAsync(string token)
    {
        try
        {
            var client = httpFactory.CreateClient("api");
            var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/session");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            var res = await client.SendAsync(req);
            if (res.IsSuccessStatusCode) return SessionResult.Ok;
            return res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                ? SessionResult.Denied
                : SessionResult.Error;
        }
        catch
        {
            return SessionResult.Error;
        }
    }

    // Silently re-issue the token shortly before it expires (auto-select One Tap).
    private void ScheduleRefresh(long expSeconds)
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;
        var msUntil = (expSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60) * 1000;
        if (msUntil < 0) msUntil = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay((int)Math.Min(msUntil, int.MaxValue), ct);
                if (!ct.IsCancellationRequested && _module is not null)
                    await _module.InvokeVoidAsync("promptSilent");
            }
            catch (TaskCanceledException) { }
            catch (JSDisconnectedException) { }
        });
    }

    private static (string? email, string? name, long exp) Decode(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return (null, null, 0);

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload += (payload.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };

        try
        {
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = doc.RootElement;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var exp = root.TryGetProperty("exp", out var x) && x.TryGetInt64(out var v) ? v : 0;
            return (email, name, exp);
        }
        catch
        {
            return (null, null, 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _refreshCts?.Cancel();
        _selfRef?.Dispose();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        }
    }
}

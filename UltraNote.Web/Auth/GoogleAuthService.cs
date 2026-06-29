using System.Text.Json;
using Microsoft.JSInterop;

namespace UltraNote.Web.Auth;

/// <summary>
/// "Sign in with Google" via Google Identity Services (GIS), with login persistence:
/// the ID token is cached in localStorage (survives refresh) and silently renewed via
/// auto-select before it expires.
/// </summary>
public class GoogleAuthService(IJSRuntime js, IConfiguration config) : IAsyncDisposable
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
    public bool IsAuthenticated => !IsEnabled || IdToken is not null;

    public event Action? OnChange;

    /// <summary>Loads GIS, wires the callback, and restores a still-valid cached token (no UI).</summary>
    public async Task InitAsync()
    {
        if (!IsEnabled) return;
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./google-auth.js");
        _selfRef ??= DotNetObjectReference.Create(this);
        if (!_gisInited)
        {
            await _module.InvokeVoidAsync("init", _selfRef, ClientId, true);
            _gisInited = true;
        }

        var stored = await _module.InvokeAsync<string?>("getStored");
        if (!string.IsNullOrEmpty(stored)) UseToken(stored);
    }

    /// <summary>Shows the sign-in button and tries a silent auto-select sign-in.</summary>
    public async Task RenderButtonAndSilentAsync(string buttonElementId)
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("renderButton", buttonElementId);
        await _module.InvokeVoidAsync("promptSilent");
    }

    [JSInvokable]
    public void OnCredential(string idToken) => UseToken(idToken);

    public async Task SignOutAsync()
    {
        _refreshCts?.Cancel();
        IdToken = null;
        Email = null;
        Name = null;
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("signOut"); } catch (JSDisconnectedException) { }
        }
        OnChange?.Invoke();
    }

    private void UseToken(string token)
    {
        var (email, name, exp) = Decode(token);
        // Reject already-expired tokens (30s skew); a fresh one will be fetched silently.
        if (exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30)
        {
            if (_module is not null) _ = _module.InvokeVoidAsync("signOut").AsTask();
            return;
        }

        IdToken = token;
        Email = email;
        Name = name;
        ScheduleRefresh(exp);
        OnChange?.Invoke();
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

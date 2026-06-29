// Google Identity Services wrapper with login persistence.
// - The ID token is kept in localStorage so a page refresh doesn't require re-login.
// - auto_select lets GIS silently re-issue a token (no click) when the Google session is
//   still active, covering the ~1h token expiry.

let dotnetRef = null;
const KEY = "ultranote-gid";

export async function init(ref, clientId, autoSelect) {
    dotnetRef = ref;
    await loadGis();
    google.accounts.id.initialize({
        client_id: clientId,
        auto_select: !!autoSelect,
        use_fedcm_for_prompt: true,
        callback: (resp) => {
            try { localStorage.setItem(KEY, resp.credential); } catch (e) { }
            dotnetRef.invokeMethodAsync("OnCredential", resp.credential);
        },
    });
}

export function renderButton(elId) {
    const el = document.getElementById(elId);
    if (el && window.google?.accounts?.id) {
        google.accounts.id.renderButton(el, { theme: "outline", size: "large", text: "signin_with", shape: "pill" });
    }
}

// Attempts a silent (One Tap / auto-select) sign-in. Fires the callback if Google can
// issue a token without user interaction; otherwise it's a no-op / shows One Tap.
export function promptSilent() {
    if (window.google?.accounts?.id) google.accounts.id.prompt();
}

export function getStored() {
    try { return localStorage.getItem(KEY); } catch (e) { return null; }
}

export function signOut() {
    try { localStorage.removeItem(KEY); } catch (e) { }
    if (window.google?.accounts?.id) google.accounts.id.disableAutoSelect();
}

function loadGis() {
    return new Promise((resolve, reject) => {
        if (window.google?.accounts?.id) { resolve(); return; }
        const s = document.createElement("script");
        s.src = "https://accounts.google.com/gsi/client";
        s.async = true;
        s.defer = true;
        s.onload = () => resolve();
        s.onerror = () => reject(new Error("Falha ao carregar o Google Identity Services."));
        document.head.appendChild(s);
    });
}

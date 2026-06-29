// Google Identity Services (GIS) wrapper. Loads the GIS script on demand, initializes it
// with the OAuth client id, and renders the "Sign in with Google" button. On success it
// hands the ID token back to .NET (GoogleAuthService.OnCredential).

let dotnetRef = null;

export async function init(ref, clientId, buttonElementId) {
    dotnetRef = ref;
    await loadGisScript();

    google.accounts.id.initialize({
        client_id: clientId,
        callback: (response) => {
            dotnetRef.invokeMethodAsync("OnCredential", response.credential);
        },
    });

    const el = document.getElementById(buttonElementId);
    if (el) {
        google.accounts.id.renderButton(el, {
            theme: "outline",
            size: "large",
            text: "signin_with",
            shape: "pill",
        });
    }
    // One Tap (prompt) is intentionally not auto-shown — the explicit button is the
    // reliable path and avoids FedCM noise when there is no active Google session.
}

export function signOut() {
    if (window.google?.accounts?.id) {
        google.accounts.id.disableAutoSelect();
    }
}

function loadGisScript() {
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

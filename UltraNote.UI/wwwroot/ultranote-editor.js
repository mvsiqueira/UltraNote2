// First-cut editor backend: a thin wrapper over a contenteditable surface.
// When TipTap is bundled in, only this module + RichTextEditor.razor need to change.

export function attach(el, dotNetRef) {
    if (!el) return;
    el.addEventListener("input", () => {
        dotNetRef.invokeMethodAsync("NotifyChanged");
    });
    // Ctrl+click to open links (matches the original app's behaviour).
    el.addEventListener("click", (e) => {
        if (!e.ctrlKey) return;
        const a = e.target.closest("a");
        if (a && a.href) {
            e.preventDefault();
            window.open(a.href, "_blank", "noopener,noreferrer");
        }
    });
}

export function setHtml(el, html) {
    if (el) el.innerHTML = html || "";
}

export function getHtml(el) {
    return el ? el.innerHTML : "";
}

export function exec(el, command, value) {
    if (!el) return;
    el.focus();
    document.execCommand(command, false, value ?? undefined);
}

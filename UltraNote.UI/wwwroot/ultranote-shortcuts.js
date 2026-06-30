// Global keyboard shortcuts. Ctrl+S (or Cmd+S on Mac) saves the open note instead of
// triggering the browser's "Save page" dialog.
let handler = null;

export function init(dotnetRef) {
    handler = (e) => {
        const isSave = (e.ctrlKey || e.metaKey) && (e.key === "s" || e.key === "S");
        if (!isSave) return;
        e.preventDefault();
        dotnetRef.invokeMethodAsync("OnSaveShortcut");
    };
    window.addEventListener("keydown", handler);
}

export function dispose() {
    if (handler) { window.removeEventListener("keydown", handler); handler = null; }
}

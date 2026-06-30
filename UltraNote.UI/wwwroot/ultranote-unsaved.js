// Warns the browser (native "leave site?" prompt) when closing/refreshing the tab while
// there are unsaved edits. In-app navigation (switching notes) is guarded separately in C#.
let handler = null;

export function setGuard(enabled) {
    if (enabled && !handler) {
        handler = (e) => { e.preventDefault(); e.returnValue = ""; };
        window.addEventListener("beforeunload", handler);
    } else if (!enabled && handler) {
        window.removeEventListener("beforeunload", handler);
        handler = null;
    }
}

// Drag-to-resize the sidebar. Width is applied as --sidebar-width on the
// grid container and persisted in localStorage — pure JS, no Blazor round-trip.
const KEY = "ultranote_sidebar_width";
const MIN = 180;
const MAX = 480;
const DEFAULT_WIDTH = 240;

const instances = new Map();

function clamp(width) {
    return Math.min(MAX, Math.max(MIN, width));
}

export function init(bodyEl, handleEl) {
    const saved = parseInt(localStorage.getItem(KEY) || "", 10);
    const initial = Number.isFinite(saved) ? clamp(saved) : DEFAULT_WIDTH;
    bodyEl.style.setProperty("--sidebar-width", `${initial}px`);

    let dragging = false;
    let startX = 0;
    let startWidth = 0;

    const onMove = (e) => {
        if (!dragging) return;
        const width = clamp(startWidth + (e.clientX - startX));
        bodyEl.style.setProperty("--sidebar-width", `${width}px`);
    };

    const onUp = () => {
        if (!dragging) return;
        dragging = false;
        handleEl.classList.remove("dragging");
        document.body.classList.remove("col-resize-cursor");
        document.removeEventListener("mousemove", onMove);
        document.removeEventListener("mouseup", onUp);
        const width = parseInt(bodyEl.style.getPropertyValue("--sidebar-width"), 10);
        try { localStorage.setItem(KEY, String(width)); } catch (e) { }
    };

    const onDown = (e) => {
        dragging = true;
        startX = e.clientX;
        startWidth = parseInt(bodyEl.style.getPropertyValue("--sidebar-width"), 10) || DEFAULT_WIDTH;
        handleEl.classList.add("dragging");
        document.body.classList.add("col-resize-cursor");
        document.addEventListener("mousemove", onMove);
        document.addEventListener("mouseup", onUp);
        e.preventDefault();
    };

    handleEl.addEventListener("mousedown", onDown);
    instances.set(handleEl, { onDown, onMove, onUp });
}

export function dispose(handleEl) {
    const h = instances.get(handleEl);
    if (!h) return;
    handleEl.removeEventListener("mousedown", h.onDown);
    document.removeEventListener("mousemove", h.onMove);
    document.removeEventListener("mouseup", h.onUp);
    instances.delete(handleEl);
}

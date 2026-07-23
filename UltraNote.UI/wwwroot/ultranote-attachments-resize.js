// Drag-to-resize the attachments panel — same pattern as ultranote-sidebar-resize.js (a CSS
// var on the panel, persisted in localStorage, pure JS, no Blazor round-trip), kept as its
// own module since the axis/CSS var/handle position differ by dock mode (width when docked
// to the side, height when docked at the bottom — see NotesApp.razor's
// ToggleAttachmentsPanelSide) enough that sharing one generic module would need about as
// many parameters as just having two small modules.
const WIDTH_KEY = "ultranote_attachments_width";
const HEIGHT_KEY = "ultranote_attachments_height";
const WIDTH_MIN = 200, WIDTH_MAX = 600, WIDTH_DEFAULT = 280;
const HEIGHT_MIN = 80, HEIGHT_MAX = 480, HEIGHT_DEFAULT = 150;

const instances = new Map();

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

// containerEl must be the shared ancestor of both the panel and the resize handle (the
// .editor-body wrapper, not .attachments-bar itself) — CSS custom properties only inherit
// downward, so setting the var on the panel alone would leave the handle (a sibling, not a
// descendant, of the panel) unable to see it and permanently stuck at the CSS fallback
// value regardless of the actual size.
export function init(containerEl, handleEl, isSide) {
    const cssVar = isSide ? "--attachments-width" : "--attachments-height";
    const key = isSide ? WIDTH_KEY : HEIGHT_KEY;
    const min = isSide ? WIDTH_MIN : HEIGHT_MIN;
    const max = isSide ? WIDTH_MAX : HEIGHT_MAX;
    const defaultSize = isSide ? WIDTH_DEFAULT : HEIGHT_DEFAULT;
    const cursorClass = isSide ? "col-resize-cursor" : "row-resize-cursor";

    const saved = parseInt(localStorage.getItem(key) || "", 10);
    const initial = Number.isFinite(saved) ? clamp(saved, min, max) : defaultSize;
    containerEl.style.setProperty(cssVar, `${initial}px`);

    let dragging = false;
    let startPos = 0;
    let startSize = 0;

    // The handle sits on the panel's leading edge (left edge docked right, top edge docked
    // bottom) — dragging toward the panel's own content should grow it, the opposite sign
    // from raw pointer movement on that edge.
    const onMove = (e) => {
        if (!dragging) return;
        const pos = isSide ? e.clientX : e.clientY;
        const size = clamp(startSize - (pos - startPos), min, max);
        containerEl.style.setProperty(cssVar, `${size}px`);
    };
    const onUp = () => {
        if (!dragging) return;
        dragging = false;
        handleEl.classList.remove("dragging");
        document.body.classList.remove(cursorClass);
        document.removeEventListener("mousemove", onMove);
        document.removeEventListener("mouseup", onUp);
        const size = parseInt(containerEl.style.getPropertyValue(cssVar), 10);
        try { localStorage.setItem(key, String(size)); } catch (e) { }
    };
    const onDown = (e) => {
        dragging = true;
        startPos = isSide ? e.clientX : e.clientY;
        startSize = parseInt(containerEl.style.getPropertyValue(cssVar), 10) || defaultSize;
        handleEl.classList.add("dragging");
        document.body.classList.add(cursorClass);
        document.addEventListener("mousemove", onMove);
        document.addEventListener("mouseup", onUp);
        e.preventDefault();
    };

    handleEl.addEventListener("mousedown", onDown);
    instances.set(handleEl, { onDown, onMove, onUp, cursorClass });
}

export function dispose(handleEl) {
    const h = instances.get(handleEl);
    if (!h) return;
    handleEl.removeEventListener("mousedown", h.onDown);
    document.removeEventListener("mousemove", h.onMove);
    document.removeEventListener("mouseup", h.onUp);
    document.body.classList.remove(h.cursorClass);
    instances.delete(handleEl);
}

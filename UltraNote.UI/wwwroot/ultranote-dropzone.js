// Drop zone for the attachments bar — no TipTap dependency.
const zones = new Map();

function toBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result.substring(reader.result.indexOf(",") + 1));
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

export function init(el, dotNetRef) {
    const onDragOver = (e) => {
        if (e.dataTransfer?.types?.includes("Files")) {
            e.preventDefault();
            e.stopPropagation();
            el.classList.add("drop-target");
        }
    };
    const onDragLeave = () => el.classList.remove("drop-target");
    const onDrop = async (e) => {
        el.classList.remove("drop-target");
        const files = Array.from(e.dataTransfer?.files ?? []);
        if (files.length === 0) return;
        e.preventDefault();
        e.stopPropagation();
        for (const file of files) {
            try {
                const b64 = await toBase64(file);
                await dotNetRef.invokeMethodAsync("UploadFileAsync", b64, file.name, file.type);
            } catch (err) {
                console.error("[UltraNote] Drop upload failed", err);
            }
        }
    };
    el.addEventListener("dragover", onDragOver);
    el.addEventListener("dragleave", onDragLeave);
    el.addEventListener("drop", onDrop);
    zones.set(el, { onDragOver, onDragLeave, onDrop });
}

export function dispose(el) {
    const h = zones.get(el);
    if (!h) return;
    el.removeEventListener("dragover", h.onDragOver);
    el.removeEventListener("dragleave", h.onDragLeave);
    el.removeEventListener("drop", h.onDrop);
    el.classList.remove("drop-target");
    zones.delete(el);
}

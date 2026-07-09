// Triggers a browser download of a byte array built client-side (export/backup zips),
// without needing a server round-trip for the file itself.
export function downloadBytes(bytes, fileName, contentType) {
    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

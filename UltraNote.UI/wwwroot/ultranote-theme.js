// Theme persistence. data-theme on <html> drives the CSS variables (Aurora vs Midnight).
const KEY = "ultranote-theme";

export function getTheme() {
    return document.documentElement.getAttribute("data-theme")
        || localStorage.getItem(KEY)
        || ((window.matchMedia && matchMedia("(prefers-color-scheme: dark)").matches) ? "dark" : "light");
}

export function setTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    try { localStorage.setItem(KEY, theme); } catch (e) { }
    return theme;
}

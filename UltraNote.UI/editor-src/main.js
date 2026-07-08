// TipTap rich-text editor, ported from the original Tauri/React UltraNotes app to vanilla
// JS so it can be driven from Blazor via JSInterop. Build with `npm run build` (esbuild) —
// the output (../wwwroot/ultranote-editor.js) is committed because the QNAP deploy
// environment does not run npm; only `dotnet publish` runs there.

import { Editor, Extension } from "@tiptap/core";
import StarterKit from "@tiptap/starter-kit";
import Image from "@tiptap/extension-image";
import { NodeSelection } from "@tiptap/pm/state";
import TextStyle from "@tiptap/extension-text-style";
import Color from "@tiptap/extension-color";
import Highlight from "@tiptap/extension-highlight";
import Link from "@tiptap/extension-link";
import Placeholder from "@tiptap/extension-placeholder";
import Table from "@tiptap/extension-table";
import TableRow from "@tiptap/extension-table-row";
import TableHeader from "@tiptap/extension-table-header";
import TableCell from "@tiptap/extension-table-cell";

// Image extension extended to persist width attribute
const ResizableImage = Image.extend({
    addAttributes() {
        return {
            ...this.parent?.(),
            width: {
                default: null,
                parseHTML: (el) => el.getAttribute("width") ?? null,
                renderHTML: (attrs) => {
                    if (!attrs.width) return {};
                    const w = Number(attrs.width);
                    return { width: w, style: `width: ${w}px` };
                },
            },
        };
    },
});

// Global resize handle (shared across all editor instances)
let imgResizeHandle = null;
let currentResizeImg = null;
let currentResizeEditor = null;

function findImagePos(doc, src) {
    let result = null;
    doc.descendants((node, pos) => {
        if (result !== null) return false;
        if (node.type.name === "image" && node.attrs.src === src) { result = pos; return false; }
    });
    return result;
}

function positionResizeHandle(img) {
    const handle = getOrCreateResizeHandle();
    const rect = img.getBoundingClientRect();
    handle.style.left = `${rect.right - 6}px`;
    handle.style.top = `${rect.bottom - 6}px`;
    handle.style.display = "block";
}

function hideResizeHandle() {
    if (imgResizeHandle) imgResizeHandle.style.display = "none";
    currentResizeImg = null;
    currentResizeEditor = null;
}

function getOrCreateResizeHandle() {
    if (imgResizeHandle) return imgResizeHandle;
    imgResizeHandle = document.createElement("div");
    imgResizeHandle.className = "img-resize-handle";
    imgResizeHandle.addEventListener("mousedown", (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (!currentResizeImg || !currentResizeEditor) return;
        const img = currentResizeImg;
        const ed = currentResizeEditor;
        const startX = e.clientX;
        const startWidth = img.getBoundingClientRect().width;
        const imgSrc = img.src;
        const onMove = (me) => {
            const w = Math.max(40, startWidth + me.clientX - startX);
            img.style.width = `${w}px`;
            positionResizeHandle(img);
        };
        const onUp = (ue) => {
            document.removeEventListener("mousemove", onMove);
            document.removeEventListener("mouseup", onUp);
            const w = Math.max(40, startWidth + ue.clientX - startX);
            try {
                const pos = findImagePos(ed.state.doc, imgSrc);
                if (pos !== null)
                    ed.chain().setNodeSelection(pos).updateAttributes("image", { width: Math.round(w) }).focus().run();
            } catch {}
            positionResizeHandle(img);
        };
        document.addEventListener("mousemove", onMove);
        document.addEventListener("mouseup", onUp);
    });
    document.body.appendChild(imgResizeHandle);
    return imgResizeHandle;
}

const maxIndentLevel = 6;

const IndentExtension = Extension.create({
    name: "indent",
    addGlobalAttributes() {
        return [
            {
                types: ["paragraph", "listItem"],
                attributes: {
                    indent: {
                        default: 0,
                        parseHTML: (el) => Number(el.getAttribute("data-indent")) || 0,
                        renderHTML: (attrs) => {
                            const indent = Number(attrs.indent) || 0;
                            return indent > 0 ? { "data-indent": String(indent) } : {};
                        },
                    },
                },
            },
        ];
    },
});

// Table parts need a passthrough "style" attribute so per-cell/row width+height (set by the
// resize interactions below) round-trips through getHTML()/setContent().
function withStyleAttribute(Base) {
    return Base.extend({
        addAttributes() {
            return {
                ...this.parent?.(),
                style: {
                    default: null,
                    parseHTML: (el) => el.getAttribute("style"),
                    renderHTML: (attrs) => (attrs.style ? { style: attrs.style } : {}),
                },
            };
        },
    });
}

const StyledTableRow = withStyleAttribute(TableRow);
const StyledTableHeader = withStyleAttribute(TableHeader);
const StyledTableCell = withStyleAttribute(TableCell);

const registry = new WeakMap();
let guideEl = null;

function ensureGuide() {
    if (!guideEl) {
        guideEl = document.createElement("div");
        guideEl.className = "row-resize-guide";
        guideEl.style.display = "none";
        document.body.appendChild(guideEl);
    }
    return guideEl;
}

function showGuide(rect, offsetY) {
    const g = ensureGuide();
    g.style.left = `${rect.left}px`;
    g.style.top = `${rect.bottom + offsetY - 1}px`;
    g.style.width = `${rect.width}px`;
    g.style.display = "block";
}

function hideGuide() {
    if (guideEl) guideEl.style.display = "none";
}

function isNearRowBottom(row, clientY) {
    const rect = row.getBoundingClientRect();
    return rect.bottom - clientY <= 8 && rect.bottom - clientY >= -5;
}

function findResizeRowAtPoint(root, clientX, clientY) {
    const rows = Array.from(root.querySelectorAll("tr"));
    return (
        rows.find((row) => {
            const rect = row.getBoundingClientRect();
            return (
                clientX >= rect.left &&
                clientX <= rect.right &&
                clientY >= rect.top &&
                clientY <= rect.bottom + 5 &&
                isNearRowBottom(row, clientY)
            );
        }) ?? null
    );
}

function findRowPosition(view, row) {
    try {
        // posAtDOM(row, 0) lands just inside the row (at the first cell's boundary), not
        // at the row node itself — walk up to the tableRow ancestor so setNodeMarkup
        // targets the <tr>, not whichever cell happens to be first.
        const $pos = view.state.doc.resolve(view.posAtDOM(row, 0));
        for (let depth = $pos.depth; depth > 0; depth -= 1) {
            if ($pos.node(depth).type.name === "tableRow") return $pos.before(depth);
        }
        return null;
    } catch {
        return null;
    }
}

function setTableRowHeight(view, rowPosition, height) {
    const row = view.state.doc.nodeAt(rowPosition);
    if (!row) return;
    view.dispatch(
        view.state.tr.setNodeMarkup(rowPosition, undefined, { ...row.attrs, style: `height: ${height}px;` }),
    );
}

function setRowResizeActive(row, active) {
    row.classList.toggle("row-resize-active", active);
}

function setRowResizeHover(row, active) {
    row.classList.toggle("row-resize-hover", active);
}

function getIndentTarget(editor) {
    const { selection } = editor.state;
    const from = selection.$from;
    let paragraphTarget = null;
    for (let depth = from.depth; depth > 0; depth -= 1) {
        const node = from.node(depth);
        if (node.type.name === "listItem") {
            return { nodeName: "listItem", indent: Number(node.attrs.indent) || 0 };
        }
        if (node.type.name === "paragraph") {
            paragraphTarget = { nodeName: "paragraph", indent: Number(node.attrs.indent) || 0 };
        }
    }
    return paragraphTarget;
}

function adjustIndent(editor, direction) {
    const target = getIndentTarget(editor);
    if (!target) return;
    const next = Math.max(0, Math.min(maxIndentLevel, target.indent + direction));
    editor.chain().focus().updateAttributes(target.nodeName, { indent: next }).run();
}

const COMMANDS = {
    bold: (e) => e.chain().focus().toggleBold().run(),
    italic: (e) => e.chain().focus().toggleItalic().run(),
    strike: (e) => e.chain().focus().toggleStrike().run(),
    bulletList: (e) => e.chain().focus().toggleBulletList().run(),
    orderedList: (e) => e.chain().focus().toggleOrderedList().run(),
    color: (e, arg) => e.chain().focus().setColor(arg).run(),
    highlight: (e, arg) => e.chain().focus().toggleHighlight({ color: arg }).run(),
    link: (e, arg) => (arg ? e.chain().focus().setLink({ href: arg }).run() : e.chain().focus().unsetLink().run()),
    indent: (e) => adjustIndent(e, 1),
    outdent: (e) => adjustIndent(e, -1),
    insertImage: (e, arg) => e.chain().focus().setImage({ src: arg }).run(),
    horizontalRule: (e) => e.chain().focus().setHorizontalRule().run(),
    clearFormatting: (e) => {
        const { from, to } = e.state.selection;
        const start = from;
        const end   = to;
        return e.chain().focus().command(({ tr, state }) => {
            const { schema, doc } = state;
            // Remove all inline marks (bold, color, highlight, etc.)
            Object.values(schema.marks).forEach(type => tr.removeMark(start, end, type));
            // Convert block nodes that aren't plain paragraphs back to paragraphs
            const para = schema.nodes.paragraph;
            const skip = new Set(['paragraph','bulletList','orderedList','listItem','tableRow','tableCell','tableHeader','table']);
            doc.nodesBetween(start, end, (node, pos) => {
                if (node.isBlock && !node.isLeaf && !skip.has(node.type.name))
                    tr.setNodeMarkup(pos, para);
            });
            return true;
        }).run();
    },
    deleteImage: (e) => {
        const { selection } = e.state;
        if (selection.node?.type.name === "image") e.chain().focus().deleteSelection().run();
    },
    insertTable: (e) => e.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(),
    addColumnBefore: (e) => e.chain().focus().addColumnBefore().run(),
    addColumnAfter: (e) => e.chain().focus().addColumnAfter().run(),
    deleteColumn: (e) => e.chain().focus().deleteColumn().run(),
    addRowBefore: (e) => e.chain().focus().addRowBefore().run(),
    addRowAfter: (e) => e.chain().focus().addRowAfter().run(),
    deleteRow: (e) => e.chain().focus().deleteRow().run(),
    mergeCells: (e) => e.chain().focus().mergeCells().run(),
    splitCell: (e) => e.chain().focus().splitCell().run(),
    deleteTable: (e) => e.chain().focus().deleteTable().run(),
};

export function attach(el, dotNetRef) {
    let dragCleanup = null;
    let hoveredRow = null;

    const editor = new Editor({
        element: el,
        extensions: [
            StarterKit,
            TextStyle,
            Color,
            Highlight.configure({ multicolor: true }),
            IndentExtension,
            Link.configure({ openOnClick: false }),
            Placeholder.configure({ placeholder: "Escreva sua nota..." }),
            ResizableImage.configure({ inline: false, HTMLAttributes: { class: "rte-image" } }),
            Table.configure({ resizable: true, cellMinWidth: 36, lastColumnResizable: true }),
            StyledTableRow,
            StyledTableHeader,
            StyledTableCell,
        ],
        content: "",
        editorProps: {
            attributes: {
                class: "rte-prosemirror",
                spellcheck: "false",
                autocorrect: "off",
                autocomplete: "off",
                "data-gramm": "false",
            },
            handleClick(view, _pos, event) {
                if (!event.ctrlKey) return false;
                const link = event.target?.closest?.("a");
                const href = link?.getAttribute("href");
                if (!href) return false;
                event.preventDefault();
                window.open(href, "_blank", "noopener,noreferrer");
                view.dom.blur();
                return true;
            },
            handleDOMEvents: {
                mousedown(view, event) {
                    const row = findResizeRowAtPoint(view.dom, event.clientX, event.clientY);
                    if (!row || !isNearRowBottom(row, event.clientY)) return false;
                    const rowPosition = findRowPosition(view, row);
                    if (rowPosition === null) return false;

                    event.preventDefault();
                    const startY = event.clientY;
                    const startRect = row.getBoundingClientRect();
                    const startHeight = Math.max(22, Math.round(startRect.height));

                    const onMove = (moveEvent) => {
                        const height = Math.max(22, startHeight + moveEvent.clientY - startY);
                        row.style.height = `${height}px`;
                        row.querySelectorAll("td, th").forEach((cell) => { cell.style.height = `${height}px`; });
                        setRowResizeActive(row, true);
                        showGuide(startRect, height - startHeight);
                        setTableRowHeight(view, rowPosition, height);
                    };
                    const onUp = (upEvent) => {
                        const height = Math.max(22, startHeight + upEvent.clientY - startY);
                        document.body.classList.remove("row-resize-cursor");
                        setRowResizeActive(row, false);
                        hideGuide();
                        document.removeEventListener("mousemove", onMove);
                        document.removeEventListener("mouseup", onUp);
                        dragCleanup = null;
                        setTableRowHeight(view, rowPosition, height);
                    };

                    document.body.classList.add("row-resize-cursor");
                    setRowResizeActive(row, true);
                    showGuide(startRect, 0);
                    document.addEventListener("mousemove", onMove);
                    document.addEventListener("mouseup", onUp);
                    dragCleanup = () => {
                        document.removeEventListener("mousemove", onMove);
                        document.removeEventListener("mouseup", onUp);
                        document.body.classList.remove("row-resize-cursor");
                        setRowResizeActive(row, false);
                        hideGuide();
                    };
                    return true;
                },
                mousemove(view, event) {
                    const row = findResizeRowAtPoint(view.dom, event.clientX, event.clientY);
                    if (!dragCleanup) {
                        const isTarget = Boolean(row && isNearRowBottom(row, event.clientY));
                        if (hoveredRow && hoveredRow !== row) setRowResizeHover(hoveredRow, false);
                        hoveredRow = isTarget ? row : null;
                        document.body.classList.toggle("row-resize-cursor", isTarget);
                        if (row) setRowResizeHover(row, isTarget);
                    }
                    return false;
                },
                mouseleave() {
                    if (!dragCleanup) {
                        document.body.classList.remove("row-resize-cursor");
                        if (hoveredRow) setRowResizeHover(hoveredRow, false);
                        hoveredRow = null;
                    }
                    return false;
                },
                contextmenu(view, event) {
                    const target = event.target;
                    const cell = target?.closest?.("td, th");
                    const row = target?.closest?.("tr");
                    const inTable = Boolean(target?.closest?.("td, th, table"));
                    const img = target?.closest?.("img");
                    const inImage = Boolean(img);
                    event.preventDefault();

                    if (inImage) {
                        hideResizeHandle();
                        try {
                            const pos = view.posAtDOM(img, 0);
                            view.dispatch(view.state.tr.setSelection(NodeSelection.create(view.state.doc, pos)));
                        } catch {}
                    }

                    const menuWidth = inImage ? 192 : inTable ? 210 : 168;
                    const menuHeight = inImage ? 84 : inTable ? 430 : 170;
                    const margin = 8;
                    const x = Math.max(margin, Math.min(event.clientX, window.innerWidth - menuWidth - margin));
                    const y = Math.max(margin, Math.min(event.clientY, window.innerHeight - menuHeight - margin));

                    dotNetRef.invokeMethodAsync(
                        "OnEditorContextMenu",
                        x, y, inTable,
                        Math.round(cell?.getBoundingClientRect().width ?? 120),
                        Math.round(row?.getBoundingClientRect().height ?? 32),
                        cell?.tagName?.toLowerCase() === "th",
                        inImage,
                        img?.src ?? "",
                    );
                    return true;
                },
            },
        },
        onUpdate() {
            dotNetRef.invokeMethodAsync("NotifyChanged");
        },
    });

    const proseMirror = editor.view.dom;
    const setLinkCursors = (cursor) => proseMirror.querySelectorAll("a").forEach(a => { a.style.cursor = cursor; });
    const onKeyDown = (e) => { if (e.key === "Control") setLinkCursors("pointer"); };
    const onKeyUp = (e) => { if (e.key === "Control") setLinkCursors(""); };
    const onBlur = () => setLinkCursors("");
    document.addEventListener("keydown", onKeyDown);
    document.addEventListener("keyup", onKeyUp);
    window.addEventListener("blur", onBlur);

    const onImgClick = (e) => {
        const img = e.target?.closest?.("img");
        if (img) { currentResizeImg = img; currentResizeEditor = editor; positionResizeHandle(img); }
        else hideResizeHandle();
    };
    const onScroll = () => hideResizeHandle();
    proseMirror.addEventListener("click", onImgClick);
    proseMirror.addEventListener("scroll", onScroll);

    const onPaste = async (e) => {
        const items = Array.from(e.clipboardData?.items ?? []).filter(i => i.type.startsWith("image/"));
        if (items.length === 0) return;
        e.preventDefault();
        e.stopPropagation();
        for (const item of items) {
            const file = item.getAsFile();
            if (file) await uploadAndInsert(file, editor, dotNetRef);
        }
    };
    const onDrop = async (e) => {
        const all = Array.from(e.dataTransfer?.files ?? []);
        if (all.length === 0) return;
        e.preventDefault();
        e.stopPropagation();
        // Tell ProseMirror to dismiss its drop-cursor indicator (our stopPropagation
        // prevents it from seeing the real drop event, so the cursor would otherwise stay).
        proseMirror.dispatchEvent(new DragEvent('dragleave', { bubbles: true }));
        const images = all.filter(f => f.type.startsWith("image/"));
        const others = all.filter(f => !f.type.startsWith("image/"));
        for (const file of images) await uploadAndInsert(file, editor, dotNetRef);
        for (const file of others) await uploadFileOnly(file, dotNetRef);
    };
    // Use capture on the surface element so our handler fires before ProseMirror's bubble
    // handler on proseMirror. This prevents stopImmediatePropagation from blocking us.
    const onDragOver = (e) => {
        if (e.dataTransfer?.types?.includes("Files")) e.preventDefault();
    };
    proseMirror.addEventListener("paste", onPaste);
    el.addEventListener("dragover", onDragOver, { capture: true });
    el.addEventListener("drop", onDrop, { capture: true });

    registry.set(el, {
        editor,
        dispose: () => {
            if (dragCleanup) dragCleanup();
            document.removeEventListener("keydown", onKeyDown);
            document.removeEventListener("keyup", onKeyUp);
            window.removeEventListener("blur", onBlur);
            proseMirror.removeEventListener("paste", onPaste);
            el.removeEventListener("dragover", onDragOver, { capture: true });
            el.removeEventListener("drop", onDrop, { capture: true });
            proseMirror.removeEventListener("click", onImgClick);
            proseMirror.removeEventListener("scroll", onScroll);
            if (currentResizeEditor === editor) hideResizeHandle();
        },
    });
}

function get(el) {
    return registry.get(el)?.editor ?? null;
}

export function setHtml(el, html) {
    const editor = get(el);
    if (editor) editor.commands.setContent(html || "", false);
}

export function getHtml(el) {
    const editor = get(el);
    return editor ? editor.getHTML() : "";
}

export function exec(el, command, arg) {
    const editor = get(el);
    if (!editor) return;
    const fn = COMMANDS[command];
    if (fn) fn(editor, arg);
}

export function resizeCurrentCell(el, width, height, isHeader) {
    const editor = get(el);
    if (!editor) return;
    const target = isHeader ? "tableHeader" : "tableCell";
    const safeWidth = Math.max(40, width);
    const safeHeight = Math.max(22, height);
    editor
        .chain()
        .focus()
        .updateAttributes(target, { style: `width: ${safeWidth}px; min-width: ${safeWidth}px; height: ${safeHeight}px;` })
        .updateAttributes("tableRow", { style: `height: ${safeHeight}px;` })
        .run();
}

function fileToBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result.substring(reader.result.indexOf(",") + 1));
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

async function uploadFileOnly(file, dotNetRef) {
    try {
        const base64 = await fileToBase64(file);
        await dotNetRef.invokeMethodAsync("UploadFileAsync", base64, file.name, file.type);
    } catch (err) {
        console.error("[UltraNote] File upload failed", err);
    }
}

async function uploadAndInsert(file, editor, dotNetRef) {
    try {
        const base64 = await fileToBase64(file);
        const url = await dotNetRef.invokeMethodAsync("UploadImageAsync", base64, file.name, file.type);
        if (url) editor.chain().focus().setImage({ src: url }).run();
    } catch (err) {
        console.error("[UltraNote] Image upload failed", err);
    }
}

export function insertImageFromFile(el, dotNetRef) {
    const editor = get(el);
    if (!editor) return;
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "image/*";
    input.onchange = async () => {
        const file = input.files?.[0];
        if (file) await uploadAndInsert(file, editor, dotNetRef);
    };
    input.click();
}

export function dispose(el) {
    const entry = registry.get(el);
    if (!entry) return;
    entry.dispose();
    entry.editor.destroy();
    registry.delete(el);
}

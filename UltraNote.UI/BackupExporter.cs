using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UltraNote.Client;
using UltraNote.Core.Dtos;

namespace UltraNote.UI;

/// <summary>
/// Exports a folder subtree (or the whole library, when <c>rootFolderId</c> is null) as a
/// .zip of .enex files — one per folder, at a path mirroring the folder tree, so BackupImporter
/// can rebuild the same structure. Each individual .enex is a valid Evernote export file
/// (round-trips through EnexImporter/ConvertEnml the same way a real Evernote export does),
/// with note attachments (both inline images and linked files) embedded as resources.
/// </summary>
public static class BackupExporter
{
    // <img src="...api/attachments/{guid}..."> or <a href="...api/attachments/{guid}...">text</a>
    private static readonly Regex AttachmentRefRegex = new(
        @"<(img|a)\b[^>]*?\b(?:src|href)=""[^""]*?/api/attachments/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?:\?[^""]*)?""[^>]*?(?:/>|>.*?</a>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static async Task<byte[]> ExportAsync(
        IUltraNoteApi api,
        IReadOnlyList<FolderDto> allFolders,
        Guid? rootFolderId,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var byParent = allFolders.ToLookup(f => f.ParentId);
        var roots = rootFolderId is { } rid
            ? allFolders.Where(f => f.Id == rid)
            : byParent[null];

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var root in roots)
                await ExportFolderRecursive(api, root, "", byParent, zip, progress, ct);
        }
        return ms.ToArray();
    }

    private static async Task ExportFolderRecursive(
        IUltraNoteApi api, FolderDto folder, string parentZipPath,
        ILookup<Guid?, FolderDto> byParent, ZipArchive zip,
        IProgress<string>? progress, CancellationToken ct)
    {
        var zipDir = string.IsNullOrEmpty(parentZipPath) ? folder.Name : $"{parentZipPath}/{folder.Name}";
        progress?.Report(folder.Name);

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        sb.Append("<en-export export-date=\"").Append(DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"))
          .Append("\" application=\"UltraNote\" version=\"1.0\">\n");

        var summaries = await api.GetNotesAsync(folder.Id, ct);
        foreach (var summary in summaries)
        {
            var note = await api.GetNoteAsync(summary.Id, ct);
            if (note is not null) await AppendNoteAsync(api, note, sb, ct);
        }

        sb.Append("</en-export>\n");

        var entry = zip.CreateEntry($"{zipDir}/_notes.enex", CompressionLevel.Optimal);
        await using (var entryStream = entry.Open())
        await using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
            await writer.WriteAsync(sb.ToString());

        foreach (var child in byParent[folder.Id])
            await ExportFolderRecursive(api, child, zipDir, byParent, zip, progress, ct);
    }

    private static async Task AppendNoteAsync(IUltraNoteApi api, NoteDto note, StringBuilder sb, CancellationToken ct)
    {
        var attachments = await api.GetAttachmentsAsync(note.Id, ct);
        var hashById = new Dictionary<Guid, string>();
        var bytesById = new Dictionary<Guid, byte[]>();
        foreach (var att in attachments)
        {
            var bytes = await api.DownloadAttachmentBytesAsync(att.Id, ct);
            bytesById[att.Id] = bytes;
            hashById[att.Id] = EnexImporter.Md5Hex(bytes);
        }

        var html = note.ContentHtml ?? "";
        var transformed = AttachmentRefRegex.Replace(html, m =>
        {
            if (!Guid.TryParse(m.Groups[2].Value, out var attId) ||
                !hashById.TryGetValue(attId, out var hash))
                return m.Value;
            var mime = attachments.First(a => a.Id == attId).ContentType;
            return $"<en-media hash=\"{hash}\" type=\"{XmlEscape(mime)}\"/>";
        });
        // CDATA can't contain a literal "]]>" — split it if the content ever has one.
        transformed = transformed.Replace("]]>", "]]]]><![CDATA[>");

        sb.Append("<note>\n");
        sb.Append("<title>").Append(XmlEscape(note.Title)).Append("</title>\n");
        sb.Append("<content><![CDATA[<?xml version=\"1.0\" encoding=\"UTF-8\"?><en-note>")
          .Append(transformed).Append("</en-note>]]></content>\n");
        sb.Append("<created>").Append(note.CreatedAt.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'")).Append("</created>\n");
        sb.Append("<updated>").Append(note.UpdatedAt.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'")).Append("</updated>\n");

        foreach (var att in attachments)
        {
            sb.Append("<resource>\n<data encoding=\"base64\">\n")
              .Append(Convert.ToBase64String(bytesById[att.Id]))
              .Append("\n</data>\n<mime>").Append(XmlEscape(att.ContentType)).Append("</mime>\n")
              .Append("<resource-attributes>\n<file-name>").Append(XmlEscape(att.FileName))
              .Append("</file-name>\n</resource-attributes>\n</resource>\n");
        }

        sb.Append("</note>\n");
    }

    private static string XmlEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

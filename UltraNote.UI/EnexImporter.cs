using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace UltraNote.UI;

public record EnexNote(string Title, string RawEnml, DateTime CreatedAt, List<EnexResource> Resources);
public record EnexResource(string Hash, string Mime, string FileName, byte[] Data);

public static class EnexImporter
{
    public static List<EnexNote> Parse(Stream stream)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
        using var reader = XmlReader.Create(stream, settings);
        var doc = XDocument.Load(reader);
        var notes = new List<EnexNote>();

        foreach (var noteEl in doc.Root?.Elements("note") ?? [])
        {
            var title = noteEl.Element("title")?.Value.Trim() ?? "Sem título";
            var created = ParseDate(noteEl.Element("created")?.Value);
            var enml = noteEl.Element("content")?.Value ?? "";
            var resources = ParseResources(noteEl);
            notes.Add(new EnexNote(title, enml, created, resources));
        }
        return notes;
    }

    private static List<EnexResource> ParseResources(XElement noteEl)
    {
        var list = new List<EnexResource>();
        foreach (var r in noteEl.Elements("resource"))
        {
            var raw = r.Element("data")?.Value ?? "";
            var b64 = Regex.Replace(raw, @"\s+", "");
            byte[] data;
            try { data = Convert.FromBase64String(b64); }
            catch { continue; }

            var mime = r.Element("mime")?.Value ?? "application/octet-stream";
            var name = r.Element("resource-attributes")?.Element("file-name")?.Value
                     ?? $"file.{MimeExt(mime)}";
#pragma warning disable CA1416
            var hash = Convert.ToHexString(MD5.HashData(data)).ToLower();
#pragma warning restore CA1416
            list.Add(new EnexResource(hash, mime, name, data));
        }
        return list;
    }

    // Called after resources are uploaded; hashToUrl maps MD5 hex -> attachment URL.
    public static string ConvertEnml(string enml, Dictionary<string, string> hashToUrl)
    {
        var start = enml.IndexOf("<en-note", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "<p></p>";
        var tagEnd = enml.IndexOf('>', start);
        if (tagEnd < 0) return "<p></p>";
        var end = enml.LastIndexOf("</en-note>", StringComparison.OrdinalIgnoreCase);
        var inner = end > tagEnd ? enml[(tagEnd + 1)..end] : enml[(tagEnd + 1)..];

        // <en-media hash="..." type="..."/> → <img> for images, <a> for files
        inner = Regex.Replace(inner, @"<en-media\b([^>]*?)/>",
            m =>
            {
                var attrs = m.Groups[1].Value;
                var hash = Regex.Match(attrs, @"hash=""([^""]+)""").Groups[1].Value;
                var type = Regex.Match(attrs, @"type=""([^""]+)""").Groups[1].Value;
                if (!hashToUrl.TryGetValue(hash, out var url)) return "";
                return type.StartsWith("image/")
                    ? $"<img src=\"{url}\" style=\"max-width:100%;\" />"
                    : $"<a href=\"{url}\">[anexo]</a>";
            },
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // <en-todo checked="true"/> → checked checkbox
        inner = Regex.Replace(inner, @"<en-todo\s+checked=""true""\s*/>",
            "<input type=\"checkbox\" checked disabled />", RegexOptions.IgnoreCase);
        // remaining <en-todo/> → unchecked
        inner = Regex.Replace(inner, @"<en-todo\b[^>]*/>",
            "<input type=\"checkbox\" disabled />", RegexOptions.IgnoreCase);

        return string.IsNullOrWhiteSpace(inner) ? "<p></p>" : inner;
    }

    private static DateTime ParseDate(string? s)
    {
        if (s is not null &&
            DateTime.TryParseExact(s, "yyyyMMdd'T'HHmmss'Z'", null,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
            return dt.ToUniversalTime();
        return DateTime.UtcNow;
    }

    private static string MimeExt(string mime) => mime switch
    {
        "image/jpeg" or "image/jpg" => "jpg",
        "image/png"  => "png",
        "image/gif"  => "gif",
        "image/webp" => "webp",
        "application/pdf" => "pdf",
        "text/plain" => "txt",
        "audio/mpeg" => "mp3",
        "audio/wav" or "audio/x-wav" => "wav",
        _ => "bin",
    };
}

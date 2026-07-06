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
            var hash = Md5Hex(data);
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

    // Pure managed MD5 (RFC 1321) — System.Security.Cryptography.MD5 is unavailable in WASM
    private static readonly int[] Md5S = {
        7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,
        5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,
        4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,
        6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,
    };
    private static readonly uint[] Md5K = {
        0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee,
        0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
        0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be,
        0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
        0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa,
        0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
        0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed,
        0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
        0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c,
        0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
        0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05,
        0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
        0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039,
        0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
        0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1,
        0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391,
    };

    private static string Md5Hex(byte[] data)
    {
        long L = data.Length;
        int zeros = (int)((56 - (L + 1) % 64 + 64) % 64);
        var msg = new byte[L + 1 + zeros + 8];
        Array.Copy(data, msg, L);
        msg[L] = 0x80;
        ulong bits = (ulong)(L * 8);
        for (int i = 0; i < 8; i++) msg[msg.Length - 8 + i] = (byte)(bits >> (8 * i));

        uint a0 = 0x67452301u, b0 = 0xefcdab89u, c0 = 0x98badcfeu, d0 = 0x10325476u;
        var M = new uint[16];

        for (int chunk = 0; chunk < msg.Length / 64; chunk++)
        {
            int off = chunk * 64;
            for (int j = 0; j < 16; j++)
                M[j] = (uint)(msg[off + j*4] | msg[off + j*4+1] << 8 |
                              msg[off + j*4+2] << 16 | msg[off + j*4+3] << 24);

            uint A = a0, B = b0, C = c0, D = d0;
            for (int i = 0; i < 64; i++)
            {
                uint F; uint g;
                if      (i < 16) { F = (B & C) | (~B & D); g = (uint)i; }
                else if (i < 32) { F = (D & B) | (~D & C); g = (5u * (uint)i + 1) % 16; }
                else if (i < 48) { F = B ^ C ^ D;           g = (3u * (uint)i + 5) % 16; }
                else              { F = C ^ (B | ~D);        g = (7u * (uint)i) % 16; }
                F += A + Md5K[i] + M[g];
                A = D; D = C; C = B;
                B += (F << Md5S[i]) | (F >> (32 - Md5S[i]));
            }
            a0 += A; b0 += B; c0 += C; d0 += D;
        }

        var h = new byte[16];
        for (int i = 0; i < 4; i++) { h[i]    = (byte)(a0 >> (8*i)); h[4+i]  = (byte)(b0 >> (8*i));
                                       h[8+i]   = (byte)(c0 >> (8*i)); h[12+i] = (byte)(d0 >> (8*i)); }
        return Convert.ToHexString(h).ToLower();
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

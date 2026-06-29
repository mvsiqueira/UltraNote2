namespace UltraNote.Core.Entities;

/// <summary>An attachment (e.g. a pasted image). The binary lives on the assets volume; only the path is stored.</summary>
public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NoteId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Relative path under the assets root where the binary is stored.</summary>
    public string StoragePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Note? Note { get; set; }
}

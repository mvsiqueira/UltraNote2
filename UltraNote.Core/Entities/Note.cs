namespace UltraNote.Core.Entities;

/// <summary>A single note. Content is HTML (the same format the TipTap editor produces).</summary>
public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FolderId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Rich-text body as HTML.</summary>
    public string ContentHtml { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Folder? Folder { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
}

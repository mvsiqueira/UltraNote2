namespace UltraNote.Core.Entities;

/// <summary>A folder in the notes tree. The root is represented by <see cref="ParentId"/> == null.</summary>
public class Folder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Parent folder, or null for top-level folders.</summary>
    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Materialized "/"-separated path (e.g. "Inbox/Ideias"). Maintained on create/rename/move.</summary>
    public string Path { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Folder? Parent { get; set; }
    public List<Folder> Children { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
}

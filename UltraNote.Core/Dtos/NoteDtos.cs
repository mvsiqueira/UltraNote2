namespace UltraNote.Core.Dtos;

/// <summary>Lightweight projection for note lists (no body).</summary>
public record NoteSummaryDto(
    Guid Id,
    Guid FolderId,
    string Title,
    DateTime UpdatedAt,
    bool IsFavorite = false,
    bool IsArchived = false);

/// <summary>Full note including the HTML body.</summary>
public record NoteDto(
    Guid Id,
    Guid FolderId,
    string Title,
    string ContentHtml,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsFavorite = false,
    bool IsArchived = false);

public record CreateNoteRequest(Guid FolderId, string Title, string? ContentHtml);

public record UpdateNoteRequest(string? Title, string? ContentHtml, Guid? FolderId, bool? IsFavorite = null, bool? IsArchived = null);

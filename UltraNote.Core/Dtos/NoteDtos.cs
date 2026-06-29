namespace UltraNote.Core.Dtos;

/// <summary>Lightweight projection for note lists (no body).</summary>
public record NoteSummaryDto(
    Guid Id,
    Guid FolderId,
    string Title,
    DateTime UpdatedAt);

/// <summary>Full note including the HTML body.</summary>
public record NoteDto(
    Guid Id,
    Guid FolderId,
    string Title,
    string ContentHtml,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateNoteRequest(Guid FolderId, string Title, string? ContentHtml);

public record UpdateNoteRequest(string? Title, string? ContentHtml, Guid? FolderId);

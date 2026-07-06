namespace UltraNote.Core.Dtos;

public record AttachmentDto(
    Guid Id,
    Guid NoteId,
    string FileName,
    string ContentType,
    string Url,
    DateTime CreatedAt,
    bool IsEmbedded = false);

public record RenameAttachmentRequest(string FileName);

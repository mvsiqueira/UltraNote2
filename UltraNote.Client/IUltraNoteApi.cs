using UltraNote.Core.Dtos;

namespace UltraNote.Client;

/// <summary>
/// Abstraction over the UltraNote API. Both the web app and the Windows (Blazor Hybrid)
/// app depend on this; each supplies an HttpClient-backed implementation.
/// </summary>
public interface IUltraNoteApi
{
    Task<IReadOnlyList<FolderDto>> GetFoldersAsync(CancellationToken ct = default);
    Task<FolderDto?> GetFolderAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NoteSummaryDto>> GetNotesAsync(Guid folderId, CancellationToken ct = default);
    Task<FolderDto> CreateFolderAsync(CreateFolderRequest req, CancellationToken ct = default);
    Task<FolderDto> UpdateFolderAsync(Guid id, UpdateFolderRequest req, CancellationToken ct = default);
    Task DeleteFolderAsync(Guid id, CancellationToken ct = default);

    Task<NoteDto?> GetNoteAsync(Guid id, CancellationToken ct = default);
    Task<NoteDto> CreateNoteAsync(CreateNoteRequest req, CancellationToken ct = default);
    Task<NoteDto> UpdateNoteAsync(Guid id, UpdateNoteRequest req, CancellationToken ct = default);
    Task DeleteNoteAsync(Guid id, CancellationToken ct = default);

    Task<AttachmentDto> UploadAttachmentAsync(Guid noteId, string fileName, string contentType, Stream content, CancellationToken ct = default);
    string GetAttachmentUrl(Guid attachmentId);
}

using System.Net;
using System.Net.Http.Json;
using UltraNote.Core.Dtos;

namespace UltraNote.Client;

/// <summary>HttpClient-backed implementation of <see cref="IUltraNoteApi"/>.</summary>
public class UltraNoteApiClient(HttpClient http) : IUltraNoteApi
{
    public async Task<IReadOnlyList<FolderDto>> GetFoldersAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<FolderDto>>("api/folders", ct) ?? [];

    public async Task<FolderDto?> GetFolderAsync(Guid id, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"api/folders/{id}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<FolderDto>(ct);
    }

    public async Task<IReadOnlyList<NoteSummaryDto>> GetNotesAsync(Guid folderId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<NoteSummaryDto>>($"api/folders/{folderId}/notes", ct) ?? [];

    public async Task<FolderDto> CreateFolderAsync(CreateFolderRequest req, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("api/folders", req, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<FolderDto>(ct))!;
    }

    public async Task<FolderDto> UpdateFolderAsync(Guid id, UpdateFolderRequest req, CancellationToken ct = default)
    {
        var res = await http.PutAsJsonAsync($"api/folders/{id}", req, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<FolderDto>(ct))!;
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken ct = default) =>
        (await http.DeleteAsync($"api/folders/{id}", ct)).EnsureSuccessStatusCode();

    public async Task<NoteDto?> GetNoteAsync(Guid id, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"api/notes/{id}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<NoteDto>(ct);
    }

    public async Task<NoteDto> CreateNoteAsync(CreateNoteRequest req, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("api/notes", req, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<NoteDto>(ct))!;
    }

    public async Task<NoteDto> UpdateNoteAsync(Guid id, UpdateNoteRequest req, CancellationToken ct = default)
    {
        var res = await http.PutAsJsonAsync($"api/notes/{id}", req, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<NoteDto>(ct))!;
    }

    public async Task DeleteNoteAsync(Guid id, CancellationToken ct = default) =>
        (await http.DeleteAsync($"api/notes/{id}", ct)).EnsureSuccessStatusCode();

    public async Task<IReadOnlyList<AttachmentDto>> GetAttachmentsAsync(Guid noteId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<AttachmentDto>>($"api/notes/{noteId}/attachments", ct) ?? [];

    public async Task<AttachmentDto> UploadAttachmentAsync(
        Guid noteId, string fileName, string contentType, Stream content, bool isEmbedded = false, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        var url = isEmbedded
            ? $"api/notes/{noteId}/attachments?embedded=true"
            : $"api/notes/{noteId}/attachments";
        var res = await http.PostAsync(url, form, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttachmentDto>(ct))!;
    }

    public async Task<AttachmentDto> RenameAttachmentAsync(Guid id, string fileName, CancellationToken ct = default)
    {
        var res = await http.PatchAsJsonAsync($"api/attachments/{id}", new RenameAttachmentRequest(FileName: fileName), ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttachmentDto>(ct))!;
    }

    public async Task<AttachmentDto> SetAttachmentEmbeddedAsync(Guid id, bool isEmbedded, CancellationToken ct = default)
    {
        var res = await http.PatchAsJsonAsync($"api/attachments/{id}", new RenameAttachmentRequest(IsEmbedded: isEmbedded), ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttachmentDto>(ct))!;
    }

    public async Task DeleteAttachmentAsync(Guid id, CancellationToken ct = default) =>
        (await http.DeleteAsync($"api/attachments/{id}", ct)).EnsureSuccessStatusCode();

    public string GetAttachmentUrl(Guid attachmentId)
    {
        var baseUri = http.BaseAddress is null ? "" : http.BaseAddress.ToString().TrimEnd('/');
        return $"{baseUri}/api/attachments/{attachmentId}";
    }

    public string GetAttachmentDownloadUrl(Guid attachmentId)
    {
        var baseUri = http.BaseAddress is null ? "" : http.BaseAddress.ToString().TrimEnd('/');
        return $"{baseUri}/api/attachments/{attachmentId}?download=true";
    }
}

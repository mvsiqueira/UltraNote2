using Microsoft.EntityFrameworkCore;
using UltraNote.Api.Data;
using UltraNote.Api.Storage;
using UltraNote.Core.Dtos;
using UltraNote.Core.Entities;

namespace UltraNote.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static RouteGroupBuilder MapAttachmentEndpoints(this RouteGroupBuilder api)
    {
        // Upload an attachment to a note.
        api.MapPost("/notes/{noteId:guid}/attachments",
            async (Guid noteId, IFormFile file, AppDbContext db, IAttachmentStorage storage, CancellationToken ct) =>
            {
                if (!await db.Notes.AnyAsync(n => n.Id == noteId, ct))
                    return Results.NotFound("Note not found.");
                if (file is null || file.Length == 0)
                    return Results.BadRequest("Empty file.");

                await using var stream = file.OpenReadStream();
                var path = await storage.SaveAsync(noteId, file.FileName, stream, ct);

                var att = new Attachment
                {
                    NoteId = noteId,
                    FileName = file.FileName,
                    ContentType = file.ContentType ?? "application/octet-stream",
                    StoragePath = path,
                };
                db.Attachments.Add(att);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/attachments/{att.Id}", ToDto(att));
            })
            .WithTags("Attachments")
            .DisableAntiforgery();

        var g = api.MapGroup("/attachments").WithTags("Attachments");

        // Download the binary.
        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, IAttachmentStorage storage) =>
        {
            var att = await db.Attachments.FindAsync(id);
            if (att is null) return Results.NotFound();
            var stream = storage.OpenRead(att.StoragePath);
            return stream is null
                ? Results.NotFound("File missing on disk.")
                : Results.File(stream, att.ContentType, att.FileName);
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, IAttachmentStorage storage) =>
        {
            var att = await db.Attachments.FindAsync(id);
            if (att is null) return Results.NotFound();
            storage.Delete(att.StoragePath);
            db.Attachments.Remove(att);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return api;
    }

    private static AttachmentDto ToDto(Attachment a) =>
        new(a.Id, a.NoteId, a.FileName, a.ContentType, $"/api/attachments/{a.Id}", a.CreatedAt);
}

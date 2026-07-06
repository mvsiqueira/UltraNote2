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
        // Pass ?embedded=true when the file will be embedded inline in the editor (e.g. an image).
        api.MapPost("/notes/{noteId:guid}/attachments",
            async (Guid noteId, IFormFile file, bool? embedded, AppDbContext db, IAttachmentStorage storage, CancellationToken ct) =>
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
                    IsEmbedded = embedded ?? false,
                };
                db.Attachments.Add(att);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/attachments/{att.Id}", ToDto(att));
            })
            .WithTags("Attachments")
            .DisableAntiforgery();

        // List attachments for a note.
        api.MapGet("/notes/{noteId:guid}/attachments",
            async (Guid noteId, AppDbContext db, CancellationToken ct) =>
            {
                if (!await db.Notes.AnyAsync(n => n.Id == noteId, ct))
                    return Results.NotFound("Note not found.");
                var list = await db.Attachments
                    .Where(a => a.NoteId == noteId)
                    .OrderBy(a => a.FileName)
                    .ToListAsync(ct);
                return Results.Ok(list.Select(ToDto));
            })
            .WithTags("Attachments");

        var g = api.MapGroup("/attachments").WithTags("Attachments");

        // Serve the binary. Without ?download=true: inline (for double-click preview).
        // With ?download=true: attachment disposition (forces browser download).
        g.MapGet("/{id:guid}", async (Guid id, bool? download, AppDbContext db, IAttachmentStorage storage) =>
        {
            var att = await db.Attachments.FindAsync(id);
            if (att is null) return Results.NotFound();
            var stream = storage.OpenRead(att.StoragePath);
            if (stream is null) return Results.NotFound("File missing on disk.");
            return download == true
                ? Results.File(stream, att.ContentType, att.FileName)
                : Results.File(stream, att.ContentType);
        });

        g.MapPatch("/{id:guid}", async (Guid id, RenameAttachmentRequest req, AppDbContext db) =>
        {
            var att = await db.Attachments.FindAsync(id);
            if (att is null) return Results.NotFound();
            att.FileName = req.FileName.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(att));
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
        new(a.Id, a.NoteId, a.FileName, a.ContentType, $"/api/attachments/{a.Id}", a.CreatedAt, a.IsEmbedded);
}

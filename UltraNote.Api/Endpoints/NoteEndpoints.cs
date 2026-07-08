using Microsoft.EntityFrameworkCore;
using UltraNote.Api.Data;
using UltraNote.Core.Dtos;
using UltraNote.Core.Entities;

namespace UltraNote.Api.Endpoints;

public static class NoteEndpoints
{
    public static RouteGroupBuilder MapNoteEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/notes").WithTags("Notes");

        g.MapGet("/favorites", async (AppDbContext db) =>
        {
            var notes = await db.Notes
                .Where(n => n.IsFavorite)
                .OrderBy(n => n.Title)
                .Select(n => new NoteSummaryDto(n.Id, n.FolderId, n.Title, n.UpdatedAt, n.IsFavorite))
                .ToListAsync();
            return Results.Ok(notes);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var n = await db.Notes.FindAsync(id);
            return n is null ? Results.NotFound() : Results.Ok(ToDto(n));
        });

        g.MapPost("/", async (CreateNoteRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return Results.BadRequest("Title is required.");
            if (!await db.Folders.AnyAsync(f => f.Id == req.FolderId))
                return Results.BadRequest("Folder not found.");

            var note = new Note
            {
                FolderId = req.FolderId,
                Title = req.Title.Trim(),
                ContentHtml = req.ContentHtml ?? string.Empty,
            };
            db.Notes.Add(note);
            await db.SaveChangesAsync();
            return Results.Created($"/api/notes/{note.Id}", ToDto(note));
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateNoteRequest req, AppDbContext db) =>
        {
            var note = await db.Notes.FindAsync(id);
            if (note is null) return Results.NotFound();

            if (req.FolderId is { } fid && fid != note.FolderId)
            {
                if (!await db.Folders.AnyAsync(f => f.Id == fid))
                    return Results.BadRequest("Target folder not found.");
                note.FolderId = fid;
            }
            if (!string.IsNullOrWhiteSpace(req.Title))
                note.Title = req.Title.Trim();
            if (req.ContentHtml is not null)
                note.ContentHtml = req.ContentHtml;
            if (req.IsFavorite is { } fav)
                note.IsFavorite = fav;

            note.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(note));
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var note = await db.Notes.FindAsync(id);
            if (note is null) return Results.NotFound();
            db.Notes.Remove(note); // cascade removes attachment rows (files cleaned separately)
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return api;
    }

    private static NoteDto ToDto(Note n) =>
        new(n.Id, n.FolderId, n.Title, n.ContentHtml, n.CreatedAt, n.UpdatedAt, n.IsFavorite);
}

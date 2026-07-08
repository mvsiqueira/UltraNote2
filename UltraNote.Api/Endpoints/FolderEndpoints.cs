using Microsoft.EntityFrameworkCore;
using UltraNote.Api.Data;
using UltraNote.Core.Dtos;
using UltraNote.Core.Entities;

namespace UltraNote.Api.Endpoints;

public static class FolderEndpoints
{
    public static RouteGroupBuilder MapFolderEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/folders").WithTags("Folders");

        // Full folder tree (flat list, ordered by path — the client builds the tree).
        g.MapGet("/", async (AppDbContext db) =>
        {
            var folders = await db.Folders
                .OrderBy(f => f.Path)
                .Select(f => ToDto(f))
                .ToListAsync();
            return Results.Ok(folders);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var f = await db.Folders.FindAsync(id);
            return f is null ? Results.NotFound() : Results.Ok(ToDto(f));
        });

        // Notes in a folder (summaries, no body).
        g.MapGet("/{id:guid}/notes", async (Guid id, AppDbContext db) =>
        {
            if (!await db.Folders.AnyAsync(f => f.Id == id)) return Results.NotFound();
            var notes = await db.Notes
                .Where(n => n.FolderId == id)
                .OrderBy(n => n.Title)
                .Select(n => new NoteSummaryDto(n.Id, n.FolderId, n.Title, n.UpdatedAt, n.IsFavorite))
                .ToListAsync();
            return Results.Ok(notes);
        });

        g.MapPost("/", async (CreateFolderRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest("Name is required.");

            Folder? parent = null;
            if (req.ParentId is { } pid)
            {
                parent = await db.Folders.FindAsync(pid);
                if (parent is null) return Results.BadRequest("Parent folder not found.");
            }

            var folder = new Folder
            {
                ParentId = req.ParentId,
                Name = req.Name.Trim(),
            };
            folder.Path = BuildPath(parent?.Path, folder.Name);

            db.Folders.Add(folder);
            await db.SaveChangesAsync();
            return Results.Created($"/api/folders/{folder.Id}", ToDto(folder));
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateFolderRequest req, AppDbContext db) =>
        {
            var folder = await db.Folders.FindAsync(id);
            if (folder is null) return Results.NotFound();

            // Move: validate the new parent isn't the folder itself or one of its descendants.
            if (req.ParentId != folder.ParentId)
            {
                if (req.ParentId == folder.Id)
                    return Results.BadRequest("A folder cannot be its own parent.");

                Folder? newParent = null;
                if (req.ParentId is { } pid)
                {
                    newParent = await db.Folders.FindAsync(pid);
                    if (newParent is null) return Results.BadRequest("Parent folder not found.");
                    if (newParent.Path == folder.Path || newParent.Path.StartsWith(folder.Path + "/"))
                        return Results.BadRequest("Cannot move a folder into its own descendant.");
                }
                folder.ParentId = req.ParentId;
            }

            if (!string.IsNullOrWhiteSpace(req.Name))
                folder.Name = req.Name.Trim();

            await RecomputeSubtreePathsAsync(db, folder);
            folder.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(folder));
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var folder = await db.Folders.FindAsync(id);
            if (folder is null) return Results.NotFound();
            db.Folders.Remove(folder); // cascade removes subfolders + notes + attachment rows
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return api;
    }

    private static FolderDto ToDto(Folder f) =>
        new(f.Id, f.ParentId, f.Name, f.Path, f.CreatedAt, f.UpdatedAt);

    private static string BuildPath(string? parentPath, string name) =>
        string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";

    /// <summary>Recomputes Path for the folder and every descendant (load-all is fine for a personal library).</summary>
    private static async Task RecomputeSubtreePathsAsync(AppDbContext db, Folder folder)
    {
        var parentPath = folder.ParentId is { } pid
            ? (await db.Folders.FindAsync(pid))?.Path
            : null;
        folder.Path = BuildPath(parentPath, folder.Name);

        var all = await db.Folders.ToListAsync();
        // ToLookup tolerates the null key used by root folders (ToDictionary does not).
        var byParent = all.ToLookup(f => f.ParentId);

        void Recurse(Folder current)
        {
            foreach (var child in byParent[current.Id])
            {
                child.Path = BuildPath(current.Path, child.Name);
                Recurse(child);
            }
        }
        Recurse(folder);
    }
}

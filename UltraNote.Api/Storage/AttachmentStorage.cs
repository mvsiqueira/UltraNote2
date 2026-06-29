namespace UltraNote.Api.Storage;

/// <summary>Stores attachment binaries on the assets volume, keyed by a relative path.</summary>
public interface IAttachmentStorage
{
    /// <summary>Saves the stream and returns the relative storage path.</summary>
    Task<string> SaveAsync(Guid noteId, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>Opens a stored file for reading, or null if it no longer exists.</summary>
    Stream? OpenRead(string storagePath);

    void Delete(string storagePath);
}

public class FileSystemAttachmentStorage : IAttachmentStorage
{
    private readonly string _root;

    public FileSystemAttachmentStorage(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Storage:AssetsPath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? System.IO.Path.Combine(env.ContentRootPath, "assets")
            : configured;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Guid noteId, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeName = MakeSafe(fileName);
        var relative = System.IO.Path.Combine(noteId.ToString(), $"{Guid.NewGuid():N}_{safeName}");
        var full = System.IO.Path.Combine(_root, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);

        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);

        // store with forward slashes so paths are portable across OSes
        return relative.Replace('\\', '/');
    }

    public Stream? OpenRead(string storagePath)
    {
        var full = Resolve(storagePath);
        return File.Exists(full) ? File.OpenRead(full) : null;
    }

    public void Delete(string storagePath)
    {
        var full = Resolve(storagePath);
        if (File.Exists(full)) File.Delete(full);
    }

    private string Resolve(string storagePath)
    {
        // guard against path traversal: the resolved path must stay under _root
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, storagePath));
        var rootFull = System.IO.Path.GetFullPath(_root);
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage path.");
        return full;
    }

    private static string MakeSafe(string fileName)
    {
        var name = System.IO.Path.GetFileName(fileName);
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }
}

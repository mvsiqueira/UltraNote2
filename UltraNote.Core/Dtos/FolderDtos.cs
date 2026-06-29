namespace UltraNote.Core.Dtos;

public record FolderDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateFolderRequest(Guid? ParentId, string Name);

public record UpdateFolderRequest(string? Name, Guid? ParentId);

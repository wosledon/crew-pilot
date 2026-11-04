namespace CrewPilot.Infrastructure.Storage;

/// <summary>本地磁盘附件存储：按工作项分目录，文件名用 GUID 防止路径注入与重名。</summary>
public sealed class LocalAttachmentStorage : IAttachmentStorage
{
    private readonly string _rootPath;

    public LocalAttachmentStorage(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
    }

    public long MaxFileSize => 20 * 1024 * 1024;

    public async Task<(string RelativePath, long Size)> SaveAsync(
        Guid workItemId, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "file";
        }

        var extension = Path.GetExtension(safeName);
        if (extension.Length > 32)
        {
            extension = string.Empty;
        }

        var relativePath = Path.Combine(
            workItemId.ToString("N"),
            $"{Guid.NewGuid():N}{extension}");
        var fullPath = GetFullPath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var target = File.Create(fullPath))
        {
            await content.CopyToAsync(target, ct);
        }

        return (relativePath, new FileInfo(fullPath).Length);
    }

    public string GetFullPath(string relativePath)
    {
        var root = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("附件路径越界");
        }

        return full;
    }

    public bool DeleteFile(string relativePath)
    {
        try
        {
            var full = GetFullPath(relativePath);
            if (!File.Exists(full))
            {
                return false;
            }

            File.Delete(full);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

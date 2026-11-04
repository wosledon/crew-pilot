using System.IO.Compression;

namespace CrewPilot.Infrastructure.Storage;

/// <summary>
/// Skill 资源目录存储：每个 Skill 一个目录，可存放 scripts/ 与 resources/ 文件。
/// 仅允许这两类相对路径，拒绝 zip 路径穿越；导出为含 SKILL.md 的资源包。
/// </summary>
public sealed class SkillStorage
{
    public const long MaxBundleBytes = 10 * 1024 * 1024;
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxFiles = 100;
    public const long MaxTotalBytes = 20 * 1024 * 1024;

    private static readonly string[] AllowedPrefixes = ["scripts/", "resources/"];

    private readonly string _rootPath;

    public SkillStorage(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
    }

    public string SkillDirectory(Guid skillId) => Path.Combine(_rootPath, skillId.ToString("N"));

    /// <summary>解压脚本/资源文件（忽略 SKILL.md 等元数据），返回文件数。</summary>
    public async Task<int> ExtractAsync(
        Guid skillId, Stream zipStream, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var directory = SkillDirectory(skillId);
        Directory.CreateDirectory(directory);

        var files = 0;
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                continue; // 目录项
            }

            var relative = NormalizeEntryPath(entry.FullName);
            if (relative is null)
            {
                continue; // SKILL.md / 其他元数据
            }

            files++;
            if (files > MaxFiles)
            {
                throw new InvalidOperationException($"资源文件超过 {MaxFiles} 个上限");
            }

            if (entry.Length > MaxFileBytes)
            {
                throw new InvalidOperationException($"文件 {relative} 超过 5MB 上限");
            }

            total += entry.Length;
            if (total > MaxTotalBytes)
            {
                throw new InvalidOperationException("资源包解压后超过 20MB 上限");
            }

            var target = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            var full = Path.GetFullPath(target);
            if (!full.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"非法资源路径：{entry.FullName}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await using var source = entry.Open();
            await using var destination = File.Create(full);
            await source.CopyToAsync(destination, ct);
        }

        return files;
    }

    public IReadOnlyList<string> ListFiles(Guid skillId)
    {
        var directory = SkillDirectory(skillId);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>打包为 zip：SKILL.md + scripts/ + resources/。</summary>
    public byte[] BuildBundle(Guid skillId, string skillMarkdown)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var markdownEntry = archive.CreateEntry("SKILL.md", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(markdownEntry.Open()))
            {
                writer.Write(skillMarkdown);
            }

            var directory = SkillDirectory(skillId);
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(
                             directory, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
                    var entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
                    using var source = File.OpenRead(file);
                    using var target = entry.Open();
                    source.CopyTo(target);
                }
            }
        }

        return output.ToArray();
    }

    public void Delete(Guid skillId)
    {
        var directory = SkillDirectory(skillId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>仅保留 scripts/ 与 resources/ 下的安全相对路径；其他返回 null。</summary>
    private static string? NormalizeEntryPath(string entryName)
    {
        var normalized = entryName.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException($"非法资源路径：{entryName}");
        }

        return AllowedPrefixes.Any(prefix =>
            normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : null;
    }
}

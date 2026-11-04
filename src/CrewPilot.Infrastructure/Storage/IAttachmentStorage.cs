namespace CrewPilot.Infrastructure.Storage;

/// <summary>附件存储抽象：本地目录实现，后续可替换为对象存储。</summary>
public interface IAttachmentStorage
{
    /// <summary>单文件大小上限（字节）。</summary>
    long MaxFileSize { get; }

    /// <summary>保存附件，返回相对存储路径与实际大小。</summary>
    Task<(string RelativePath, long Size)> SaveAsync(
        Guid workItemId, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>相对路径 → 绝对路径（校验仍在存储根目录内）。</summary>
    string GetFullPath(string relativePath);

    /// <summary>删除物理文件；不存在时返回 false。</summary>
    bool DeleteFile(string relativePath);
}

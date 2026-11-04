namespace CrewPilot.Api.Contracts;

public sealed record CommentDto(
    Guid Id,
    Guid WorkItemId,
    string Content,
    string AuthorName,
    string AuthorKind,
    DateTimeOffset CreatedAt);

/// <summary>创建评论的结果：评论本身 + @Agent 时触发的 AgentRun。</summary>
public sealed record CreateCommentResultDto(CommentDto Comment, Guid? AgentRunId);

public sealed record CreateCommentRequest(string Content, Guid? MentionAgentId = null);

public sealed record AttachmentDto(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long Size,
    DateTimeOffset CreatedAt);

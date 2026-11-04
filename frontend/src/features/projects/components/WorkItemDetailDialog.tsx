import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Download, Paperclip, Pencil, Send, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { Textarea } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import { useAgents } from "@/features/agents/api";
import { formatDateTime } from "@/lib/format";
import type { WorkItemDetail } from "@/lib/types";
import { priorityTone, statusTone } from "../statusFlow";
import {
  useAddComment,
  useAttachments,
  useComments,
  useDeleteAttachment,
  useDeleteComment,
  useDeleteWorkItem,
  useUploadAttachment,
  useWorkItem,
  useWorkLogs,
} from "../api";

export interface WorkItemDetailDialogProps {
  workItemId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onEdit: (item: WorkItemDetail) => void;
}

/** 工作项详情：描述/验收标准、工时记录、评论（可 @Agent）、附件。 */
export function WorkItemDetailDialog({
  workItemId,
  open,
  onOpenChange,
  onEdit,
}: WorkItemDetailDialogProps) {
  const { t } = useTranslation(["workitems", "collaboration", "common"]);
  const item = useWorkItem(workItemId);
  const workLogs = useWorkLogs(workItemId);
  const comments = useComments(workItemId);
  const attachments = useAttachments(workItemId);
  const agents = useAgents();
  const addComment = useAddComment();
  const deleteComment = useDeleteComment();
  const uploadAttachment = useUploadAttachment();
  const deleteAttachment = useDeleteAttachment();
  const deleteWorkItem = useDeleteWorkItem();
  const confirm = useConfirm();
  const fileInput = useRef<HTMLInputElement>(null);

  const [content, setContent] = useState("");
  const [mentionAgentId, setMentionAgentId] = useState("");

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleSend = async () => {
    if (content.trim() === "") {
      return;
    }
    try {
      const result = await addComment.mutateAsync({
        workItemId,
        content: content.trim(),
        mentionAgentId: mentionAgentId === "" ? undefined : mentionAgentId,
      });
      toast.success(
        result.agentRunId
          ? t("collaboration:commentAddedWithRun")
          : t("collaboration:commentAdded"),
      );
      setContent("");
      setMentionAgentId("");
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDeleteComment = async (id: string) => {
    const approved = await confirm({
      title: t("collaboration:deleteComment"),
      description: t("collaboration:deleteCommentConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteComment.mutateAsync(id);
      toast.success(t("collaboration:commentDeleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleFileSelected = async (file: File | undefined) => {
    if (file === undefined) {
      return;
    }
    try {
      await uploadAttachment.mutateAsync({ workItemId, file });
      toast.success(t("collaboration:uploaded"));
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      if (fileInput.current !== null) {
        fileInput.current.value = "";
      }
    }
  };

  const handleDeleteAttachment = async (id: string) => {
    const approved = await confirm({
      title: t("collaboration:deleteAttachment"),
      description: t("collaboration:deleteAttachmentConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteAttachment.mutateAsync(id);
      toast.success(t("collaboration:attachmentDeleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDeleteWorkItem = async () => {
    const approved = await confirm({
      title: t("workitems:deleteConfirm.title"),
      description: t("workitems:deleteConfirm.description"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteWorkItem.mutateAsync(workItemId);
      toast.success(t("workitems:deleted"));
      onOpenChange(false);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const detail = item.data;
  const agentOptions = (agents.data ?? [])
    .filter((agent) => agent.enabled)
    .map((agent) => ({ value: agent.id, label: `@${agent.name}` }));

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("workitems:detailTitle")}
      closeLabel={t("common:cancel")}
      className="max-w-2xl"
    >
      {item.isPending ? (
        <LoadingState />
      ) : item.isError || detail === undefined ? (
        <ErrorState onRetry={() => void item.refetch()} />
      ) : (
        <div className="space-y-5">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone="neutral">{t(`workitems:types.${detail.type}`)}</Badge>
              <Badge tone={statusTone(detail.status)}>
                {t(`workitems:statuses.${detail.status.toLowerCase()}`)}
              </Badge>
              <Badge tone={priorityTone(detail.priority)}>
                {t(`workitems:priorities.${detail.priority}`)}
              </Badge>
            </div>
            <h3 className="mt-2 text-base font-semibold">{detail.title}</h3>
            <p className="mt-1 text-xs text-muted-foreground">
              {t("collaboration:assignee")}：{detail.assigneeName ? `@${detail.assigneeName}` : "—"}{" "}
              ·{" "}
              {t("workitems:hoursSummary", {
                logged: detail.loggedHours,
                estimated: detail.estimatedHours ?? "—",
              })}
            </p>
            <div className="mt-3 flex gap-2">
              <Button variant="outline" size="sm" onClick={() => onEdit(detail)}>
                <Pencil className="h-3.5 w-3.5" />
                {t("workitems:edit")}
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => void handleDeleteWorkItem()}
                disabled={deleteWorkItem.isPending}
              >
                <Trash2 className="h-3.5 w-3.5 text-destructive" />
                {t("common:delete")}
              </Button>
            </div>
          </div>

          {detail.description ? (
            <section>
              <h4 className="text-sm font-medium">{t("workitems:description")}</h4>
              <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                {detail.description}
              </p>
            </section>
          ) : null}

          <section>
            <h4 className="text-sm font-medium">{t("workitems:acceptanceCriteria")}</h4>
            <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
              {detail.acceptanceCriteria ?? t("workitems:acceptanceCriteriaEmpty")}
            </p>
          </section>

          <section>
            <h4 className="text-sm font-medium">{t("collaboration:worklogs")}</h4>
            {workLogs.isPending ? (
              <LoadingState />
            ) : (workLogs.data ?? []).length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">{t("collaboration:noWorklogs")}</p>
            ) : (
              <ul className="mt-2 space-y-1 text-sm">
                {(workLogs.data ?? []).map((log) => (
                  <li key={log.id} className="flex items-center justify-between gap-3">
                    <span className="text-muted-foreground">
                      {log.date} · {log.memberName ?? "—"} {log.note ? `· ${log.note}` : ""}
                    </span>
                    <span className="font-medium">{log.hours}h</span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section>
            <h4 className="text-sm font-medium">{t("collaboration:comments")}</h4>
            {comments.isPending ? (
              <LoadingState />
            ) : (comments.data ?? []).length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">{t("collaboration:noComments")}</p>
            ) : (
              <ul className="mt-2 space-y-2">
                {(comments.data ?? []).map((comment) => (
                  <li
                    key={comment.id}
                    className="rounded-md border border-border bg-muted/30 px-3 py-2"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 text-xs">
                        <Badge tone={comment.authorKind === "agent" ? "info" : "neutral"}>
                          {t(`collaboration:authorKinds.${comment.authorKind}`)}
                        </Badge>
                        <span className="font-medium">{comment.authorName}</span>
                        <span className="text-muted-foreground">
                          {formatDateTime(comment.createdAt)}
                        </span>
                      </div>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t("collaboration:deleteComment")}
                        onClick={() => void handleDeleteComment(comment.id)}
                      >
                        <Trash2 className="h-3.5 w-3.5 text-destructive" />
                      </Button>
                    </div>
                    <p className="mt-1 whitespace-pre-wrap text-sm">{comment.content}</p>
                  </li>
                ))}
              </ul>
            )}

            <div className="mt-3 space-y-2">
              <Textarea
                value={content}
                onChange={(event) => setContent(event.target.value)}
                placeholder={t("collaboration:commentPlaceholder")}
              />
              <div className="flex items-center justify-between gap-2">
                <Select
                  value={mentionAgentId}
                  onValueChange={setMentionAgentId}
                  options={[{ value: "", label: t("collaboration:mentionNone") }, ...agentOptions]}
                  placeholder={t("collaboration:mention")}
                  ariaLabel={t("collaboration:mention")}
                  className="w-52"
                />
                <Button
                  onClick={() => void handleSend()}
                  disabled={addComment.isPending || content.trim() === ""}
                >
                  <Send className="h-4 w-4" />
                  {t("collaboration:send")}
                </Button>
              </div>
            </div>
          </section>

          <section>
            <div className="flex items-center justify-between">
              <h4 className="text-sm font-medium">{t("collaboration:attachments")}</h4>
              <input
                ref={fileInput}
                type="file"
                className="hidden"
                onChange={(event) => void handleFileSelected(event.target.files?.[0])}
              />
              <Button
                variant="outline"
                size="sm"
                aria-label={t("collaboration:upload")}
                disabled={uploadAttachment.isPending}
                onClick={() => fileInput.current?.click()}
              >
                <Paperclip className="h-3.5 w-3.5" />
                {t("collaboration:upload")}
              </Button>
            </div>
            {attachments.isPending ? (
              <LoadingState />
            ) : (attachments.data ?? []).length === 0 ? (
              <p className="mt-1 text-sm text-muted-foreground">
                {t("collaboration:noAttachments")}
              </p>
            ) : (
              <ul className="mt-2 divide-y divide-border">
                {(attachments.data ?? []).map((attachment) => (
                  <li
                    key={attachment.id}
                    className="flex items-center justify-between gap-2 py-1.5"
                  >
                    <a
                      href={`/api/attachments/${attachment.id}/download`}
                      className="flex min-w-0 items-center gap-2 text-sm hover:underline"
                    >
                      <Download className="h-3.5 w-3.5 shrink-0" />
                      <span className="truncate">{attachment.fileName}</span>
                      <span className="shrink-0 text-xs text-muted-foreground">
                        {t("collaboration:fileSize", {
                          size: Math.max(1, Math.round(attachment.size / 1024)),
                        })}
                      </span>
                    </a>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={t("collaboration:deleteAttachment")}
                      onClick={() => void handleDeleteAttachment(attachment.id)}
                    >
                      <Trash2 className="h-3.5 w-3.5 text-destructive" />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </Dialog>
  );
}

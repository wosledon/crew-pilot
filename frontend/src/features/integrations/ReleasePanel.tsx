import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Rocket } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Input, Textarea } from "@/components/ui/Input";
import { toast } from "@/components/ui/Toast";
import type { ReleaseResult } from "@/lib/types";
import { useCreateRelease } from "./api";

export interface ReleasePanelProps {
  projectId: string;
}

/** 发布面板：通过 gh/glab 打 tag 并创建 Release。 */
export function ReleasePanel({ projectId }: ReleasePanelProps) {
  const { t } = useTranslation(["integrations", "common"]);
  const createRelease = useCreateRelease(projectId);
  const [tag, setTag] = useState("");
  const [title, setTitle] = useState("");
  const [notes, setNotes] = useState("");
  const [result, setResult] = useState<ReleaseResult | null>(null);

  const handleCreate = async () => {
    try {
      const released = await createRelease.mutateAsync({
        tag: tag.trim(),
        title: title.trim() || undefined,
        notes: notes.trim() || undefined,
      });
      setResult(released);
      toast.success(t("integrations:released"));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Rocket className="h-4 w-4" />
          {t("integrations:release")}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("integrations:tag")}</span>
            <Input
              value={tag}
              onChange={(event) => setTag(event.target.value)}
              placeholder="v1.0.0"
            />
          </label>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("integrations:releaseTitle")}</span>
            <Input value={title} onChange={(event) => setTitle(event.target.value)} />
          </label>
        </div>
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("integrations:releaseNotes")}</span>
          <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} />
        </label>
        <div className="flex items-center gap-3">
          <Button
            onClick={() => void handleCreate()}
            disabled={createRelease.isPending || tag.trim() === ""}
          >
            {t("integrations:createRelease")}
          </Button>
          {result?.url ? (
            <a
              href={result.url}
              target="_blank"
              rel="noreferrer"
              className="truncate text-sm text-primary hover:underline"
            >
              {result.tag}
            </a>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}

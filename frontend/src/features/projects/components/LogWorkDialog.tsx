import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Textarea } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export interface LogWorkDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  workItemTitle: string;
  onSubmit: (body: { hours: number; note?: string }) => Promise<unknown>;
}

/** 登记工时（1:1，不加权） */
export function LogWorkDialog({ open, onOpenChange, workItemTitle, onSubmit }: LogWorkDialogProps) {
  const { t } = useTranslation(["workitems", "common"]);
  const [hours, setHours] = useState("1");
  const [note, setNote] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async () => {
    const value = Number(hours);
    if (!Number.isFinite(value) || value <= 0 || value > 24) {
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit({ hours: value, note: note.trim() || undefined });
      setHours("1");
      setNote("");
      onOpenChange(false);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={`${t("workitems:logWorkTitle")} · ${workItemTitle}`}
      closeLabel={t("common:cancel")}
    >
      <div className="space-y-4">
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:workHours")}</span>
          <Input
            type="number"
            min={0.5}
            max={24}
            step={0.5}
            value={hours}
            onChange={(event) => setHours(event.target.value)}
          />
        </label>
        <label className="block space-y-1.5 text-sm">
          <span className="font-medium">{t("workitems:note")}</span>
          <Textarea
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder={t("workitems:notePlaceholder")}
          />
        </label>
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t("common:cancel")}
          </Button>
          <Button onClick={() => void handleSubmit()} disabled={submitting}>
            {t("common:save")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

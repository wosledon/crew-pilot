import * as DialogPrimitive from "@radix-ui/react-dialog";
import type { ReactNode } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/cn";

export interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  closeLabel: string;
  children: ReactNode;
  className?: string;
}

/** 弹层：Radix Dialog 封装（Esc 关闭、点击外部关闭、焦点圈定） */
export function Dialog({
  open,
  onOpenChange,
  title,
  closeLabel,
  children,
  className,
}: DialogProps) {
  return (
    <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px]" />
        <DialogPrimitive.Content
          className={cn(
            "fixed left-1/2 top-1/2 z-50 max-h-[90vh] w-[92vw] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-xl bg-surface-container-high p-6 shadow-elevation-3",
            className,
          )}
        >
          <DialogPrimitive.Title className="pr-8 text-2xl font-normal leading-8 text-on-surface">
            {title}
          </DialogPrimitive.Title>
          <div className="mt-4">{children}</div>
          <DialogPrimitive.Close
            aria-label={closeLabel}
            className="absolute right-4 top-4 rounded-full p-2 text-on-surface-variant transition-colors hover:bg-on-surface/10"
          >
            <X className="h-4 w-4" />
          </DialogPrimitive.Close>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}

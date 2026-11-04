import { Toaster as SonnerToaster, toast as sonnerToast } from "sonner";

/** 轻提示：sonner 封装。禁止使用 alert（见 AGENTS.md 5.3） */
export function Toaster() {
  return (
    <SonnerToaster
      position="top-right"
      toastOptions={{
        style: {
          background: "var(--md-inverse-surface)",
          color: "var(--md-inverse-on-surface)",
          border: "none",
          borderRadius: "var(--md-shape-xs)",
          boxShadow: "var(--md-elevation-3)",
          fontSize: "14px",
          letterSpacing: "0.25px",
        },
      }}
    />
  );
}

export const toast = {
  success: (message: string) => sonnerToast.success(message),
  error: (message: string) => sonnerToast.error(message),
  info: (message: string) => sonnerToast.info(message),
};

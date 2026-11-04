import * as AlertDialogPrimitive from "@radix-ui/react-alert-dialog";
import type { ReactNode } from "react";
import { createContext, useCallback, useContext, useRef, useState } from "react";
import { Button } from "./Button";

export interface ConfirmOptions {
  title: string;
  description?: string;
  confirmText?: string;
  cancelText?: string;
  destructive?: boolean;
}

type ConfirmFn = (options: ConfirmOptions) => Promise<boolean>;

const ConfirmContext = createContext<ConfirmFn | null>(null);

/**
 * 确认框：全仓库统一入口（Radix AlertDialog 封装）。
 * 禁止使用 window.confirm / alert / prompt（见 AGENTS.md 5.3）。
 */
export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [options, setOptions] = useState<ConfirmOptions | null>(null);
  const resolver = useRef<((value: boolean) => void) | null>(null);

  const confirm = useCallback<ConfirmFn>((nextOptions) => {
    setOptions(nextOptions);
    return new Promise<boolean>((resolve) => {
      resolver.current = resolve;
    });
  }, []);

  const settle = (value: boolean) => {
    resolver.current?.(value);
    resolver.current = null;
    setOptions(null);
  };

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      <AlertDialogPrimitive.Root
        open={options !== null}
        onOpenChange={(open) => {
          if (!open) {
            settle(false);
          }
        }}
      >
        <AlertDialogPrimitive.Portal>
          <AlertDialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px]" />
          <AlertDialogPrimitive.Content className="fixed left-1/2 top-1/2 z-50 w-[92vw] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-xl bg-surface-container-high p-6 shadow-elevation-3">
            <AlertDialogPrimitive.Title className="text-2xl font-normal leading-8 text-on-surface">
              {options?.title}
            </AlertDialogPrimitive.Title>
            {options?.description ? (
              <AlertDialogPrimitive.Description className="mt-2 text-sm text-on-surface-variant">
                {options.description}
              </AlertDialogPrimitive.Description>
            ) : null}
            <div className="mt-6 flex justify-end gap-2">
              <AlertDialogPrimitive.Cancel asChild>
                <Button variant="outline" onClick={() => settle(false)}>
                  {options?.cancelText ?? "取消"}
                </Button>
              </AlertDialogPrimitive.Cancel>
              <AlertDialogPrimitive.Action asChild>
                <Button
                  variant={options?.destructive ? "destructive" : "primary"}
                  onClick={() => settle(true)}
                >
                  {options?.confirmText ?? "确认"}
                </Button>
              </AlertDialogPrimitive.Action>
            </div>
          </AlertDialogPrimitive.Content>
        </AlertDialogPrimitive.Portal>
      </AlertDialogPrimitive.Root>
    </ConfirmContext.Provider>
  );
}

export function useConfirm(): ConfirmFn {
  const context = useContext(ConfirmContext);
  if (context === null) {
    throw new Error("useConfirm 必须在 ConfirmProvider 内使用");
  }
  return context;
}

import type { InputHTMLAttributes, TextareaHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

export function Input({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cn(
        "flex h-10 w-full rounded-xs border border-outline bg-transparent px-3 text-sm text-on-surface transition-colors placeholder:text-on-surface-variant hover:border-on-surface focus-visible:border-primary focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}

export function Textarea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      className={cn(
        "flex min-h-20 w-full rounded-xs border border-outline bg-transparent px-3 py-2.5 text-sm text-on-surface transition-colors placeholder:text-on-surface-variant hover:border-on-surface focus-visible:border-primary focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}

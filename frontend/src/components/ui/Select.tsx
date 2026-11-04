import * as SelectPrimitive from "@radix-ui/react-select";
import { Check, ChevronDown } from "lucide-react";
import { cn } from "@/lib/cn";

export interface SelectOption {
  value: string;
  label: string;
}

/** Radix Select 不接受空字符串 value，这里用哨兵值承载"全部/未选择"语义 */
const EMPTY_VALUE = "__empty__";

export interface SelectProps {
  value: string;
  onValueChange: (value: string) => void;
  options: SelectOption[];
  placeholder?: string;
  className?: string;
  ariaLabel?: string;
  disabled?: boolean;
}

/** 下拉选择：Radix Select 封装。禁止使用原生 <select>（见 AGENTS.md 5.3） */
export function Select({
  value,
  onValueChange,
  options,
  placeholder,
  className,
  ariaLabel,
  disabled,
}: SelectProps) {
  return (
    <SelectPrimitive.Root
      value={
        value === "" && options.some((option) => option.value === "") ? EMPTY_VALUE : value
      }
      onValueChange={(next) => onValueChange(next === EMPTY_VALUE ? "" : next)}
    >
      <SelectPrimitive.Trigger
        aria-label={ariaLabel}
        disabled={disabled}
        className={cn(
          "flex h-10 w-full items-center justify-between gap-2 rounded-xs border border-outline bg-transparent px-3 text-sm text-on-surface transition-colors hover:border-on-surface disabled:cursor-not-allowed disabled:opacity-50 data-[placeholder]:text-on-surface-variant",
          className,
        )}
      >
        <SelectPrimitive.Value placeholder={placeholder} />
        <SelectPrimitive.Icon>
          <ChevronDown className="h-4 w-4 opacity-70" />
        </SelectPrimitive.Icon>
      </SelectPrimitive.Trigger>
      <SelectPrimitive.Portal>
        <SelectPrimitive.Content
          position="popper"
          className="z-50 max-h-72 min-w-(--radix-select-trigger-width) overflow-hidden rounded-sm bg-surface-container-high py-2 shadow-elevation-2"
        >
          <SelectPrimitive.Viewport>
            {options.map((option) => (
              <SelectPrimitive.Item
                key={option.value}
                value={option.value === "" ? EMPTY_VALUE : option.value}
                className="relative mx-1 flex cursor-default select-none items-center rounded-xs py-2 pl-9 pr-4 text-sm text-on-surface outline-none data-highlighted:bg-secondary-container data-highlighted:text-on-secondary-container"
              >
                <SelectPrimitive.ItemIndicator className="absolute left-2">
                  <Check className="h-4 w-4" />
                </SelectPrimitive.ItemIndicator>
                <SelectPrimitive.ItemText>{option.label}</SelectPrimitive.ItemText>
              </SelectPrimitive.Item>
            ))}
          </SelectPrimitive.Viewport>
        </SelectPrimitive.Content>
      </SelectPrimitive.Portal>
    </SelectPrimitive.Root>
  );
}

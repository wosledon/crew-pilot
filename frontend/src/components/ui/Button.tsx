import { cva, type VariantProps } from "class-variance-authority";
import { forwardRef } from "react";
import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

const buttonVariants = cva(
  "relative inline-flex select-none items-center justify-center gap-2 whitespace-nowrap rounded-full text-sm font-medium tracking-[0.1px] transition-[background-color,box-shadow,color] duration-200 ease-out focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:pointer-events-none disabled:opacity-40",
  {
    variants: {
      variant: {
        primary:
          "bg-primary text-primary-foreground shadow-elevation-1 hover:bg-primary/90 hover:shadow-elevation-2 active:bg-primary/80",
        outline:
          "border border-outline text-primary hover:bg-primary/10 active:bg-primary/15",
        ghost: "text-primary hover:bg-primary/10 active:bg-primary/15",
        destructive:
          "bg-error text-on-error shadow-elevation-1 hover:bg-error/90 hover:shadow-elevation-2 active:bg-error/80",
      },
      size: {
        sm: "h-8 px-3 text-xs",
        md: "h-10 px-6",
        icon: "h-10 w-10 px-0",
      },
    },
    defaultVariants: {
      variant: "primary",
      size: "md",
    },
  },
);

export interface ButtonProps
  extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {}

/** forwardRef：供 Radix asChild（AlertDialog Cancel/Action 等）传递 ref */
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, ...props }, ref) => (
    <button
      type="button"
      ref={ref}
      className={cn(buttonVariants({ variant, size }), className)}
      {...props}
    />
  ),
);

Button.displayName = "Button";

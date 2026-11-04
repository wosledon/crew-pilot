import Markdown from "react-markdown";
import { cn } from "@/lib/cn";

export interface MarkdownViewProps {
  content: string;
  className?: string;
}

/** 轻量 Markdown 渲染（标题/列表/代码/链接），用于需求描述与验收标准。 */
export function MarkdownView({ content, className }: MarkdownViewProps) {
  return (
    <div
      className={cn("text-sm text-muted-foreground [&_a]:text-primary [&_a]:underline", className)}
    >
      <Markdown
        components={{
          h1: (props) => (
            <h2 className="mb-1 mt-2 text-base font-semibold text-foreground" {...props} />
          ),
          h2: (props) => (
            <h3 className="mb-1 mt-2 text-sm font-semibold text-foreground" {...props} />
          ),
          h3: (props) => (
            <h4 className="mb-1 mt-2 text-sm font-medium text-foreground" {...props} />
          ),
          p: (props) => <p className="mb-2 leading-relaxed" {...props} />,
          ul: (props) => <ul className="mb-2 list-disc pl-5" {...props} />,
          ol: (props) => <ol className="mb-2 list-decimal pl-5" {...props} />,
          li: (props) => <li className="mb-0.5" {...props} />,
          code: (props) => (
            <code
              className="rounded bg-muted px-1 py-0.5 font-mono text-xs text-foreground"
              {...props}
            />
          ),
          pre: (props) => (
            <pre
              className="mb-2 overflow-auto rounded-md bg-muted/60 p-2 font-mono text-xs"
              {...props}
            />
          ),
          blockquote: (props) => (
            <blockquote className="mb-2 border-l-2 border-border pl-3 italic" {...props} />
          ),
        }}
      >
        {content}
      </Markdown>
    </div>
  );
}

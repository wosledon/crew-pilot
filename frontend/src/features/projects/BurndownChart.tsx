import { useTranslation } from "react-i18next";
import type { Burndown } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/States";

/** 燃尽图（按工时）：剩余工时 vs 理想线，纯 SVG 自绘 */
export function BurndownChart({ burndown }: { burndown: Burndown }) {
  const { t } = useTranslation("iterations");

  if (burndown.points.length === 0) {
    return <EmptyState>{t("iterations:burndownEmpty")}</EmptyState>;
  }

  const width = 640;
  const height = 220;
  const padding = 32;
  const maxHours = Math.max(
    burndown.totalEstimatedHours,
    ...burndown.points.map((point) => Math.max(point.remainingHours, point.idealHours)),
    1,
  );

  const x = (index: number) =>
    padding +
    (index * (width - padding * 2)) / Math.max(1, burndown.points.length - 1);
  const y = (hours: number) =>
    height - padding - (hours / maxHours) * (height - padding * 2);

  const toPath = (picker: (point: Burndown["points"][number]) => number) =>
    burndown.points
      .map((point, index) => `${index === 0 ? "M" : "L"}${x(index)},${y(picker(point))}`)
      .join(" ");

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("iterations:burndown")}</CardTitle>
      </CardHeader>
      <CardContent>
        <svg
          viewBox={`0 0 ${width} ${height}`}
          className="h-56 w-full"
          role="img"
          aria-label={t("iterations:burndown")}
        >
          {/* 坐标轴 */}
          <line
            x1={padding}
            y1={height - padding}
            x2={width - padding}
            y2={height - padding}
            stroke="var(--border)"
          />
          <line
            x1={padding}
            y1={padding}
            x2={padding}
            y2={height - padding}
            stroke="var(--border)"
          />
          {/* 理想线（虚线） */}
          <path
            d={toPath((point) => point.idealHours)}
            fill="none"
            stroke="var(--muted-foreground)"
            strokeDasharray="5 4"
            strokeWidth={1.5}
          />
          {/* 实际剩余 */}
          <path
            d={toPath((point) => point.remainingHours)}
            fill="none"
            stroke="var(--primary)"
            strokeWidth={2}
          />
          {burndown.points.map((point, index) => (
            <circle
              key={point.date}
              cx={x(index)}
              cy={y(point.remainingHours)}
              r={2.5}
              fill="var(--primary)"
            />
          ))}
          <text x={padding} y={padding - 10} fontSize={11} fill="var(--muted-foreground)">
            {maxHours}h
          </text>
          <text
            x={width - padding}
            y={height - padding + 16}
            fontSize={11}
            textAnchor="end"
            fill="var(--muted-foreground)"
          >
            {burndown.points[burndown.points.length - 1].date}
          </text>
        </svg>
        <div className="mt-2 flex gap-4 text-xs text-muted-foreground">
          <span className="flex items-center gap-1.5">
            <span className="inline-block h-0.5 w-4 bg-primary" />
            {t("iterations:burndownRemaining")}
          </span>
          <span className="flex items-center gap-1.5">
            <span className="inline-block h-0.5 w-4 border-t border-dashed border-muted-foreground" />
            {t("iterations:burndownIdeal")}
          </span>
        </div>
      </CardContent>
    </Card>
  );
}

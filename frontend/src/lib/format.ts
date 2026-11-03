/** 日期 / 数字 / 货币格式化：统一走 Intl API，不手写格式 */

const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: "short",
  timeStyle: "short",
});

export function formatDateTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : dateTimeFormatter.format(date);
}

export function formatHours(value: number | null): string {
  return value === null ? "—" : `${value}h`;
}

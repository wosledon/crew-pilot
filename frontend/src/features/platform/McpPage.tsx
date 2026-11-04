import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { useConfirm } from "@/components/ui/ConfirmDialog";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { EmptyState, ErrorState, LoadingState } from "@/components/ui/States";
import { toast } from "@/components/ui/Toast";
import type { McpServer } from "@/lib/types";
import {
  useCreateMcpServer,
  useDeleteMcpServer,
  useMcpServers,
  useTestMcpServer,
  useUpdateMcpServer,
} from "./api";

interface McpForm {
  id?: string;
  name: string;
  endpoint: string;
  transport: string;
  authToken: string;
  enabled: boolean;
}

const emptyForm: McpForm = {
  name: "",
  endpoint: "",
  transport: "http",
  authToken: "",
  enabled: true,
};

export function McpPage() {
  const { t } = useTranslation(["mcp", "common"]);
  const servers = useMcpServers();
  const createServer = useCreateMcpServer();
  const updateServer = useUpdateMcpServer();
  const deleteServer = useDeleteMcpServer();
  const testServer = useTestMcpServer();
  const confirm = useConfirm();
  const [form, setForm] = useState<McpForm | null>(null);
  const [testMessages, setTestMessages] = useState<Record<string, string>>({});

  const errorMessage = (error: unknown) =>
    error instanceof Error ? error.message : t("common:error");

  const handleSave = async () => {
    if (form === null) {
      return;
    }
    try {
      if (form.id === undefined) {
        await createServer.mutateAsync({
          name: form.name.trim(),
          endpoint: form.endpoint.trim(),
          transport: form.transport,
          authToken: form.authToken.trim() || undefined,
        });
        toast.success(t("mcp:created"));
      } else {
        await updateServer.mutateAsync({
          id: form.id,
          name: form.name.trim(),
          endpoint: form.endpoint.trim(),
          transport: form.transport,
          authToken: form.authToken.trim() || undefined,
          enabled: form.enabled,
        });
        toast.success(t("mcp:updated"));
      }
      setForm(null);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleTest = async (server: McpServer) => {
    try {
      const result = await testServer.mutateAsync(server.id);
      setTestMessages((previous) => ({ ...previous, [server.id]: result.message }));
      if (result.ok) {
        toast.success(t("mcp:testOk"));
      } else {
        toast.error(t("mcp:testFailed"));
      }
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async (server: McpServer) => {
    const approved = await confirm({
      title: t("common:delete"),
      description: t("mcp:deleteConfirm"),
      confirmText: t("common:delete"),
      destructive: true,
    });
    if (!approved) {
      return;
    }
    try {
      await deleteServer.mutateAsync(server.id);
      toast.success(t("mcp:deleted"));
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  if (servers.isPending) {
    return <LoadingState />;
  }

  if (servers.isError) {
    return <ErrorState onRetry={() => void servers.refetch()} />;
  }

  const items = servers.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-normal">{t("mcp:title")}</h1>
          <p className="text-sm text-muted-foreground">{t("mcp:description")}</p>
        </div>
        <Button onClick={() => setForm({ ...emptyForm })}>
          <Plus className="h-4 w-4" />
          {t("mcp:create")}
        </Button>
      </div>

      {items.length === 0 ? (
        <EmptyState>{t("mcp:empty")}</EmptyState>
      ) : (
        <div className="grid gap-4 grid-cols-[repeat(auto-fill,minmax(300px,1fr))]">
          {items.map((server) => (
            <Card key={server.id}>
              <CardHeader>
                <CardTitle className="flex items-center justify-between gap-2">
                  <span className="truncate">{server.name}</span>
                  <Badge tone={server.enabled ? "success" : "neutral"}>
                    {t(`mcp:transports.${server.transport}`)}
                  </Badge>
                </CardTitle>
                <p className="truncate text-xs text-muted-foreground">{server.endpoint}</p>
              </CardHeader>
              <CardContent className="space-y-3">
                <p className="text-xs text-muted-foreground">
                  {t("mcp:agentCount", { count: server.agentCount })}
                  {server.hasToken ? " · Token ✓" : ""}
                </p>
                {testMessages[server.id] ? (
                  <p className="text-xs text-muted-foreground">{testMessages[server.id]}</p>
                ) : null}
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" variant="outline" onClick={() => void handleTest(server)}>
                    {t("mcp:test")}
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      setForm({
                        id: server.id,
                        name: server.name,
                        endpoint: server.endpoint,
                        transport: server.transport,
                        authToken: "",
                        enabled: server.enabled,
                      })
                    }
                  >
                    <Pencil className="h-3.5 w-3.5" />
                    {t("common:save")}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={t("common:delete")}
                    onClick={() => void handleDelete(server)}
                  >
                    <Trash2 className="h-3.5 w-3.5 text-destructive" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog
        open={form !== null}
        onOpenChange={(open) => {
          if (!open) {
            setForm(null);
          }
        }}
        title={form?.id === undefined ? t("mcp:createTitle") : t("mcp:editTitle")}
        closeLabel={t("common:cancel")}
      >
        {form !== null ? (
          <div className="space-y-4">
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("mcp:name")}</span>
              <Input
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("mcp:endpoint")}</span>
              <Input
                value={form.endpoint}
                onChange={(event) => setForm({ ...form, endpoint: event.target.value })}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("mcp:transport")}</span>
              <Select
                value={form.transport}
                onValueChange={(value) => setForm({ ...form, transport: value })}
                options={["http", "sse", "stdio"].map((transport) => ({
                  value: transport,
                  label: t(`mcp:transports.${transport}`),
                }))}
                ariaLabel={t("mcp:transport")}
              />
            </label>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("mcp:token")}</span>
              <Input
                type="password"
                value={form.authToken}
                onChange={(event) => setForm({ ...form, authToken: event.target.value })}
                placeholder={t("mcp:tokenKeep")}
              />
            </label>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setForm(null)}>
                {t("common:cancel")}
              </Button>
              <Button
                onClick={() => void handleSave()}
                disabled={
                  form.name.trim() === "" ||
                  form.endpoint.trim() === "" ||
                  createServer.isPending ||
                  updateServer.isPending
                }
              >
                {t("common:save")}
              </Button>
            </div>
          </div>
        ) : null}
      </Dialog>
    </div>
  );
}

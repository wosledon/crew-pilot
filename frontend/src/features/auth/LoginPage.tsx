import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { LogIn } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { Input } from "@/components/ui/Input";
import { toast } from "@/components/ui/Toast";
import { useLogin } from "./api";

/** 单用户登录页（仅在配置了 Auth:Password 时出现）。 */
export function LoginPage() {
  const { t } = useTranslation(["auth", "common"]);
  const login = useLogin();
  const [password, setPassword] = useState("");

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (password === "") {
      return;
    }
    try {
      await login.mutateAsync(password);
      toast.success(t("auth:loggedIn"));
      setPassword("");
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t("common:error"));
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-lg">{t("auth:title")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("auth:subtitle")}</p>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={(event) => void handleSubmit(event)}>
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("auth:password")}</span>
              <Input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder={t("auth:passwordPlaceholder")}
                autoFocus
              />
            </label>
            <Button type="submit" className="w-full" disabled={login.isPending || password === ""}>
              <LogIn className="h-4 w-4" />
              {t("auth:login")}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

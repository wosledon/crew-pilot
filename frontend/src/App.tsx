import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { ConfirmProvider } from "@/components/ui/ConfirmDialog";
import { TooltipProvider } from "@/components/ui/Tooltip";
import { Toaster } from "@/components/ui/Toast";
import { AppLayout } from "@/components/layout/AppLayout";
import { DashboardPage } from "@/features/dashboard/DashboardPage";
import { ProjectsPage } from "@/features/projects/ProjectsPage";
import { ProjectDetailPage } from "@/features/projects/ProjectDetailPage";
import { BoardPage } from "@/features/projects/BoardPage";
import { IterationsPage } from "@/features/projects/IterationsPage";
import { GanttPage } from "@/features/projects/GanttPage";
import { PrototypeStudioPage } from "@/features/prototype/PrototypeStudioPage";
import { ProvidersPage } from "@/features/agents/ProvidersPage";
import { AgentsPage } from "@/features/agents/AgentsPage";
import { BlockersPage } from "@/features/workflows/BlockersPage";
import { SkillsPage } from "@/features/platform/SkillsPage";
import { McpPage } from "@/features/platform/McpPage";
import { AuditPage } from "@/features/platform/AuditPage";
import { AuthGate } from "@/features/auth/AuthGate";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 5_000,
    },
  },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        <ConfirmProvider>
          <BrowserRouter>
            <AuthGate>
              <Routes>
                <Route element={<AppLayout />}>
                  <Route index element={<Navigate to="/dashboard" replace />} />
                  <Route path="dashboard" element={<DashboardPage />} />
                  <Route path="projects" element={<ProjectsPage />} />
                  <Route path="projects/:projectId" element={<ProjectDetailPage />} />
                  <Route path="projects/:projectId/board" element={<BoardPage />} />
                  <Route path="projects/:projectId/iterations" element={<IterationsPage />} />
                  <Route path="projects/:projectId/gantt" element={<GanttPage />} />
                  <Route path="projects/:projectId/blockers" element={<BlockersPage />} />
                  <Route path="providers" element={<ProvidersPage />} />
                  <Route path="agents" element={<AgentsPage />} />
                  <Route path="skills" element={<SkillsPage />} />
                  <Route path="mcp" element={<McpPage />} />
                  <Route path="audit" element={<AuditPage />} />
                  <Route path="prototypes" element={<PrototypeStudioPage />} />
                </Route>
              </Routes>
            </AuthGate>
          </BrowserRouter>
          <Toaster />
        </ConfirmProvider>
      </TooltipProvider>
    </QueryClientProvider>
  );
}

import { Navigate, Route, Routes } from "react-router-dom";
import { RequireAdmin } from "../../auth/RequireRoles";
import { AdminLayout } from "./AdminLayout";
import { AdminErrorBoundary } from "./AdminErrorBoundary";
import AdminDashboard from "./AdminDashboard";
import AdminUsers from "./AdminUsers";
import AdminAudit from "./AdminAudit";
import AdminAnalytics from "./AdminAnalytics";
import AdminAudioTasks from "./AdminAudioTasks";
import AdminContent from "./AdminContent";
import AdminPlaces from "./AdminPlaces";

function Section({ children }: { children: React.ReactNode }) {
  return <AdminErrorBoundary>{children}</AdminErrorBoundary>;
}

export default function AdminPortal() {
  return (
    <RequireAdmin>
      <Routes>
        <Route element={<AdminLayout />}>
          <Route
            index
            element={
              <Section>
                <AdminDashboard />
              </Section>
            }
          />
          <Route
            path="places"
            element={
              <Section>
                <AdminPlaces />
              </Section>
            }
          />
          <Route
            path="audio-tasks"
            element={
              <Section>
                <AdminAudioTasks />
              </Section>
            }
          />
          <Route
            path="content"
            element={
              <Section>
                <AdminContent />
              </Section>
            }
          />
          <Route
            path="users"
            element={
              <Section>
                <AdminUsers />
              </Section>
            }
          />
          <Route
            path="audit"
            element={
              <Section>
                <AdminAudit />
              </Section>
            }
          />
          <Route
            path="analytics"
            element={
              <Section>
                <AdminAnalytics />
              </Section>
            }
          />
          <Route path="*" element={<Navigate to="/admin" replace />} />
        </Route>
      </Routes>
    </RequireAdmin>
  );
}

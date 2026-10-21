import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "./AuthContext";
import { hasAnyRole } from "./session";

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { session } = useAuth();
  const location = useLocation();
  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }
  return <>{children}</>;
}

export function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { session } = useAuth();
  const location = useLocation();
  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }
  if (!hasAnyRole(session, ["admin", "super_admin"])) {
    return <Navigate to="/" replace />;
  }
  return <>{children}</>;
}

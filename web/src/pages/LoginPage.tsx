import { FormEvent, useState } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { isAdmin } from "../auth/session";

export default function LoginPage() {
  const { session, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const fromQuery = new URLSearchParams(location.search).get("from");
  const from =
    (location.state as { from?: string } | null)?.from ??
    (fromQuery && fromQuery.startsWith("/") ? fromQuery : null) ??
    "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (session) {
    if (from.startsWith("/admin") && isAdmin(session)) {
      return <Navigate to={from} replace />;
    }
    return <Navigate to={from} replace />;
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await login({ email, password });
      const adminOk = res.roles.some((r) => r === "admin" || r === "super_admin");
      if (from.startsWith("/admin") && !adminOk) {
        setError("This account does not have admin access.");
        return;
      }
      navigate(from, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="portal portal--auth">
      <div className="portal-card portal-card--narrow">
        <h1>Sign in</h1>
        <p className="portal-muted">TourismHcm admin portal</p>
        <form className="portal-form" onSubmit={(e) => void onSubmit(e)}>
          <label>
            Email
            <input
              type="email"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </label>
          <label>
            Password
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </label>
          {error && <p className="portal-error">{error}</p>}
          <button type="submit" className="btn btn--primary" disabled={busy}>
            {busy ? "Signing in…" : "Sign in"}
          </button>
        </form>
        <p className="portal-footer-links">
          <Link to="/">← Back to map</Link>
          {" · "}
          <Link to="/admin">Admin portal</Link>
        </p>
      </div>
    </div>
  );
}

import { useEffect, useState } from "react";
import { Link, NavLink, Outlet, useLocation } from "react-router-dom";
import { clearSession, loadSession } from "../../auth/session";

const NAV = [
  { to: "/admin", end: true, label: "Dashboard", hint: "Tổng quan" },
  { to: "/admin/places", label: "Places", hint: "Địa điểm" },
  { to: "/admin/audio-tasks", label: "Audio", hint: "TTS tasks" },
  { to: "/admin/content", label: "Content", hint: "Bản dịch" },
  { to: "/admin/users", label: "Users", hint: "Vai trò" },
  { to: "/admin/audit", label: "Audit", hint: "Nhật ký" },
  { to: "/admin/analytics", label: "Analytics", hint: "Số liệu" },
] as const;

export function AdminLayout() {
  const session = loadSession();
  const location = useLocation();
  const [navOpen, setNavOpen] = useState(false);

  useEffect(() => {
    setNavOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!navOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setNavOpen(false);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [navOpen]);

  const active = NAV.find((item) =>
    "end" in item && item.end
      ? location.pathname === item.to
      : location.pathname.startsWith(item.to),
  );

  return (
    <div className={"portal portal--admin" + (navOpen ? " portal--admin-nav-open" : "")}>
      <button
        type="button"
        className="admin-shell__scrim"
        aria-label="Đóng menu"
        tabIndex={navOpen ? 0 : -1}
        onClick={() => setNavOpen(false)}
      />

      <aside className="admin-shell__sidebar" id="admin-nav" aria-label="Admin navigation">
        <div className="admin-shell__brand">
          <span className="admin-shell__mark" aria-hidden>
            TH
          </span>
          <div>
            <strong>TourismHcm</strong>
            <span>Admin console</span>
          </div>
        </div>

        <nav className="admin-shell__nav">
          {NAV.map((item) => (
            <NavLink key={item.to} to={item.to} end={"end" in item ? item.end : false}>
              <span className="admin-shell__nav-label">{item.label}</span>
              <span className="admin-shell__nav-hint">{item.hint}</span>
            </NavLink>
          ))}
        </nav>

        <div className="admin-shell__sidebar-foot">
          <Link to="/" className="admin-shell__map-link">
            ← Public map
          </Link>
        </div>
      </aside>

      <div className="admin-shell__body">
        <header className="admin-shell__topbar">
          <div className="admin-shell__topbar-left">
            <button
              type="button"
              className="admin-shell__menu-btn"
              aria-expanded={navOpen}
              aria-controls="admin-nav"
              onClick={() => setNavOpen((v) => !v)}
            >
              <span />
              <span />
              <span />
              <span className="sr-only">Menu</span>
            </button>
            <div>
              <p className="admin-shell__crumb">Admin / {active?.label ?? "…"}</p>
              <h1>{active?.label ?? "Admin"}</h1>
            </div>
          </div>

          <div className="admin-shell__topbar-right">
            <div className="admin-shell__user">
              <span className="admin-shell__avatar" aria-hidden>
                {(session?.email ?? "A").slice(0, 1).toUpperCase()}
              </span>
              <div>
                <strong>Admin</strong>
                <span>{session?.email}</span>
              </div>
            </div>
            <button
              type="button"
              className="btn btn--ghost"
              onClick={() => {
                clearSession();
                window.location.href = "/login";
              }}
            >
              Sign out
            </button>
          </div>
        </header>

        <main className="admin-shell__main">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

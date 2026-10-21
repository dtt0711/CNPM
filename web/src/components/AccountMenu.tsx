import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "../auth/AuthContext";

function canHover(): boolean {
  return typeof window !== "undefined" && window.matchMedia("(hover: hover) and (pointer: fine)").matches;
}

export function AccountMenu() {
  const { t } = useTranslation();
  const { session, isAdmin, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const closeTimer = useRef<number>(0);

  useEffect(() => {
    if (!open) return;
    const onPointer = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("pointerdown", onPointer);
    window.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  useEffect(() => {
    return () => window.clearTimeout(closeTimer.current);
  }, []);

  if (!session) {
    return (
      <Link className="portal-link" to="/login">
        {t("account.signIn")}
      </Link>
    );
  }

  const showNow = () => {
    window.clearTimeout(closeTimer.current);
    setOpen(true);
  };

  const hideSoon = () => {
    window.clearTimeout(closeTimer.current);
    closeTimer.current = window.setTimeout(() => setOpen(false), 160);
  };

  return (
    <div
      className={"account-menu" + (open ? " account-menu--open" : "")}
      ref={rootRef}
      onMouseEnter={() => {
        if (canHover()) showNow();
      }}
      onMouseLeave={() => {
        if (canHover()) hideSoon();
      }}
    >
      <button
        type="button"
        className="account-menu__trigger"
        aria-expanded={open}
        aria-haspopup="menu"
        onClick={() => {
          // Desktop hover already opens the menu; a following click must not toggle it shut.
          if (canHover()) {
            showNow();
            return;
          }
          setOpen((value) => !value);
        }}
      >
        <span className="account-menu__email-text">{session.email}</span>
        <span className="account-menu__caret" aria-hidden>
          ▾
        </span>
      </button>
      {open ? (
        <div className="account-menu__pop" role="menu">
          <p className="account-menu__who">{session.email}</p>
          {isAdmin ? (
            <Link
              className="account-menu__item"
              role="menuitem"
              to="/admin"
              onClick={() => setOpen(false)}
            >
              {t("account.admin")}
            </Link>
          ) : null}
          <button
            type="button"
            className="account-menu__item account-menu__item--danger"
            role="menuitem"
            onClick={() => {
              setOpen(false);
              void logout();
            }}
          >
            {t("account.signOut")}
          </button>
        </div>
      ) : null}
    </div>
  );
}

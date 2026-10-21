import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

const ONLINE_HIDE_MS = 4500;

export function NetworkToast({ online }: { online: boolean | null }) {
  const { t } = useTranslation();
  const [kind, setKind] = useState<"offline" | "online" | null>(null);
  const baseline = useRef<boolean | null>(null);

  useEffect(() => {
    if (online === null) return;

    if (baseline.current === null) {
      baseline.current = online;
      if (!online) setKind("offline");
      return;
    }

    if (baseline.current === online) return;
    baseline.current = online;
    setKind(online ? "online" : "offline");
  }, [online]);

  useEffect(() => {
    if (kind !== "online") return;
    const timer = window.setTimeout(() => setKind(null), ONLINE_HIDE_MS);
    return () => window.clearTimeout(timer);
  }, [kind]);

  if (!kind) return null;

  return (
    <div
      className={"app-net-toast app-net-toast--" + kind}
      role="status"
      aria-live="polite"
    >
      <span className="app-net-toast__dot" aria-hidden />
      <p>
        {kind === "offline" ? t("network.offline") : t("network.online")}
      </p>
      <button
        type="button"
        className="app-net-toast__close"
        onClick={() => setKind(null)}
        aria-label={t("network.dismiss")}
      >
        ×
      </button>
    </div>
  );
}

import { useEffect } from "react";
import { api } from "../api/client";
import { getOrCreateClientId } from "../api/clientId";

const INTERVAL_MS = 30_000;

/** Anonymous last-seen ping so admin can count devices currently on the site. */
export function PresenceHeartbeat() {
  useEffect(() => {
    let cancelled = false;

    const ping = () => {
      if (cancelled || typeof document !== "undefined" && document.visibilityState === "hidden") {
        return;
      }
      void api.recordPresence({ deviceId: getOrCreateClientId() }).catch(() => {
        // Presence is best-effort; a failed ping must not break the app.
      });
    };

    ping();
    const timer = window.setInterval(ping, INTERVAL_MS);
    const onVisible = () => {
      if (document.visibilityState === "visible") ping();
    };
    document.addEventListener("visibilitychange", onVisible);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, []);

  return null;
}

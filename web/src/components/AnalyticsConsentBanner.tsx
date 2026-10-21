import { useEffect, useState } from "react";
import { api } from "../api/client";
import { getOrCreateClientId } from "../api/clientId";

const CONSENT_KEY = "tourismhcm.analyticsConsent";

function readConsent(): boolean | null {
  try {
    const raw = window.localStorage.getItem(CONSENT_KEY);
    if (raw === "granted") return true;
    if (raw === "denied") return false;
  } catch {
    // ignore
  }
  return null;
}

function writeConsent(granted: boolean): void {
  try {
    window.localStorage.setItem(CONSENT_KEY, granted ? "granted" : "denied");
  } catch {
    // ignore
  }
}

function sessionId(): string {
  const key = "tourismhcm.analyticsSession";
  try {
    const existing = window.sessionStorage.getItem(key);
    if (existing) return existing;
    const id = crypto.randomUUID();
    window.sessionStorage.setItem(key, id);
    return id;
  } catch {
    return crypto.randomUUID();
  }
}

export function AnalyticsConsentBanner() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    setVisible(readConsent() === null);
  }, []);

  const sendConsent = async (consent: boolean) => {
    writeConsent(consent);
    setVisible(false);
    if (!consent) return;
    try {
      await api.collect({
        deviceId: getOrCreateClientId(),
        sessionId: sessionId(),
        consent: true,
        events: [
          {
            eventName: "consent_granted",
            occurredAt: new Date().toISOString(),
          },
        ],
      });
    } catch {
      // non-blocking
    }
  };

  if (!visible) return null;

  return (
    <div className="app__banner app__banner--consent" role="dialog" aria-label="Analytics consent">
      <span>
        Help improve TourismHcm by sharing anonymous usage analytics. You can change this later in
        browser storage.
      </span>
      <div className="app__banner-actions">
        <button type="button" className="app__banner-action" onClick={() => void sendConsent(true)}>
          Allow
        </button>
        <button type="button" className="btn btn--ghost" onClick={() => void sendConsent(false)}>
          Decline
        </button>
      </div>
    </div>
  );
}

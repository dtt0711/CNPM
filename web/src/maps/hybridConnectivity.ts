/**
 * Hybrid basemap: OSM tiles need real Internet. `navigator.onLine` stays true
 * when Wi‑Fi is up but WAN is down, so we also ping an OSM tile as an <img>
 * (avoids CORS; MapLibre loads tiles the same way).
 */

const OSM_PROBE = "https://tile.openstreetmap.org/0/0/0.png";
const PROBE_MS = 4000;
const INTERVAL_MS = 30000;

export function probeInternetMapTiles(timeoutMs = PROBE_MS): Promise<boolean> {
  if (typeof window === "undefined") return Promise.resolve(true);
  if (typeof navigator !== "undefined" && navigator.onLine === false) {
    return Promise.resolve(false);
  }

  return new Promise((resolve) => {
    const img = new Image();
    let settled = false;
    const finish = (ok: boolean) => {
      if (settled) return;
      settled = true;
      window.clearTimeout(timer);
      img.onload = null;
      img.onerror = null;
      img.src = "";
      resolve(ok);
    };

    const timer = window.setTimeout(() => finish(false), timeoutMs);
    img.referrerPolicy = "no-referrer";
    img.onload = () => finish(true);
    img.onerror = () => finish(false);
    img.src = `${OSM_PROBE}?_=${Date.now()}`;
  });
}

export function watchHybridConnectivity(handlers: {
  onInternetLost: () => void;
  onInternetRestored: () => void;
}): () => void {
  let stopped = false;
  let inFlight = false;
  let lastOnline: boolean | null = null;

  const run = async (forceOffline = false) => {
    if (stopped || inFlight) return;
    inFlight = true;
    try {
      const online = forceOffline ? false : await probeInternetMapTiles();
      if (stopped) return;
      if (lastOnline === online) return;
      lastOnline = online;
      if (online) handlers.onInternetRestored();
      else handlers.onInternetLost();
    } finally {
      inFlight = false;
    }
  };

  const onOffline = () => {
    lastOnline = false;
    handlers.onInternetLost();
  };
  const onOnline = () => {
    void run();
  };

  window.addEventListener("offline", onOffline);
  window.addEventListener("online", onOnline);
  document.addEventListener("visibilitychange", onOnline);

  void run(typeof navigator !== "undefined" && navigator.onLine === false);
  const interval = window.setInterval(() => void run(), INTERVAL_MS);

  return () => {
    stopped = true;
    window.clearInterval(interval);
    window.removeEventListener("offline", onOffline);
    window.removeEventListener("online", onOnline);
    document.removeEventListener("visibilitychange", onOnline);
  };
}

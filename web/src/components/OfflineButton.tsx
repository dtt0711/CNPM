import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { api, apiAudioUrl } from "../api/client";

export interface OfflineButtonProps {
  lang: string;
}

type DownloadState = "idle" | "downloading" | "success" | "error";

async function sha256Hex(buffer: ArrayBuffer): Promise<string> {
  const hash = await crypto.subtle.digest("SHA-256", buffer);
  return [...new Uint8Array(hash)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

/**
 * Offline pack: places+text+audio with SHA-256 verify when pack-manifest is available,
 * plus map pack manifest cache.
 */
export function OfflineButton({ lang }: OfflineButtonProps) {
  const { t } = useTranslation();
  const [state, setState] = useState<DownloadState>("idle");
  const [placeCount, setPlaceCount] = useState(0);
  const [verified, setVerified] = useState(0);
  const [mapPacks, setMapPacks] = useState<string[]>([]);
  const [mapCached, setMapCached] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void api
      .offlineOptions()
      .then((opts) => {
        if (!cancelled) setMapPacks(opts.availablePacks);
      })
      .catch(() => {
        if (!cancelled) setMapPacks([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const cacheMapManifest = async (packId: string) => {
    const manifest = await api.offlineManifest(packId);
    if ("caches" in window) {
      const cache = await caches.open("tourismhcm-map-manifests");
      const url = `/api/v1/maps/offline/manifest/${encodeURIComponent(packId)}`;
      await cache.put(
        url,
        new Response(JSON.stringify(manifest), {
          headers: { "Content-Type": "application/json" },
        }),
      );
    }
    setMapCached(true);
  };

  const handleDownload = async () => {
    setState("downloading");
    setVerified(0);
    try {
      const manifest = await api.getOfflinePack([lang]);
      let okCount = 0;

      // Prefer SHA-verified audio pack when available.
      try {
        const pack = await api.getAudioPackManifest(lang);
        if ("caches" in window && pack.files?.length) {
          const cache = await caches.open(`tourismhcm-audio-cache-${lang}`);
          for (const file of pack.files) {
            const url = apiAudioUrl(file.path.startsWith("/audio/") ? file.path : `/audio/${file.path}`);
            const res = await fetch(url, { mode: "cors", credentials: "omit" });
            if (!res.ok) continue;
            const buf = await res.arrayBuffer();
            if (file.sha256) {
              const digest = await sha256Hex(buf);
              if (digest.toLowerCase() !== file.sha256.toLowerCase()) continue;
            }
            await cache.put(url, new Response(buf, { headers: { "Content-Type": "audio/mpeg" } }));
            okCount += 1;
          }
        }
      } catch {
        const audioUrls = manifest.places
          .map((p) => p.audioUrls[lang])
          .filter((url): url is string => Boolean(url));
        await Promise.allSettled(
          audioUrls.map((url) => fetch(apiAudioUrl(url), { mode: "cors", credentials: "omit" })),
        );
      }

      try {
        window.localStorage.setItem(
          `tourismhcm.offlinePack.${lang}`,
          JSON.stringify({ savedAt: Date.now(), manifest, verifiedAudio: okCount }),
        );
      } catch {
        /* storage full */
      }

      const packId = mapPacks[0] ?? "current";
      try {
        await cacheMapManifest(packId);
      } catch {
        /* optional */
      }

      setVerified(okCount);
      setPlaceCount(manifest.places.length);
      setState("success");
    } catch {
      setState("error");
    }
  };

  return (
    <div className="offline-button">
      <button
        type="button"
        className="btn btn--accent"
        onClick={() => void handleDownload()}
        disabled={state === "downloading"}
      >
        {state === "downloading" ? t("offline.downloading") : t("offline.download")}
      </button>
      {state === "success" && (
        <p className="offline-button__status">
          {t("offline.success", { count: placeCount, lang })}
          {verified > 0 ? ` · ${verified} audio SHA-ok` : ""}
        </p>
      )}
      {state === "error" && (
        <p className="offline-button__status offline-button__status--error">
          {t("offline.error")}
        </p>
      )}
    </div>
  );
}

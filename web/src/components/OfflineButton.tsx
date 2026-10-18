import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";

export interface OfflineButtonProps {
  lang: string;
}

type DownloadState = "idle" | "downloading" | "success" | "error";

/**
 * "Tải gói offline" / "Download offline pack" button.
 * Calls GET /api/v1/offline/pack?langs={lang} and stores the manifest (plus
 * any audio URLs it references) so the service worker's runtime caches pick
 * them up for offline use.
 */
export function OfflineButton({ lang }: OfflineButtonProps) {
  const { t } = useTranslation();
  const [state, setState] = useState<DownloadState>("idle");
  const [placeCount, setPlaceCount] = useState(0);

  const handleDownload = async () => {
    setState("downloading");
    try {
      const manifest = await api.getOfflinePack([lang]);

      // Prime the browser HTTP cache (picked up by the service worker's
      // CacheFirst runtime caching rule for audio URLs) by prefetching them.
      const audioUrls = manifest.places
        .map((p) => p.audioUrls[lang])
        .filter((url): url is string => Boolean(url));

      await Promise.allSettled(
        audioUrls.map((url) => fetch(url, { mode: "cors", credentials: "omit" })),
      );

      try {
        window.localStorage.setItem(
          `tourismhcm.offlinePack.${lang}`,
          JSON.stringify({ savedAt: Date.now(), manifest }),
        );
      } catch {
        // Storage might be full/unavailable - the HTTP cache prefetch above still helps.
      }

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
        onClick={handleDownload}
        disabled={state === "downloading"}
      >
        {state === "downloading" ? t("offline.downloading") : t("offline.download")}
      </button>
      {state === "success" && (
        <p className="offline-button__status">
          {t("offline.success", { count: placeCount, lang })}
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

import type { StyleSpecification } from "maplibre-gl";
import { api, staticAssetUrl } from "../api/client";
import { OFFLINE_PMTILES_PATH, OFFLINE_STYLE_PATH, type MapMode } from "./mapConfig";
import { ensurePmtilesProtocol } from "./pmtilesProtocol";

/** OSM raster — no API key; good street context for HCMC demos. */
export const CLOUD_OSM_STYLE: StyleSpecification = {
  version: 8,
  sources: {
    osm: {
      type: "raster",
      tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
      tileSize: 256,
      attribution: "© OpenStreetMap contributors",
      maxzoom: 19,
    },
  },
  layers: [{ id: "osm", type: "raster", source: "osm" }],
};

export type OfflineStyleResult = {
  style: string | StyleSpecification;
  /** True when local hcm.pmtiles is reachable. */
  packReady: boolean;
};

export function resolveCloudMapStyle(): string | StyleSpecification {
  const configured = (import.meta.env.VITE_MAP_TILE_URL as string | undefined)?.trim();
  if (!configured) return CLOUD_OSM_STYLE;

  if (configured.includes("style") || configured.endsWith(".json")) {
    return configured;
  }

  if (configured.includes("{z}") && configured.includes("{x}")) {
    return {
      version: 8,
      sources: {
        osm: {
          type: "raster",
          tiles: [configured],
          tileSize: 256,
          attribution: "© OpenStreetMap",
        },
      },
      layers: [{ id: "osm", type: "raster", source: "osm" }],
    };
  }

  return configured;
}

export function isBrowserOnline(): boolean {
  return typeof navigator === "undefined" || navigator.onLine !== false;
}

let cachedOfflineStyle: StyleSpecification | null = null;

async function probeUrl(url: string): Promise<boolean> {
  try {
    const head = await fetch(url, { method: "HEAD", cache: "no-store" });
    if (head.ok) return true;
    if (head.status === 404) return false;
    const ranged = await fetch(url, {
      method: "GET",
      headers: { Range: "bytes=0-15" },
      cache: "no-store",
    });
    return ranged.ok || ranged.status === 206;
  } catch {
    return false;
  }
}

function absolutizePmtilesUrls(style: StyleSpecification): StyleSpecification {
  const sources = { ...style.sources };
  for (const [key, source] of Object.entries(sources)) {
    if (!source || typeof source !== "object" || !("url" in source)) continue;
    const url = String((source as { url?: string }).url ?? "");
    if (!url.startsWith("pmtiles://")) continue;

    const rest = url.slice("pmtiles://".length);
    // Already absolute http(s)
    if (rest.startsWith("http://") || rest.startsWith("https://")) continue;

    const path = rest.startsWith("/") ? rest : `/${rest}`;
    const absolute = staticAssetUrl(path);
    sources[key] = { ...source, url: `pmtiles://${absolute}` };
  }

  let glyphs = style.glyphs;
  if (glyphs && glyphs.startsWith("/")) {
    glyphs = staticAssetUrl(glyphs);
  }
  return { ...style, sources, glyphs };
}

/**
 * Offline style: use local PMTiles pack when present; otherwise fall back to OSM
 * so the map is never a blank beige canvas (repo ships a stub without hcm.pmtiles).
 */
export async function resolveOfflineMapStyle(): Promise<OfflineStyleResult> {
  ensurePmtilesProtocol();

  if (isBrowserOnline()) {
    try {
      const options = await api.offlineOptions();
      const packId = options.availablePacks[0] ?? "current";
      await api.offlineManifest(packId);
    } catch {
      // manifest optional
    }
  }

  const pmtilesUrl = staticAssetUrl(OFFLINE_PMTILES_PATH);
  const packReady = await probeUrl(pmtilesUrl);
  if (!packReady) {
    return { style: CLOUD_OSM_STYLE, packReady: false };
  }

  try {
    const styleUrl = staticAssetUrl(OFFLINE_STYLE_PATH);
    const res = await fetch(styleUrl, { cache: "no-store" });
    if (res.ok) {
      const raw = (await res.json()) as StyleSpecification;
      const style = absolutizePmtilesUrls(raw);
      cachedOfflineStyle = style;
      return { style, packReady: true };
    }
  } catch {
    // use cache below
  }

  if (cachedOfflineStyle) {
    return { style: cachedOfflineStyle, packReady: true };
  }

  return { style: CLOUD_OSM_STYLE, packReady: false };
}

/** @deprecated Prefer resolveOfflineMapStyle() for pack status. */
export async function resolveOfflineMapStyleUrl(): Promise<string | StyleSpecification> {
  const result = await resolveOfflineMapStyle();
  return result.style;
}

export async function resolveInitialMapStyle(
  mode: MapMode,
): Promise<{ style: string | StyleSpecification; offlinePackReady: boolean | null }> {
  if (mode === "offline" || (mode === "hybrid" && !isBrowserOnline())) {
    const result = await resolveOfflineMapStyle();
    return { style: result.style, offlinePackReady: result.packReady };
  }
  return { style: resolveCloudMapStyle(), offlinePackReady: null };
}

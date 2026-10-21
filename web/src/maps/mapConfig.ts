export type MapMode = "cloud" | "offline" | "hybrid";

const STORAGE_KEY = "tourismhcm.mapMode";

export const DEFAULT_MAP_MODE: MapMode = "hybrid";

export function loadMapMode(): MapMode {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw === "cloud" || raw === "offline" || raw === "hybrid") return raw;
  } catch {
    // ignore
  }
  return DEFAULT_MAP_MODE;
}

export function saveMapMode(mode: MapMode): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, mode);
  } catch {
    // ignore
  }
}

export const OFFLINE_STYLE_PATH = "/static/maps/styles/hcm-offline.json";
export const OFFLINE_PMTILES_PATH = "/static/maps/packs/current/hcm.pmtiles";

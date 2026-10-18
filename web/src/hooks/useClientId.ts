import { useMemo } from "react";
import { getOrCreateClientId } from "../api/clientId";

/** Stable per-device client id, persisted in localStorage, used for geofence cooldown. */
export function useClientId(): string {
  return useMemo(() => getOrCreateClientId(), []);
}

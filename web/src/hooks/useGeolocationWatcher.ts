import { useCallback, useEffect, useRef, useState } from "react";

export type GeolocationStatus = "idle" | "watching" | "unsupported" | "denied" | "error";

export interface UseGeolocationWatcherResult {
  status: GeolocationStatus;
  lastPosition: { lat: number; lng: number } | null;
  start: () => void;
  stop: () => void;
}

/**
 * Thin wrapper around navigator.geolocation.watchPosition. Debounce/throttle
 * and all trigger logic live in GeofenceEngine - this hook only forwards raw
 * fixes to it via `onPosition`.
 */
export function useGeolocationWatcher(
  onPosition: (position: { lat: number; lng: number; timestamp: number }) => void,
): UseGeolocationWatcherResult {
  const [status, setStatus] = useState<GeolocationStatus>("idle");
  const [lastPosition, setLastPosition] = useState<{ lat: number; lng: number } | null>(null);
  const watchId = useRef<number | null>(null);
  const onPositionRef = useRef(onPosition);
  onPositionRef.current = onPosition;

  const stop = useCallback(() => {
    if (watchId.current !== null && typeof navigator !== "undefined") {
      navigator.geolocation.clearWatch(watchId.current);
      watchId.current = null;
    }
    setStatus("idle");
  }, []);

  const start = useCallback(() => {
    if (typeof navigator === "undefined" || !navigator.geolocation) {
      setStatus("unsupported");
      return;
    }

    watchId.current = navigator.geolocation.watchPosition(
      (pos) => {
        const next = { lat: pos.coords.latitude, lng: pos.coords.longitude, timestamp: pos.timestamp };
        setLastPosition({ lat: next.lat, lng: next.lng });
        setStatus("watching");
        onPositionRef.current(next);
      },
      (err) => {
        setStatus(err.code === err.PERMISSION_DENIED ? "denied" : "error");
      },
      { enableHighAccuracy: true, maximumAge: 2000, timeout: 15000 },
    );
    setStatus("watching");
  }, []);

  useEffect(() => stop, [stop]);

  return { status, lastPosition, start, stop };
}

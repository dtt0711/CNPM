import { useCallback, useEffect, useRef, useState } from "react";

export type GeolocationStatus =
  | "idle"
  | "locating"
  | "watching"
  | "unsupported"
  | "denied"
  | "error";

export interface UseGeolocationWatcherResult {
  status: GeolocationStatus;
  lastPosition: { lat: number; lng: number } | null;
  start: () => void;
  stop: () => void;
}

/**
 * Thin wrapper around navigator.geolocation. Debounce/throttle and trigger
 * logic live in GeofenceEngine — this hook only forwards raw fixes.
 */
export function useGeolocationWatcher(
  onPosition: (position: { lat: number; lng: number; timestamp: number }) => void,
): UseGeolocationWatcherResult {
  const [status, setStatus] = useState<GeolocationStatus>("idle");
  const [lastPosition, setLastPosition] = useState<{ lat: number; lng: number } | null>(null);
  const watchId = useRef<number | null>(null);
  const onPositionRef = useRef(onPosition);
  onPositionRef.current = onPosition;

  const emit = useCallback((lat: number, lng: number, timestamp: number) => {
    setLastPosition({ lat, lng });
    setStatus("watching");
    onPositionRef.current({ lat, lng, timestamp });
  }, []);

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

    setStatus("locating");

    // Immediate fix so the UI does not sit on "watching" with no marker.
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        emit(pos.coords.latitude, pos.coords.longitude, pos.timestamp);
      },
      (err) => {
        if (err.code === err.PERMISSION_DENIED) {
          setStatus("denied");
          return;
        }
        // Keep trying via watchPosition even if the first reading timed out.
        setStatus("locating");
      },
      { enableHighAccuracy: true, maximumAge: 5000, timeout: 12000 },
    );

    if (watchId.current !== null) {
      navigator.geolocation.clearWatch(watchId.current);
    }

    watchId.current = navigator.geolocation.watchPosition(
      (pos) => {
        emit(pos.coords.latitude, pos.coords.longitude, pos.timestamp);
      },
      (err) => {
        setStatus(err.code === err.PERMISSION_DENIED ? "denied" : "error");
      },
      { enableHighAccuracy: true, maximumAge: 2000, timeout: 15000 },
    );
  }, [emit]);

  useEffect(() => stop, [stop]);

  return { status, lastPosition, start, stop };
}

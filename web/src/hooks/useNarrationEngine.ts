import { useEffect, useRef, useState, useCallback } from "react";
import { api } from "../api/client";
import { useClientId } from "./useClientId";
import { useGeolocationWatcher, type GeolocationStatus } from "./useGeolocationWatcher";
import { GeofenceEngine, type NarrationEvent, type NarrationLike } from "../engine/GeofenceEngine";
import { AudioPlayer, type FallbackTier } from "../audio/AudioPlayer";

export interface UserPosition {
  lat: number;
  lng: number;
}

/** Independence Palace — handy for demos without going on-site. */
export const DEMO_POSITION: UserPosition = { lat: 10.7769, lng: 106.6959 };

export interface NarrationState {
  narration: NarrationLike | null;
  source: "geofence" | "on-demand" | null;
  tier: FallbackTier | "loading" | "idle";
  geofenceStatus: GeolocationStatus;
  userPosition: UserPosition | null;
  error: string | null;
  enableGeofence: () => void;
  disableGeofence: () => void;
  /** Feed a fake GPS fix (e.g. standing at a landmark) to trigger geofence logic. */
  simulatePosition: (position: UserPosition) => void;
  playPlace: (placeId: string) => void;
  stop: () => void;
}

/** Wires GeofenceEngine (position -> trigger decisions) to AudioPlayer (fallback playback). */
export function useNarrationEngine(lang: string): NarrationState {
  const clientId = useClientId();
  const [narration, setNarration] = useState<NarrationLike | null>(null);
  const [source, setSource] = useState<"geofence" | "on-demand" | null>(null);
  const [tier, setTier] = useState<FallbackTier | "loading" | "idle">("idle");
  const [error, setError] = useState<string | null>(null);
  const [userPosition, setUserPosition] = useState<UserPosition | null>(null);

  const playerRef = useRef<AudioPlayer | null>(null);
  const engineRef = useRef<GeofenceEngine | null>(null);

  if (!playerRef.current) {
    playerRef.current = new AudioPlayer((t) => setTier(t));
  }

  if (!engineRef.current) {
    engineRef.current = new GeofenceEngine({
      clientId,
      initialLang: lang,
      // Shorter waits for local demos (still respects server cooldown).
      debounceMs: 800,
      throttleMs: 1500,
      fetchTrigger: ({ lat, lng, lang: l, clientId: cid }) =>
        api.trigger({ latitude: lat, longitude: lng, lang: l, clientId: cid }),
      fetchNarration: async ({ placeId, lang: l }) => {
        const n = await api.getNarration(placeId, l);
        return {
          placeId: n.placeId,
          name: n.name,
          requestedLang: n.requestedLang,
          actualLang: n.actualLang,
          isFallback: n.isFallback,
          text: n.text,
          audio: n.audio,
        };
      },
      onNarration: (event: NarrationEvent) => {
        setNarration(event.narration);
        setSource(event.source);
        setTier("loading");
        setError(null);
        const speakLang = event.narration.actualLang || event.lang;
        playerRef.current
          ?.play({
            placeId: event.narration.placeId,
            lang: speakLang,
            text: event.narration.text,
            audio: event.narration.audio,
          })
          .catch(() => {
            setError("playback-error");
            setTier("none");
          });
      },
      onError: (err) => {
        setError(err instanceof Error ? err.message : "geofence-error");
      },
    });
  }

  useEffect(() => {
    engineRef.current?.setLanguage(lang);
  }, [lang]);

  useEffect(() => {
    return () => {
      engineRef.current?.dispose();
      playerRef.current?.stop();
    };
  }, []);

  const { status: geofenceStatus, start, stop: stopWatching } = useGeolocationWatcher((position) => {
    setUserPosition({ lat: position.lat, lng: position.lng });
    engineRef.current?.onPosition(position);
  });

  const simulatePosition = useCallback((position: UserPosition) => {
    setUserPosition(position);
    engineRef.current?.onPosition({ ...position, timestamp: Date.now() });
  }, []);

  const playPlace = useCallback((placeId: string) => {
    setTier("loading");
    setError(null);
    void engineRef.current?.requestOnDemand(placeId);
  }, []);

  const stop = useCallback(() => {
    playerRef.current?.stop();
    setTier("idle");
  }, []);

  return {
    narration,
    source,
    tier,
    geofenceStatus,
    userPosition,
    error,
    enableGeofence: start,
    disableGeofence: stopWatching,
    simulatePosition,
    playPlace,
    stop,
  };
}

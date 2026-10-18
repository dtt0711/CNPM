import { useEffect, useRef, useState, useCallback } from "react";
import { api } from "../api/client";
import { useClientId } from "./useClientId";
import { useGeolocationWatcher, type GeolocationStatus } from "./useGeolocationWatcher";
import { GeofenceEngine, type NarrationEvent, type NarrationLike } from "../engine/GeofenceEngine";
import { AudioPlayer, type FallbackTier } from "../audio/AudioPlayer";

export interface NarrationState {
  narration: NarrationLike | null;
  source: "geofence" | "on-demand" | null;
  tier: FallbackTier | "loading" | "idle";
  geofenceStatus: GeolocationStatus;
  error: string | null;
  enableGeofence: () => void;
  disableGeofence: () => void;
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

  const playerRef = useRef<AudioPlayer | null>(null);
  const engineRef = useRef<GeofenceEngine | null>(null);

  if (!playerRef.current) {
    playerRef.current = new AudioPlayer((t) => setTier(t));
  }

  if (!engineRef.current) {
    engineRef.current = new GeofenceEngine({
      clientId,
      initialLang: lang,
      fetchTrigger: ({ lat, lng, lang: l, clientId: cid }) =>
        api.trigger({ latitude: lat, longitude: lng, lang: l, clientId: cid }),
      fetchNarration: ({ placeId, lang: l }) => api.getNarration(placeId, l),
      onNarration: (event: NarrationEvent) => {
        setNarration(event.narration);
        setSource(event.source);
        setTier("loading");
        setError(null);
        playerRef.current?.play({ ...event.narration, lang: event.lang }).catch(() => {
          setError("playback-error");
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
    engineRef.current?.onPosition(position);
  });

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
    error,
    enableGeofence: start,
    disableGeofence: stopWatching,
    playPlace,
    stop,
  };
}

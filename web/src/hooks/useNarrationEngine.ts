import { useEffect, useRef, useState, useCallback } from "react";
import { api, apiAudioUrl } from "../api/client";
import type { TriggerResponse } from "../api/types";
import { useClientId } from "./useClientId";
import { useGeolocationWatcher, type GeolocationStatus } from "./useGeolocationWatcher";
import {
  GeofenceEngine,
  type NarrationEvent,
  type NarrationLike,
  type TriggerResultLike,
  type AudioInfoLike,
} from "../engine/GeofenceEngine";
import { AudioPlayer, type FallbackTier } from "../audio/AudioPlayer";

function resolveMediaUrl(audio: AudioInfoLike | null | undefined): string | null {
  if (!audio?.available || !audio.url) return null;
  return apiAudioUrl(audio.url);
}

export interface UserPosition {
  lat: number;
  lng: number;
}

export interface NearbyHint {
  placeId: string;
  name: string;
  distanceMeters: number;
  triggerRadiusMeters: number;
  /** True when GPS is inside the place's trigger radius. */
  inRange: boolean;
  /** True when in range but server cooldown blocked auto-play. */
  cooldownActive: boolean;
}

export interface NarrationState {
  narration: NarrationLike | null;
  source: "geofence" | "on-demand" | null;
  tier: FallbackTier | "loading" | "idle";
  /** Absolute URL for the visible HTML5 player (seekable MP3). */
  mediaUrl: string | null;
  geofenceStatus: GeolocationStatus;
  userPosition: UserPosition | null;
  nearbyHint: NearbyHint | null;
  error: string | null;
  enableGeofence: () => void;
  disableGeofence: () => void;
  playPlace: (placeId: string, opts?: { source?: "geofence" | "on-demand" }) => void;
  stop: () => void;
}

/** Must run inside a click handler so Chrome allows later speech / audio.play(). */
export function unlockSpeechSynthesis(): void {
  if (typeof window === "undefined") return;
  try {
    // Unlock HTMLAudioElement autoplay for the subsequent async trigger→play path.
    const warmAudio = new Audio(
      "data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQAAAAA=",
    );
    warmAudio.volume = 0.01;
    void warmAudio.play().then(() => {
      warmAudio.pause();
      warmAudio.src = "";
    }).catch(() => undefined);
  } catch {
    /* ignore */
  }
  if (!window.speechSynthesis) return;
  try {
    window.speechSynthesis.cancel();
    const warm = new SpeechSynthesisUtterance(" ");
    warm.volume = 0;
    warm.rate = 10;
    window.speechSynthesis.speak(warm);
    window.speechSynthesis.cancel();
    void window.speechSynthesis.getVoices();
  } catch {
    /* ignore */
  }
}

function mapTrigger(response: TriggerResponse | null): TriggerResultLike | null {
  if (!response) return null;
  const n = response.narration;
  return {
    placeId: response.placeId,
    name: response.name,
    distanceMeters: response.distanceMeters,
    triggered: response.triggered,
    narration: n
      ? {
          placeId: n.placeId || response.placeId,
          name: n.name || response.name,
          requestedLang: n.requestedLang,
          actualLang: n.actualLang,
          isFallback: n.isFallback,
          text: n.text ?? "",
          audio: n.audio ?? { available: false },
        }
      : null,
  };
}

export function useNarrationEngine(lang: string): NarrationState {
  const clientId = useClientId();
  const langRef = useRef(lang);
  langRef.current = lang;

  const [narration, setNarration] = useState<NarrationLike | null>(null);
  const [source, setSource] = useState<"geofence" | "on-demand" | null>(null);
  const [tier, setTier] = useState<FallbackTier | "loading" | "idle">("idle");
  const [mediaUrl, setMediaUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [userPosition, setUserPosition] = useState<UserPosition | null>(null);
  const [nearbyHint, setNearbyHint] = useState<NearbyHint | null>(null);

  const playerRef = useRef<AudioPlayer | null>(null);
  const engineRef = useRef<GeofenceEngine | null>(null);
  const playRequestIdRef = useRef(0);
  const lastTriggerMetaRef = useRef<{ cooldownActive: boolean; placeId: string } | null>(null);

  if (!playerRef.current) {
    playerRef.current = new AudioPlayer((t) => setTier(t));
  }

  if (!engineRef.current) {
    engineRef.current = new GeofenceEngine({
      clientId,
      initialLang: lang,
      debounceMs: 600,
      throttleMs: 1500,
      fetchTrigger: async ({ lat, lng, lang: l, clientId: cid }) => {
        const raw = await api.trigger({ latitude: lat, longitude: lng, lang: l, clientId: cid });
        lastTriggerMetaRef.current = raw
          ? { cooldownActive: raw.cooldownActive, placeId: raw.placeId }
          : null;
        return mapTrigger(raw);
      },
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
        setError(null);
        const url = resolveMediaUrl(event.narration.audio);
        if (url) {
          playerRef.current?.stop();
          setMediaUrl(url);
          setTier("ready-url");
          return;
        }
        setMediaUrl(null);
        const speakLang = event.narration.actualLang || event.lang;
        setTier("loading");
        const requestId = ++playRequestIdRef.current;
        playerRef.current
          ?.play({
            placeId: event.narration.placeId,
            lang: speakLang,
            text: event.narration.text,
            audio: event.narration.audio ?? { available: false },
          })
          .then((resultTier) => {
            if (playRequestIdRef.current !== requestId) return;
            if (resultTier === "none") {
              setError("playback-error");
            }
            setTier(resultTier === "none" ? "idle" : resultTier);
          })
          .catch(() => {
            if (playRequestIdRef.current !== requestId) return;
            setError("playback-error");
            setTier("idle");
          });
      },
      onError: (err) => {
        setError(err instanceof Error ? err.message : "geofence-error");
        setTier("idle");
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

  // Surface nearest place so location mode is never a silent black box.
  useEffect(() => {
    if (!userPosition) {
      setNearbyHint(null);
      return;
    }

    let cancelled = false;
    const controller = new AbortController();

    api
      .nearbyPlaces(
        {
          lat: userPosition.lat,
          lng: userPosition.lng,
          // 0 = no radius cap — always suggest the globally nearest place.
          radiusMeters: 0,
          limit: 1,
        },
        controller.signal,
      )
      .then((list) => {
        if (cancelled) return;
        const nearest = list[0];
        if (!nearest) {
          setNearbyHint(null);
          return;
        }
        const radius = nearest.triggerRadiusMeters > 0 ? nearest.triggerRadiusMeters : 30;
        const inRange = nearest.distanceMeters <= radius;
        const cooldownActive =
          inRange &&
          lastTriggerMetaRef.current?.placeId === nearest.id &&
          lastTriggerMetaRef.current.cooldownActive;
        setNearbyHint({
          placeId: nearest.id,
          name: nearest.name,
          distanceMeters: Math.round(nearest.distanceMeters),
          triggerRadiusMeters: radius,
          inRange,
          cooldownActive: Boolean(cooldownActive),
        });
      })
      .catch(() => {
        /* keep previous hint */
      });

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [userPosition, geofenceStatus, narration?.placeId]);

  const enableGeofence = useCallback(() => {
    unlockSpeechSynthesis();
    start();
  }, [start]);

  const disableGeofence = useCallback(() => {
    stopWatching();
    setNearbyHint(null);
  }, [stopWatching]);

  const playPlace = useCallback((placeId: string, opts?: { source?: "geofence" | "on-demand" }) => {
    unlockSpeechSynthesis();
    // Cancel any in-flight prepare/play before starting a new one.
    playRequestIdRef.current += 1;
    playerRef.current?.stop();
    setMediaUrl(null);

    const requestId = ++playRequestIdRef.current;
    const activeLang = langRef.current;
    const playSource = opts?.source ?? "on-demand";

    setNarration({
      placeId,
      name: undefined,
      requestedLang: activeLang,
      actualLang: activeLang,
      isFallback: false,
      text: "",
      audio: { available: false },
    });
    setTier("loading");
    setError(null);
    setSource(playSource);

    void (async () => {
      const stillCurrent = () => playRequestIdRef.current === requestId;

      try {
        // 1) Enqueue translate + TTS (surface failures instead of swallowing).
        let ttsQueued = false;
        try {
          await api.requestTts({ placeId, lang: activeLang });
          ttsQueued = true;
        } catch (err) {
          if (!stillCurrent()) return;
          // Continue — narration may still have cached text/audio.
          setError(err instanceof Error ? err.message : "tts-request-failed");
        }
        if (!stillCurrent()) return;

        // 2) Poll until target language text + audio are ready (or timeout).
        const deadline = Date.now() + 90_000;
        let n = await api.getNarration(placeId, activeLang);
        if (!stillCurrent()) return;

        const needsTargetLang = activeLang !== "vi";
        let textReadyAt: number | null = null;
        while (stillCurrent() && Date.now() < deadline) {
          const textOk = Boolean(n.text?.trim()) && (!needsTargetLang || !n.isFallback);
          const audioOk = Boolean(n.audio?.available && n.audio.url);
          if (textOk && audioOk) break;

          if (textOk) {
            if (textReadyAt === null) textReadyAt = Date.now();
            setNarration({
              placeId: n.placeId || placeId,
              name: n.name,
              requestedLang: n.requestedLang,
              actualLang: n.actualLang,
              isFallback: n.isFallback,
              text: n.text,
              audio: n.audio,
            });
            setTier("poll-task");
            // After ~20s with text but no MP3, fall through to Web Speech fallback.
            if (Date.now() - textReadyAt >= 20_000) break;
          }

          await new Promise((r) => setTimeout(r, 2000));
          if (!stillCurrent()) return;
          n = await api.getNarration(placeId, activeLang);
        }

        if (!stillCurrent()) return;

        const mapped: NarrationLike = {
          placeId: n.placeId || placeId,
          name: n.name,
          requestedLang: n.requestedLang,
          actualLang: n.actualLang,
          isFallback: n.isFallback,
          text: n.text,
          audio: n.audio,
        };
        setNarration(mapped);

        const url = resolveMediaUrl(n.audio);
        if (url) {
          // Prefer visible seekable HTML5 player over hidden Audio()/Web Speech.
          setMediaUrl(url);
          setTier("ready-url");
          setError(null);
          return;
        }

        if (!n.text?.trim()) {
          setError(ttsQueued ? "empty-narration" : "tts-request-failed");
          setTier("idle");
          return;
        }

        // No MP3 yet — fall back to Web Speech so the user still hears something.
        const speakLang = !n.isFallback ? activeLang : n.actualLang || activeLang;
        setTier("loading");
        const resultTier = await playerRef.current!.play({
          placeId: mapped.placeId,
          lang: speakLang,
          text: n.text,
          audio: n.audio ?? { available: false },
        });

        if (!stillCurrent()) return;

        if (resultTier === "none") {
          setError("playback-error");
          setTier("idle");
        } else {
          setTier(resultTier);
        }
      } catch (err) {
        if (!stillCurrent()) return;
        const message =
          err instanceof Error
            ? err.name === "AbortError"
              ? "timeout"
              : err.message
            : "playback-error";
        setError(message);
        setTier("idle");
      }
    })();
  }, []);

  const stop = useCallback(() => {
    playRequestIdRef.current += 1;
    playerRef.current?.stop();
    setMediaUrl(null);
    setTier("idle");
    setError(null);
    setNarration(null);
    setSource(null);
  }, []);

  return {
    narration,
    source,
    tier,
    mediaUrl,
    geofenceStatus,
    userPosition,
    nearbyHint,
    error,
    enableGeofence,
    disableGeofence,
    playPlace,
    stop,
  };
}

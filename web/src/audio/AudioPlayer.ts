// AudioPlayer
// -----------
// Plays narration audio with a 4-tier fallback chain:
//   1. ready audio URL          - narration.audio.available && .url -> play directly.
//   2. wait for task / poll     - audio still generating -> poll the task status
//                                  endpoint until it becomes ready (or times out).
//   3. POST /audio/tts (stream) - request on-the-fly synthesis; play the
//                                  resulting stream/URL if the backend returns one.
//   4. Web Speech API           - last resort: synthesize client-side with
//                                  `speechSynthesis` so the user always hears something.
//
// `resolveNarrationPlayback` is the pure, fully-injectable decision function
// used by the tests. `AudioPlayer` is a thin class that wires it up to real
// browser APIs (HTMLAudioElement, fetch via the api client, speechSynthesis)
// for use from React components/hooks.

export interface AudioInfoLike {
  available: boolean;
  url?: string | null;
  durationSec?: number;
  status?: string;
}

export interface NarrationLike {
  placeId: string;
  lang: string;
  text: string;
  audio: AudioInfoLike;
}

export type FallbackTier = "ready-url" | "poll-task" | "tts-stream" | "web-speech" | "none";

export interface PlaybackCancelToken {
  cancelled: boolean;
}

const PENDING_STATUSES = new Set(["pending", "running", "queued", "processing"]);

export interface AudioPlayerDeps {
  /** Plays an already-resolved audio URL/blob-URL to completion. */
  playUrl: (url: string) => Promise<void>;
  /** Polls backend task status for the given place+lang; returns updated audio info or null if unknown. */
  pollTaskStatus: (params: { placeId: string; lang: string }) => Promise<AudioInfoLike | null>;
  /** Requests on-the-fly TTS; resolves with a playable URL, or null if unavailable. */
  requestTtsStream: (params: {
    placeId: string;
    lang: string;
    text: string;
  }) => Promise<{ url: string } | null>;
  /** Client-side speech synthesis fallback. */
  speak: (text: string, lang: string) => Promise<void>;
  /** Awaitable delay, injectable so tests don't need real timers. */
  wait?: (ms: number) => Promise<void>;
  now?: () => number;
  pollIntervalMs?: number;
  pollTimeoutMs?: number;
}

const defaultWait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

function isPending(status?: string): boolean {
  return !!status && PENDING_STATUSES.has(status.toLowerCase());
}

function isTerminalFailure(status?: string): boolean {
  return !!status && status.toLowerCase() === "failed";
}

/** Polls `pollTaskStatus` until audio is ready, it fails, the timeout elapses, or cancellation. */
export async function pollUntilReady(
  narration: NarrationLike,
  deps: AudioPlayerDeps,
  token?: PlaybackCancelToken,
): Promise<AudioInfoLike | null> {
  const now = deps.now ?? Date.now;
  const wait = deps.wait ?? defaultWait;
  const intervalMs = deps.pollIntervalMs ?? 2000;
  const timeoutMs = deps.pollTimeoutMs ?? 20000;
  const start = now();

  while (now() - start < timeoutMs) {
    if (token?.cancelled) return null;

    const status = await deps.pollTaskStatus({ placeId: narration.placeId, lang: narration.lang });
    if (token?.cancelled) return null;

    if (status?.available && status.url) return status;
    if (isTerminalFailure(status?.status)) return null;

    await wait(intervalMs);
  }

  return null;
}

/**
 * Pure fallback-resolution logic: tries each tier in order and returns which
 * tier succeeded (or "none"). All I/O is injected via `deps`, so this is
 * fully unit-testable without a real DOM/audio element/network.
 */
export async function playNarrationWithFallback(
  narration: NarrationLike,
  deps: AudioPlayerDeps,
  token?: PlaybackCancelToken,
): Promise<FallbackTier> {
  // Tier 1: ready audio URL.
  if (narration.audio.available && narration.audio.url) {
    try {
      await deps.playUrl(narration.audio.url);
      return "ready-url";
    } catch {
      // fall through to the next tier.
    }
  }
  if (token?.cancelled) return "none";

  // Tier 2: wait for an in-progress generation task to finish.
  if (isPending(narration.audio.status)) {
    const polled = await pollUntilReady(narration, deps, token);
    if (token?.cancelled) return "none";
    if (polled?.url) {
      try {
        await deps.playUrl(polled.url);
        return "poll-task";
      } catch {
        // fall through.
      }
    }
  }
  if (token?.cancelled) return "none";

  // Tier 3: request on-demand synthesis (streamed or queued).
  try {
    const stream = await deps.requestTtsStream({
      placeId: narration.placeId,
      lang: narration.lang,
      text: narration.text,
    });
    if (token?.cancelled) return "none";
    if (stream?.url) {
      await deps.playUrl(stream.url);
      return "tts-stream";
    }
  } catch {
    // fall through.
  }
  if (token?.cancelled) return "none";

  // Tier 4: client-side Web Speech API, last resort.
  try {
    await deps.speak(narration.text, narration.lang);
    return "web-speech";
  } catch {
    return "none";
  }
}

// ---------------------------------------------------------------------------
// Browser-backed implementation used by the app at runtime.
// ---------------------------------------------------------------------------

import { api, apiAudioUrl } from "../api/client";

export class AudioPlayer {
  private audioEl: HTMLAudioElement | null = null;
  private token: PlaybackCancelToken = { cancelled: false };
  private utterance: SpeechSynthesisUtterance | null = null;

  constructor(private readonly onTierChange?: (tier: FallbackTier) => void) {}

  /** Cancels any in-flight playback/polling/speech and stops current audio. */
  stop(): void {
    this.token.cancelled = true;
    this.token = { cancelled: false };
    if (this.audioEl) {
      this.audioEl.pause();
      this.audioEl.src = "";
      this.audioEl = null;
    }
    if (typeof window !== "undefined" && window.speechSynthesis) {
      window.speechSynthesis.cancel();
    }
    this.utterance = null;
  }

  async play(narration: NarrationLike): Promise<FallbackTier> {
    this.stop();
    const token = this.token;

    const deps: AudioPlayerDeps = {
      playUrl: (url) => this.playUrl(url),
      pollTaskStatus: async ({ placeId, lang }) => {
        const task = await api.getAudioTaskStatus(placeId, lang);
        if (!task) return null;
        const status = task.status;
        if (status?.toLowerCase() === "done") {
          // Done tasks don't carry a URL directly; re-fetch narration text/audio.
          const refreshed = await api.getNarration(placeId, lang);
          return refreshed.audio;
        }
        return { available: false, status };
      },
      requestTtsStream: async ({ placeId, lang }) => {
        const result = await api.requestTts({ placeId, lang });
        if (result.kind === "stream") {
          return { url: URL.createObjectURL(result.blob) };
        }
        // Returned a queued task - poll briefly for it to complete.
        const info = await pollUntilReady(narration, deps, token);
        return info?.url ? { url: info.url } : null;
      },
      speak: (text, lang) => this.speak(text, lang),
    };

    const tier = await playNarrationWithFallback(narration, deps, token);
    this.onTierChange?.(tier);
    return tier;
  }

  private playUrl(url: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const el = new Audio(apiAudioUrl(url));
      this.audioEl = el;
      el.addEventListener("ended", () => resolve(), { once: true });
      el.addEventListener("error", () => reject(new Error("Audio playback failed")), { once: true });
      el.play().catch(reject);
    });
  }

  private speak(text: string, lang: string): Promise<void> {
    return new Promise((resolve, reject) => {
      if (typeof window === "undefined" || !window.speechSynthesis) {
        reject(new Error("Web Speech API unavailable"));
        return;
      }
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.lang = lang;
      utterance.onend = () => resolve();
      utterance.onerror = () => reject(new Error("Speech synthesis failed"));
      this.utterance = utterance;
      window.speechSynthesis.speak(utterance);
    });
  }
}

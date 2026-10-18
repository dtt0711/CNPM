// AudioPlayer
// -----------
// Plays narration with fallback:
//   1. ready server audio URL (real MP3 from Azure / prior generation)
//   2. Web Speech API when narration text is present (needed for Fake/no-key demos;
//      also recovers when silent/broken server audio would otherwise "succeed")
//   3. poll in-progress generation task
//   4. POST /audio/tts then play result
//
// Pure logic lives in `playNarrationWithFallback` for vitest; `AudioPlayer` wires browsers.

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
  playUrl: (url: string) => Promise<void>;
  pollTaskStatus: (params: { placeId: string; lang: string }) => Promise<AudioInfoLike | null>;
  requestTtsStream: (params: {
    placeId: string;
    lang: string;
    text: string;
  }) => Promise<{ url: string } | null>;
  speak: (text: string, lang: string) => Promise<void>;
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

/** Map app lang codes to BCP-47 tags browsers understand for speechSynthesis. */
export function toSpeechLocale(lang: string): string {
  const code = lang.trim().toLowerCase().split("-")[0];
  switch (code) {
    case "vi":
      return "vi-VN";
    case "en":
      return "en-US";
    case "ja":
      return "ja-JP";
    case "ko":
      return "ko-KR";
    case "zh":
      return "zh-CN";
    default:
      return lang || "vi-VN";
  }
}

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
 * Tries each playback tier. Web Speech is attempted early whenever text exists so
 * Development/Fake (silent MP3) demos still produce audible narration on a user click.
 */
export async function playNarrationWithFallback(
  narration: NarrationLike,
  deps: AudioPlayerDeps,
  token?: PlaybackCancelToken,
): Promise<FallbackTier> {
  // In Vite dev (and when VITE_PREFER_WEB_SPEECH=true), prefer Web Speech before
  // server MP3 — Fake TTS produces silent files that otherwise "play" with no sound.
  if (token?.cancelled) return "none";

  const preferWebSpeechFirst =
    (typeof import.meta !== "undefined" &&
      (Boolean(import.meta.env?.DEV) || import.meta.env?.VITE_PREFER_WEB_SPEECH === "true")) ||
    false;

  if (preferWebSpeechFirst && narration.text?.trim()) {
    try {
      await deps.speak(narration.text, toSpeechLocale(narration.lang));
      return "web-speech";
    } catch {
      // fall through to server audio tiers
    }
  }
  if (token?.cancelled) return "none";

  if (narration.audio.available && narration.audio.url) {
    try {
      await deps.playUrl(narration.audio.url);
      return "ready-url";
    } catch {
      // fall through
    }
  }
  if (token?.cancelled) return "none";

  if (!preferWebSpeechFirst && narration.text?.trim()) {
    try {
      await deps.speak(narration.text, toSpeechLocale(narration.lang));
      return "web-speech";
    } catch {
      // fall through to server TTS tiers
    }
  }
  if (token?.cancelled) return "none";

  if (isPending(narration.audio.status)) {
    const polled = await pollUntilReady(narration, deps, token);
    if (token?.cancelled) return "none";
    if (polled?.url) {
      try {
        await deps.playUrl(polled.url);
        return "poll-task";
      } catch {
        // fall through
      }
    }
  }
  if (token?.cancelled) return "none";

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
    // fall through
  }
  if (token?.cancelled) return "none";

  // Last retry of Web Speech if earlier attempt failed.
  if (narration.text?.trim()) {
    try {
      await deps.speak(narration.text, toSpeechLocale(narration.lang));
      return "web-speech";
    } catch {
      return "none";
    }
  }

  return "none";
}

import { api, apiAudioUrl } from "../api/client";

export class AudioPlayer {
  private audioEl: HTMLAudioElement | null = null;
  private token: PlaybackCancelToken = { cancelled: false };
  private utterance: SpeechSynthesisUtterance | null = null;

  constructor(private readonly onTierChange?: (tier: FallbackTier) => void) {}

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

      // Chrome often needs voices loaded asynchronously.
      const start = () => {
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = lang;
        const voices = window.speechSynthesis.getVoices();
        const match = voices.find((v) => v.lang.toLowerCase().startsWith(lang.toLowerCase().slice(0, 2)));
        if (match) utterance.voice = match;
        utterance.onend = () => resolve();
        utterance.onerror = () => reject(new Error("Speech synthesis failed"));
        this.utterance = utterance;
        window.speechSynthesis.speak(utterance);
      };

      const voices = window.speechSynthesis.getVoices();
      if (voices.length === 0) {
        window.speechSynthesis.onvoiceschanged = () => {
          window.speechSynthesis.onvoiceschanged = null;
          start();
        };
        // Fallback if event never fires
        setTimeout(start, 250);
      } else {
        start();
      }
    });
  }
}

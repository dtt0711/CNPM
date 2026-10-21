// GeofenceEngine
// --------------
// Pure TypeScript module (no DOM / React dependency) that turns a raw,
// noisy GPS position stream into narration playback decisions.
//
// Responsibilities:
//  - Debounce raw GPS updates (default 3s): only act once the position has
//    settled (no new fix) for `debounceMs`.
//  - Throttle geofence checks against the API (default 5s): never call the
//    trigger endpoint more than once per `throttleMs`, even if the debounced
//    position keeps changing. Uses trailing-edge throttling so the most
//    recent position is always eventually checked.
//  - Enforce a client-side cooldown (default 5 min) per place so the same
//    narration doesn't replay repeatedly while a visitor lingers nearby.
//  - Cancel stale in-flight results when the active language changes
//    mid-request (both for geofence-triggered and on-demand requests).
//
// The engine never touches `navigator.geolocation`, `fetch`, or timers
// directly beyond `setTimeout`/`clearTimeout` (which vitest can fake) - all
// I/O is injected, which keeps it fully unit-testable.

export interface GeoPosition {
  lat: number;
  lng: number;
  /** ms since epoch; defaults to Date.now() if omitted. */
  timestamp?: number;
}

export interface AudioInfoLike {
  available: boolean;
  url?: string | null;
  durationSec?: number;
  status?: string;
}

export interface NarrationLike {
  placeId: string;
  name?: string;
  requestedLang?: string;
  actualLang?: string;
  isFallback?: boolean;
  text: string;
  audio: AudioInfoLike;
}

export interface TriggerResultLike {
  placeId: string;
  name?: string;
  distanceMeters?: number;
  triggered: boolean;
  narration: NarrationLike | null;
}

export type NarrationSource = "geofence" | "on-demand";

export interface NarrationEvent {
  source: NarrationSource;
  placeId: string;
  lang: string;
  narration: NarrationLike;
}

export interface GeofenceEngineOptions {
  /** Stable per-device id sent to the backend for cooldown bookkeeping. */
  clientId: string;
  /** Initial active language (ISO code, e.g. "vi"). */
  initialLang?: string;
  /** Debounce window for raw GPS updates. Default 3000ms. */
  debounceMs?: number;
  /** Minimum spacing between geofence-check API calls. Default 5000ms. */
  throttleMs?: number;
  /** Per-place client-side cooldown before the same place can retrigger. Default 5 min. */
  cooldownMs?: number;
  /** Clock override, mainly for deterministic tests. Default Date.now. */
  now?: () => number;

  /** Called (post debounce+throttle) with the latest known position to evaluate geofences. */
  fetchTrigger: (params: {
    lat: number;
    lng: number;
    lang: string;
    clientId: string;
  }) => Promise<TriggerResultLike | null>;

  /** Called for explicit, user-initiated ("on-demand") narration requests. */
  fetchNarration?: (params: {
    placeId: string;
    lang: string;
    clientId: string;
  }) => Promise<NarrationLike | null>;

  /** Invoked whenever a narration should actually be played. */
  onNarration: (event: NarrationEvent) => void;

  /** Optional error sink (network failures etc. are otherwise swallowed). */
  onError?: (error: unknown, context: { operation: string }) => void;
}

const DEFAULT_DEBOUNCE_MS = 3000;
const DEFAULT_THROTTLE_MS = 5000;
const DEFAULT_COOLDOWN_MS = 5 * 60 * 1000;

export class GeofenceEngine {
  private readonly clientId: string;
  private readonly debounceMs: number;
  private readonly throttleMs: number;
  private readonly cooldownMs: number;
  private readonly now: () => number;
  private readonly fetchTrigger: GeofenceEngineOptions["fetchTrigger"];
  private readonly fetchNarration: GeofenceEngineOptions["fetchNarration"];
  private readonly onNarration: GeofenceEngineOptions["onNarration"];
  private readonly onError: GeofenceEngineOptions["onError"];

  private lang: string;
  /** Bumped on every setLanguage call; in-flight requests capture their generation and are dropped if stale. */
  private generation = 0;

  private debounceTimer: ReturnType<typeof setTimeout> | null = null;
  private throttleTimer: ReturnType<typeof setTimeout> | null = null;
  private lastCheckAt = -Infinity;
  private pendingPosition: GeoPosition | null = null;

  /** key: `${clientId}:${placeId}` -> last-triggered timestamp (ms). */
  private readonly lastTriggeredAt = new Map<string, number>();

  private disposed = false;

  /** Count of fetchTrigger calls issued so far (useful for assertions in tests). */
  triggerCallCount = 0;

  constructor(options: GeofenceEngineOptions) {
    this.clientId = options.clientId;
    this.lang = options.initialLang ?? "vi";
    this.debounceMs = options.debounceMs ?? DEFAULT_DEBOUNCE_MS;
    this.throttleMs = options.throttleMs ?? DEFAULT_THROTTLE_MS;
    this.cooldownMs = options.cooldownMs ?? DEFAULT_COOLDOWN_MS;
    this.now = options.now ?? Date.now;
    this.fetchTrigger = options.fetchTrigger;
    this.fetchNarration = options.fetchNarration;
    this.onNarration = options.onNarration;
    this.onError = options.onError;
  }

  getLanguage(): string {
    return this.lang;
  }

  /** Switches the active narration language. Any in-flight requests started
   * under the previous language will be discarded when they resolve. */
  setLanguage(lang: string): void {
    if (lang === this.lang) return;
    this.lang = lang;
    this.generation += 1;
  }

  /** Feed a raw GPS fix. Safe to call as often as the browser reports updates;
   * internally debounced + throttled. */
  onPosition(position: GeoPosition): void {
    if (this.disposed) return;
    this.pendingPosition = position;

    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
    }
    this.debounceTimer = setTimeout(() => {
      this.debounceTimer = null;
      this.scheduleThrottledCheck();
    }, this.debounceMs);
  }

  /** Immediately requests narration for a specific place, bypassing the
   * geofence pipeline (e.g. the user tapped a place in a list). Still
   * subject to language-generation cancellation. */
  async requestOnDemand(placeId: string): Promise<void> {
    if (!this.fetchNarration) return;
    const generation = this.generation;
    const lang = this.lang;
    try {
      const narration = await this.fetchNarration({ placeId, lang, clientId: this.clientId });
      if (this.disposed || generation !== this.generation) return; // stale: language changed mid-flight
      if (narration) {
        this.onNarration({ source: "on-demand", placeId, lang, narration });
      }
    } catch (error) {
      if (!this.disposed && generation === this.generation) {
        this.onError?.(error, { operation: "requestOnDemand" });
      }
    }
  }

  /**
   * Demo/simulate helper: skip debounce + client cooldown and play narration
   * whenever the API returns a narration payload (even if server cooldown
   * set triggered=false).
   */
  async forceCheck(position: GeoPosition): Promise<void> {
    if (this.disposed) return;
    this.pendingPosition = position;
    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    if (this.throttleTimer !== null) {
      clearTimeout(this.throttleTimer);
      this.throttleTimer = null;
    }

    this.lastCheckAt = this.now();
    const generation = this.generation;
    const lang = this.lang;
    this.triggerCallCount += 1;

    try {
      const result = await this.fetchTrigger({
        lat: position.lat,
        lng: position.lng,
        lang,
        clientId: this.clientId,
      });
      if (this.disposed || generation !== this.generation) return;
      if (!result?.narration) return;

      const key = `${this.clientId}:${result.placeId}`;
      this.lastTriggeredAt.set(key, this.now());
      this.onNarration({
        source: "geofence",
        placeId: result.placeId,
        lang,
        narration: result.narration,
      });
    } catch (error) {
      if (!this.disposed && generation === this.generation) {
        this.onError?.(error, { operation: "forceCheck" });
      }
    }
  }

  /** Clears all pending timers. The engine must not be used after this. */
  dispose(): void {
    this.disposed = true;
    if (this.debounceTimer !== null) clearTimeout(this.debounceTimer);
    if (this.throttleTimer !== null) clearTimeout(this.throttleTimer);
    this.debounceTimer = null;
    this.throttleTimer = null;
  }

  // -- internals -----------------------------------------------------------

  private scheduleThrottledCheck(): void {
    if (this.disposed) return;
    const elapsed = this.now() - this.lastCheckAt;

    if (elapsed >= this.throttleMs) {
      this.runCheck();
      return;
    }

    // Trailing edge: replace any already-scheduled trailing check so only
    // the most recent position is ever checked once the window reopens.
    if (this.throttleTimer !== null) {
      clearTimeout(this.throttleTimer);
    }
    const remaining = this.throttleMs - elapsed;
    this.throttleTimer = setTimeout(() => {
      this.throttleTimer = null;
      this.runCheck();
    }, remaining);
  }

  private runCheck(): void {
    const position = this.pendingPosition;
    if (!position || this.disposed) return;

    this.lastCheckAt = this.now();
    const generation = this.generation;
    const lang = this.lang;
    this.triggerCallCount += 1;

    this.fetchTrigger({ lat: position.lat, lng: position.lng, lang, clientId: this.clientId })
      .then((result) => {
        if (this.disposed || generation !== this.generation) return; // stale language
        if (!result || !result.triggered || !result.narration) return;

        const key = `${this.clientId}:${result.placeId}`;
        const last = this.lastTriggeredAt.get(key) ?? -Infinity;
        const nowMs = this.now();
        if (nowMs - last < this.cooldownMs) {
          return; // still cooling down for this place/client - skip playback.
        }

        this.lastTriggeredAt.set(key, nowMs);
        this.onNarration({
          source: "geofence",
          placeId: result.placeId,
          lang,
          narration: result.narration,
        });
      })
      .catch((error) => {
        if (!this.disposed && generation === this.generation) {
          this.onError?.(error, { operation: "fetchTrigger" });
        }
      });
  }
}

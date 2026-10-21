import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { GeofenceEngine, type NarrationEvent, type TriggerResultLike } from "./GeofenceEngine";

function narrationResult(placeId: string, overrides: Partial<TriggerResultLike> = {}): TriggerResultLike {
  return {
    placeId,
    name: "Place " + placeId,
    distanceMeters: 10,
    triggered: true,
    narration: {
      placeId,
      text: "Hello narration",
      audio: { available: true, url: `https://cdn.example/${placeId}.mp3`, durationSec: 30, status: "Done" },
    },
    ...overrides,
  };
}

describe("GeofenceEngine", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("debounces rapid raw GPS updates and only checks once the position settles (3s)", async () => {
    const fetchTrigger = vi.fn().mockResolvedValue(null);
    const engine = new GeofenceEngine({
      clientId: "client-1",
      fetchTrigger,
      onNarration: vi.fn(),
    });

    engine.onPosition({ lat: 10.0, lng: 106.0 });
    await vi.advanceTimersByTimeAsync(1000);
    engine.onPosition({ lat: 10.0001, lng: 106.0001 }); // resets debounce
    await vi.advanceTimersByTimeAsync(1000);
    engine.onPosition({ lat: 10.0002, lng: 106.0002 }); // resets debounce again

    // Still inside the 3s debounce window from the last update - no call yet.
    await vi.advanceTimersByTimeAsync(2999);
    expect(fetchTrigger).not.toHaveBeenCalled();

    // Quiet period elapses -> exactly one check fires.
    await vi.advanceTimersByTimeAsync(1);
    expect(fetchTrigger).toHaveBeenCalledTimes(1);
    expect(fetchTrigger).toHaveBeenCalledWith({
      lat: 10.0002,
      lng: 106.0002,
      lang: "vi",
      clientId: "client-1",
    });

    engine.dispose();
  });

  it("throttles geofence checks to at most once per 5s, using the latest position (trailing edge)", async () => {
    const fetchTrigger = vi.fn().mockResolvedValue(null);
    const engine = new GeofenceEngine({
      clientId: "client-1",
      fetchTrigger,
      onNarration: vi.fn(),
    });

    // First settled position triggers an immediate check.
    engine.onPosition({ lat: 1, lng: 1 });
    await vi.advanceTimersByTimeAsync(3000);
    expect(fetchTrigger).toHaveBeenCalledTimes(1);

    // 1s after the first check, a new position settles - still within the
    // 5s throttle window, so it must be deferred (trailing), not dropped.
    await vi.advanceTimersByTimeAsync(1000);
    engine.onPosition({ lat: 2, lng: 2 });
    await vi.advanceTimersByTimeAsync(3000); // debounce elapses at t=4000 since last check
    expect(fetchTrigger).toHaveBeenCalledTimes(1); // throttle window (5s) not yet reached

    await vi.advanceTimersByTimeAsync(1000); // throttle window closes at t=5000
    expect(fetchTrigger).toHaveBeenCalledTimes(2);
    expect(fetchTrigger).toHaveBeenLastCalledWith({ lat: 2, lng: 2, lang: "vi", clientId: "client-1" });

    engine.dispose();
  });

  it("forceCheck plays immediately even when server cooldown sets triggered=false", async () => {
    const onNarration = vi.fn<(event: NarrationEvent) => void>();
    const fetchTrigger = vi.fn().mockResolvedValue(
      narrationResult("place-A", { triggered: false }),
    );
    const engine = new GeofenceEngine({
      clientId: "client-1",
      fetchTrigger,
      onNarration,
      cooldownMs: 5 * 60 * 1000,
    });

    await engine.forceCheck({ lat: 10.7769, lng: 106.6959 });
    expect(fetchTrigger).toHaveBeenCalledTimes(1);
    expect(onNarration).toHaveBeenCalledTimes(1);
    expect(onNarration.mock.calls[0][0].placeId).toBe("place-A");

    engine.dispose();
  });

  it("suppresses repeat narration playback for the same place within the 5 min client cooldown", async () => {
    const onNarration = vi.fn<(event: NarrationEvent) => void>();
    const fetchTrigger = vi.fn().mockResolvedValue(narrationResult("place-A"));
    const engine = new GeofenceEngine({
      clientId: "client-1",
      fetchTrigger,
      onNarration,
      cooldownMs: 5 * 60 * 1000,
    });

    engine.onPosition({ lat: 1, lng: 1 });
    await vi.advanceTimersByTimeAsync(3000);
    expect(onNarration).toHaveBeenCalledTimes(1);

    // Still within cooldown (well under 5 minutes), same place triggers again server-side,
    // but the client must not play it a second time.
    await vi.advanceTimersByTimeAsync(5000); // clears the 5s throttle window
    engine.onPosition({ lat: 1.00001, lng: 1.00001 });
    await vi.advanceTimersByTimeAsync(3000);
    expect(fetchTrigger).toHaveBeenCalledTimes(2);
    expect(onNarration).toHaveBeenCalledTimes(1); // unchanged - cooldown suppressed it

    // After the cooldown fully elapses, the same place may trigger again.
    await vi.advanceTimersByTimeAsync(5 * 60 * 1000);
    engine.onPosition({ lat: 1.00002, lng: 1.00002 });
    await vi.advanceTimersByTimeAsync(3000);
    expect(onNarration).toHaveBeenCalledTimes(2);

    engine.dispose();
  });

  it("does not suppress narration for a different place even while another place is cooling down", async () => {
    const onNarration = vi.fn<(event: NarrationEvent) => void>();
    const fetchTrigger = vi
      .fn()
      .mockResolvedValueOnce(narrationResult("place-A"))
      .mockResolvedValueOnce(narrationResult("place-B"));
    const engine = new GeofenceEngine({ clientId: "client-1", fetchTrigger, onNarration });

    engine.onPosition({ lat: 1, lng: 1 });
    await vi.advanceTimersByTimeAsync(3000);
    expect(onNarration).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(5000);
    engine.onPosition({ lat: 2, lng: 2 });
    await vi.advanceTimersByTimeAsync(3000);
    expect(onNarration).toHaveBeenCalledTimes(2);
    expect(onNarration.mock.calls[1][0].placeId).toBe("place-B");

    engine.dispose();
  });

  it("cancels stale on-demand results when the language changes mid-flight", async () => {
    const onNarration = vi.fn<(event: NarrationEvent) => void>();
    let resolveFirst!: (value: TriggerResultLike["narration"]) => void;
    const fetchNarration = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveFirst = resolve;
          }),
      )
      .mockResolvedValueOnce({
        placeId: "place-B",
        text: "English narration",
        audio: { available: true, url: "https://cdn.example/en.mp3" },
      });

    const engine = new GeofenceEngine({
      clientId: "client-1",
      initialLang: "vi",
      fetchTrigger: vi.fn().mockResolvedValue(null),
      fetchNarration,
      onNarration,
    });

    const pending = engine.requestOnDemand("place-A"); // started while lang=vi

    // Language flips before the first request resolves.
    engine.setLanguage("en");

    // Second, newer on-demand request under the new language.
    const second = engine.requestOnDemand("place-B");
    await second;
    expect(onNarration).toHaveBeenCalledTimes(1);
    expect(onNarration.mock.calls[0][0]).toMatchObject({ placeId: "place-B", lang: "en" });

    // Now the stale "vi" request resolves - it must be discarded.
    resolveFirst({
      placeId: "place-A",
      text: "Vietnamese narration (stale)",
      audio: { available: true, url: "https://cdn.example/vi.mp3" },
    });
    await pending;

    expect(onNarration).toHaveBeenCalledTimes(1); // still just the "place-B" event

    engine.dispose();
  });

  it("stops scheduling any work after dispose()", async () => {
    const fetchTrigger = vi.fn().mockResolvedValue(null);
    const engine = new GeofenceEngine({ clientId: "c", fetchTrigger, onNarration: vi.fn() });

    engine.onPosition({ lat: 1, lng: 1 });
    engine.dispose();

    await vi.advanceTimersByTimeAsync(10_000);
    expect(fetchTrigger).not.toHaveBeenCalled();
  });

  it("propagates fetchTrigger errors to onError without throwing", async () => {
    const onError = vi.fn();
    const fetchTrigger = vi.fn().mockRejectedValue(new Error("network down"));
    const engine = new GeofenceEngine({
      clientId: "c",
      fetchTrigger,
      onNarration: vi.fn(),
      onError,
    });

    engine.onPosition({ lat: 1, lng: 1 });
    await vi.advanceTimersByTimeAsync(3000);

    expect(onError).toHaveBeenCalledTimes(1);
    expect(onError.mock.calls[0][1]).toMatchObject({ operation: "fetchTrigger" });

    engine.dispose();
  });
});

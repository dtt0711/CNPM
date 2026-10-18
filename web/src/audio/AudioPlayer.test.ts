import { describe, it, expect, vi } from "vitest";
import {
  playNarrationWithFallback,
  pollUntilReady,
  type AudioPlayerDeps,
  type NarrationLike,
} from "./AudioPlayer";

function narration(overrides: Partial<NarrationLike["audio"]> = {}): NarrationLike {
  return {
    placeId: "place-1",
    lang: "vi",
    text: "Day la Nha Tho Duc Ba.",
    audio: { available: false, status: "", ...overrides },
  };
}

/** Base deps where every tier is a jest/vitest mock the test can override. */
function makeDeps(overrides: Partial<AudioPlayerDeps> = {}): AudioPlayerDeps {
  return {
    playUrl: vi.fn().mockResolvedValue(undefined),
    pollTaskStatus: vi.fn().mockResolvedValue(null),
    requestTtsStream: vi.fn().mockResolvedValue(null),
    speak: vi.fn().mockResolvedValue(undefined),
    wait: vi.fn().mockResolvedValue(undefined), // no real delay in tests
    pollIntervalMs: 1,
    pollTimeoutMs: 5,
    ...overrides,
  };
}

describe("playNarrationWithFallback", () => {
  it("tier 1: plays immediately when a ready audio URL is already available", async () => {
    const deps = makeDeps();
    const n = narration({ available: true, url: "https://cdn.example/ready.mp3", status: "Done" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("ready-url");
    expect(deps.playUrl).toHaveBeenCalledWith("https://cdn.example/ready.mp3");
    expect(deps.pollTaskStatus).not.toHaveBeenCalled();
    expect(deps.requestTtsStream).not.toHaveBeenCalled();
    expect(deps.speak).not.toHaveBeenCalled();
  });

  it("tier 2: waits/polls for an in-progress generation task, then plays once ready", async () => {
    const pollTaskStatus = vi
      .fn()
      .mockResolvedValueOnce({ available: false, status: "Pending" })
      .mockResolvedValueOnce({ available: false, status: "Running" })
      .mockResolvedValueOnce({ available: true, url: "https://cdn.example/generated.mp3", status: "Done" });

    const deps = makeDeps({ pollTaskStatus });
    const n = narration({ available: false, status: "Pending" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("poll-task");
    expect(pollTaskStatus).toHaveBeenCalledTimes(3);
    expect(deps.playUrl).toHaveBeenCalledWith("https://cdn.example/generated.mp3");
    expect(deps.requestTtsStream).not.toHaveBeenCalled();
    expect(deps.speak).not.toHaveBeenCalled();
  });

  it("tier 3: falls back to POST /audio/tts streaming when there is no ready/pending audio", async () => {
    const requestTtsStream = vi.fn().mockResolvedValue({ url: "blob:tts-stream" });
    const deps = makeDeps({ requestTtsStream });
    const n = narration({ available: false, status: "" }); // no url, not pending -> skip tier 1/2

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("tts-stream");
    expect(requestTtsStream).toHaveBeenCalledWith({
      placeId: "place-1",
      lang: "vi",
      text: n.text,
    });
    expect(deps.playUrl).toHaveBeenCalledWith("blob:tts-stream");
    expect(deps.speak).not.toHaveBeenCalled();
  });

  it("tier 3 is reached when polling for the pending task times out without becoming ready", async () => {
    // Deterministic virtual clock: each `wait()` advances time by one interval,
    // so the poll loop deterministically exceeds pollTimeoutMs after 3 attempts
    // without depending on real wall-clock timing.
    let elapsed = 0;
    const pollTaskStatus = vi.fn().mockResolvedValue({ available: false, status: "Pending" });
    const requestTtsStream = vi.fn().mockResolvedValue({ url: "blob:fallback" });
    const deps = makeDeps({
      pollTaskStatus,
      requestTtsStream,
      pollTimeoutMs: 3,
      pollIntervalMs: 1,
      now: () => elapsed,
      wait: vi.fn().mockImplementation(async () => {
        elapsed += 1;
      }),
    });
    const n = narration({ available: false, status: "Pending" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("tts-stream");
    expect(pollTaskStatus).toHaveBeenCalledTimes(3);
    expect(requestTtsStream).toHaveBeenCalledTimes(1);
  });

  it("tier 4: uses the Web Speech API as the last resort when every network tier fails", async () => {
    const deps = makeDeps({
      requestTtsStream: vi.fn().mockRejectedValue(new Error("network error")),
      speak: vi.fn().mockResolvedValue(undefined),
    });
    const n = narration({ available: false, status: "Failed" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("web-speech");
    expect(deps.speak).toHaveBeenCalledWith(n.text, "vi");
  });

  it("falls through to tier 3/4 when the ready URL exists but actually fails to play", async () => {
    const playUrl = vi
      .fn()
      .mockRejectedValueOnce(new Error("404"))
      .mockResolvedValueOnce(undefined);
    const requestTtsStream = vi.fn().mockResolvedValue({ url: "blob:recovered" });
    const deps = makeDeps({ playUrl, requestTtsStream });
    const n = narration({ available: true, url: "https://cdn.example/broken.mp3", status: "Done" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("tts-stream");
    expect(playUrl).toHaveBeenNthCalledWith(1, "https://cdn.example/broken.mp3");
    expect(playUrl).toHaveBeenNthCalledWith(2, "blob:recovered");
  });

  it("returns 'none' when every tier is unavailable/fails", async () => {
    const deps = makeDeps({
      requestTtsStream: vi.fn().mockResolvedValue(null),
      speak: vi.fn().mockRejectedValue(new Error("no speech engine")),
    });
    const n = narration({ available: false, status: "" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("none");
  });

  it("stops immediately and plays nothing once the cancellation token is set", async () => {
    const playUrl = vi.fn().mockResolvedValue(undefined);
    const requestTtsStream = vi.fn().mockResolvedValue({ url: "blob:x" });
    const speak = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ playUrl, requestTtsStream, speak });
    const n = narration({ available: false, status: "" });
    const token = { cancelled: true };

    const tier = await playNarrationWithFallback(n, deps, token);

    expect(tier).toBe("none");
    expect(requestTtsStream).not.toHaveBeenCalled();
    expect(speak).not.toHaveBeenCalled();
  });
});

describe("pollUntilReady", () => {
  it("stops polling and returns null as soon as the task reports Failed", async () => {
    const pollTaskStatus = vi
      .fn()
      .mockResolvedValueOnce({ available: false, status: "Pending" })
      .mockResolvedValueOnce({ available: false, status: "Failed" });
    const deps = makeDeps({ pollTaskStatus });

    const result = await pollUntilReady(narration({ status: "Pending" }), deps);

    expect(result).toBeNull();
    expect(pollTaskStatus).toHaveBeenCalledTimes(2);
  });
});

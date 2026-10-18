import { describe, it, expect, vi } from "vitest";
import {
  playNarrationWithFallback,
  pollUntilReady,
  toSpeechLocale,
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

function makeDeps(overrides: Partial<AudioPlayerDeps> = {}): AudioPlayerDeps {
  return {
    playUrl: vi.fn().mockResolvedValue(undefined),
    pollTaskStatus: vi.fn().mockResolvedValue(null),
    requestTtsStream: vi.fn().mockResolvedValue(null),
    speak: vi.fn().mockResolvedValue(undefined),
    wait: vi.fn().mockResolvedValue(undefined),
    pollIntervalMs: 1,
    pollTimeoutMs: 5,
    ...overrides,
  };
}

describe("toSpeechLocale", () => {
  it("maps app codes to BCP-47", () => {
    expect(toSpeechLocale("vi")).toBe("vi-VN");
    expect(toSpeechLocale("ja")).toBe("ja-JP");
  });
});

describe("playNarrationWithFallback", () => {
  it("uses Web Speech when text exists (preferred in Vite DEV)", async () => {
    const deps = makeDeps();
    const n = narration({ available: false, status: "" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("web-speech");
    expect(deps.speak).toHaveBeenCalledWith(n.text, "vi-VN");
    expect(deps.requestTtsStream).not.toHaveBeenCalled();
  });

  it("plays ready URL when Web Speech fails", async () => {
    const deps = makeDeps({ speak: vi.fn().mockRejectedValue(new Error("no voices")) });
    const n = narration({ available: true, url: "https://cdn.example/ready.mp3", status: "Done" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("ready-url");
    expect(deps.playUrl).toHaveBeenCalledWith("https://cdn.example/ready.mp3");
  });

  it("polls then plays when Web Speech fails and a pending task becomes ready", async () => {
    const pollTaskStatus = vi
      .fn()
      .mockResolvedValueOnce({ available: false, status: "Pending" })
      .mockResolvedValueOnce({ available: true, url: "https://cdn.example/generated.mp3", status: "Done" });

    const deps = makeDeps({
      speak: vi.fn().mockRejectedValue(new Error("no voices")),
      pollTaskStatus,
    });
    const n = narration({ available: false, status: "Pending" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("poll-task");
    expect(deps.playUrl).toHaveBeenCalledWith("https://cdn.example/generated.mp3");
  });

  it("falls back to TTS stream when Web Speech fails and nothing is pending", async () => {
    const requestTtsStream = vi.fn().mockResolvedValue({ url: "blob:tts-stream" });
    const deps = makeDeps({
      speak: vi.fn().mockRejectedValue(new Error("no voices")),
      requestTtsStream,
    });
    const n = narration({ available: false, status: "" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("tts-stream");
    expect(requestTtsStream).toHaveBeenCalled();
  });

  it("returns none when every tier fails", async () => {
    const deps = makeDeps({
      speak: vi.fn().mockRejectedValue(new Error("no speech")),
      requestTtsStream: vi.fn().mockResolvedValue(null),
    });
    const n = narration({ available: false, status: "" });

    const tier = await playNarrationWithFallback(n, deps);

    expect(tier).toBe("none");
  });

  it("stops immediately when cancelled", async () => {
    const deps = makeDeps();
    const tier = await playNarrationWithFallback(narration(), deps, { cancelled: true });
    expect(tier).toBe("none");
    expect(deps.speak).not.toHaveBeenCalled();
  });
});

describe("pollUntilReady", () => {
  it("stops polling when Failed", async () => {
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

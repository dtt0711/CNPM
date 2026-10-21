import { useEffect, useRef, type MouseEvent } from "react";
import { useTranslation } from "react-i18next";
import type { NarrationLike } from "../engine/GeofenceEngine";
import type { FallbackTier } from "../audio/AudioPlayer";
import { unlockSpeechSynthesis } from "../hooks/useNarrationEngine";

export interface AudioControlsProps {
  placeId: string;
  placeName: string;
  narration: NarrationLike | null;
  tier: FallbackTier | "loading" | "idle";
  /** Seekable MP3 URL for the native HTML5 player. */
  mediaUrl: string | null;
  error: string | null;
  onPlay: (placeId: string) => void;
  onStop: () => void;
}

function samePlace(a: string | undefined | null, b: string): boolean {
  return !!a && a.toLowerCase() === b.toLowerCase();
}

export function AudioControls({
  placeId,
  placeName,
  narration,
  tier,
  mediaUrl,
  error,
  onPlay,
  onStop,
}: AudioControlsProps) {
  const { t } = useTranslation();
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const isForThisPlace = samePlace(narration?.placeId, placeId);
  const activeMediaUrl = isForThisPlace ? mediaUrl : null;
  const isBusy = isForThisPlace && (tier === "loading" || tier === "poll-task") && !activeMediaUrl;
  const isSpeechPlaying =
    isForThisPlace &&
    !activeMediaUrl &&
    (tier === "web-speech" || tier === "tts-stream");

  const showStatus =
    isForThisPlace && (Boolean(error) || isBusy || isSpeechPlaying) && !activeMediaUrl;
  const statusKey = error ? "error" : String(tier);

  useEffect(() => {
    const el = audioRef.current;
    if (!el || !activeMediaUrl) return;
    el.load();
    void el.play().catch(() => undefined);
  }, [activeMediaUrl]);

  const handlePlay = (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    if (isBusy) return;
    unlockSpeechSynthesis();
    if (activeMediaUrl && audioRef.current) {
      void audioRef.current.play().catch(() => undefined);
      return;
    }
    onPlay(placeId);
  };

  const handleStop = (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current.currentTime = 0;
    }
    onStop();
  };

  return (
    <div className="audio-controls">
      <div className="audio-controls__buttons">
        <button
          type="button"
          className={
            "btn btn--primary audio-controls__play" +
            (isBusy ? " audio-controls__play--busy" : "")
          }
          onClick={handlePlay}
          disabled={isBusy}
          aria-busy={isBusy}
          aria-label={`${t("places.playNarration")} - ${placeName}`}
        >
          {isBusy
            ? t("audio.status.loading")
            : activeMediaUrl
              ? t("audio.play")
              : t("places.playNarration")}
        </button>
        {(activeMediaUrl || isSpeechPlaying || isBusy) && (
          <button type="button" className="btn btn--ghost" onClick={handleStop}>
            {t("audio.stop")}
          </button>
        )}
      </div>

      {showStatus && (
        <p className={"audio-controls__status" + (error ? " audio-controls__status--error" : "")}>
          {error
            ? `${t("audio.status.error")}: ${error}`
            : t(`audio.status.${statusKey}` as const, { defaultValue: statusKey })}
        </p>
      )}

      {activeMediaUrl && (
        <audio
          ref={audioRef}
          className="audio-controls__player"
          controls
          preload="metadata"
          src={activeMediaUrl}
          aria-label={`${t("places.playNarration")} - ${placeName}`}
        />
      )}
    </div>
  );
}

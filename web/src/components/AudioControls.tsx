import { useTranslation } from "react-i18next";
import type { NarrationLike } from "../engine/GeofenceEngine";
import type { FallbackTier } from "../audio/AudioPlayer";

export interface AudioControlsProps {
  placeId: string;
  placeName: string;
  narration: NarrationLike | null;
  tier: FallbackTier | "loading" | "idle";
  error: string | null;
  onPlay: (placeId: string) => void;
  onStop: () => void;
}

export function AudioControls({
  placeId,
  placeName,
  narration,
  tier,
  error,
  onPlay,
  onStop,
}: AudioControlsProps) {
  const { t } = useTranslation();

  const isForThisPlace = narration?.placeId === placeId;
  const isBusy = isForThisPlace && (tier === "loading");
  const isPlaying = isForThisPlace && tier !== "loading" && tier !== "idle";

  const statusKey = error && isForThisPlace ? "error" : isForThisPlace ? String(tier) : "idle";

  return (
    <div className="audio-controls">
      <div className="audio-controls__buttons">
        <button
          type="button"
          className="btn btn--primary"
          onClick={() => onPlay(placeId)}
          disabled={isBusy}
          aria-label={`${t("places.playNarration")} - ${placeName}`}
        >
          {isBusy ? t("audio.status.loading") : t("places.playNarration")}
        </button>
        {isPlaying && (
          <button type="button" className="btn btn--ghost" onClick={onStop}>
            {t("audio.stop")}
          </button>
        )}
      </div>

      {isForThisPlace && (
        <p className={"audio-controls__status" + (error ? " audio-controls__status--error" : "")}>
          {t(`audio.status.${statusKey}` as const, { defaultValue: statusKey })}
        </p>
      )}

      {isForThisPlace && narration?.text && (
        <p className="audio-controls__transcript">{narration.text}</p>
      )}
    </div>
  );
}

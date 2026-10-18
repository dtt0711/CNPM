import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import type { PlaceDetailDto, PlaceDto } from "../api/types";
import { AudioControls, type AudioControlsProps } from "./AudioControls";

export interface PlaceDetailProps {
  place: PlaceDto;
  lang: string;
  onBack: () => void;
  narrationProps: Omit<AudioControlsProps, "placeId" | "placeName">;
}

export function PlaceDetail({ place, lang, onBack, narrationProps }: PlaceDetailProps) {
  const { t } = useTranslation();
  const [detail, setDetail] = useState<PlaceDetailDto | null>(null);

  useEffect(() => {
    let active = true;
    setDetail(null);
    api
      .getPlace(place.id)
      .then((result) => {
        if (active) setDetail(result);
      })
      .catch(() => {
        /* fall back to the summary fields already shown */
      });
    return () => {
      active = false;
    };
  }, [place.id]);

  const translation = detail?.translations.find(
    (tr) => tr.lang.toLowerCase() === lang.toLowerCase(),
  );
  const description = lang === "vi" ? place.description : translation?.text || place.description;

  return (
    <article className="place-detail">
      <button type="button" className="btn btn--ghost place-detail__back" onClick={onBack}>
        &larr; {t("detail.back")}
      </button>

      {place.imageUrl && (
        <img className="place-detail__image" src={place.imageUrl} alt={place.name} />
      )}

      <h2 className="place-detail__title">{place.name}</h2>
      <p className="place-detail__meta">
        {place.category} · {place.district}
      </p>

      <dl className="place-detail__facts">
        <div>
          <dt>{t("detail.address")}</dt>
          <dd>{place.address}</dd>
        </div>
        <div>
          <dt>{t("detail.openingHours")}</dt>
          <dd>{place.openingHours || "—"}</dd>
        </div>
        <div>
          <dt>{t("detail.ticketPrice")}</dt>
          <dd>{place.ticketPrice || "—"}</dd>
        </div>
      </dl>

      <p className="place-detail__description">
        {description || t("detail.noDescription")}
      </p>

      <AudioControls placeId={place.id} placeName={place.name} {...narrationProps} />
    </article>
  );
}

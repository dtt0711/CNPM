import { useTranslation } from "react-i18next";
import type { PlaceDto } from "../api/types";

export interface PlaceListProps {
  places: PlaceDto[];
  loading: boolean;
  error: string | null;
  selectedPlaceId: string | null;
  onSelectPlace: (placeId: string) => void;
  onRetry: () => void;
}

export function PlaceList({
  places,
  loading,
  error,
  selectedPlaceId,
  onSelectPlace,
  onRetry,
}: PlaceListProps) {
  const { t } = useTranslation();

  if (loading) {
    return <div className="panel panel--muted">{t("places.loading")}</div>;
  }

  if (error) {
    return (
      <div className="panel panel--error">
        <p>{t("places.error")}</p>
        <button type="button" className="btn btn--ghost" onClick={onRetry}>
          {t("places.retry")}
        </button>
      </div>
    );
  }

  if (places.length === 0) {
    return <div className="panel panel--muted">{t("places.empty")}</div>;
  }

  return (
    <ul className="place-list">
      {places.map((place) => (
        <li key={place.id}>
          <button
            type="button"
            className={
              "place-card" + (place.id === selectedPlaceId ? " place-card--active" : "")
            }
            onClick={() => onSelectPlace(place.id)}
          >
            {place.imageUrl ? (
              <img className="place-card__image" src={place.imageUrl} alt="" loading="lazy" />
            ) : (
              <div className="place-card__image place-card__image--placeholder" aria-hidden="true" />
            )}
            <div className="place-card__body">
              <h3 className="place-card__title">{place.name}</h3>
              <p className="place-card__meta">
                {place.category} · {place.district}
              </p>
              <p className="place-card__rating">
                {t("places.rating", { rating: place.averageRating.toFixed(1), count: place.reviewCount })}
              </p>
            </div>
          </button>
        </li>
      ))}
    </ul>
  );
}

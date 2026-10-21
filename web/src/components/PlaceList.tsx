import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { PlaceDto } from "../api/types";
import { formatDistanceKm, haversineMeters } from "../geo/distance";
import { formatWard } from "../places/ward";
import { GoogleStyleRating } from "./GoogleStyleRating";

export interface PlaceListProps {
  places: PlaceDto[];
  loading: boolean;
  error: string | null;
  selectedPlaceId: string | null;
  onSelectPlace: (placeId: string) => void;
  onRetry: () => void;
  /** Reference point for distance sort (GPS or demo). */
  origin: { lat: number; lng: number } | null;
}

export function PlaceList({
  places,
  loading,
  error,
  selectedPlaceId,
  onSelectPlace,
  onRetry,
  origin,
}: PlaceListProps) {
  const { t } = useTranslation();

  const ranked = useMemo(() => {
    if (!origin) {
      return places.map((place) => ({ place, distanceMeters: null as number | null }));
    }
    return places
      .map((place) => ({
        place,
        distanceMeters: haversineMeters(origin.lat, origin.lng, place.latitude, place.longitude),
      }))
      .sort((a, b) => (a.distanceMeters ?? 0) - (b.distanceMeters ?? 0));
  }, [places, origin]);

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

  if (ranked.length === 0) {
    return <div className="panel panel--muted">{t("places.empty")}</div>;
  }

  return (
    <ul className="place-list">
      {ranked.map(({ place, distanceMeters }) => (
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
              <div className="place-card__heading">
                <h3 className="place-card__title">{place.name}</h3>
                {distanceMeters != null && (
                  <span className="place-card__distance" title={t("places.distanceHint")}>
                    {formatDistanceKm(distanceMeters)}
                  </span>
                )}
              </div>
              <p className="place-card__meta">
                {place.category} · {formatWard(place.ward, t("places.ward"))}
              </p>
              <GoogleStyleRating
                className="place-card__rating"
                rating={place.averageRating}
                reviewCount={place.reviewCount}
              />
              <span className="place-card__cta">{t("places.viewDetail")}</span>
            </div>
          </button>
        </li>
      ))}
    </ul>
  );
}

import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import type { PlaceDto } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { localizePlaceFacts } from "../places/facts";
import { formatWard } from "../places/ward";
import { GoogleStyleRating } from "./GoogleStyleRating";

export interface PlaceDetailProps {
  place: PlaceDto;
  lang: string;
  onBack: () => void;
}

export function PlaceDetail({ place, lang, onBack }: PlaceDetailProps) {
  const { t } = useTranslation();
  const { session } = useAuth();
  const facts = localizePlaceFacts(place, lang);
  const [description, setDescription] = useState(place.description);
  const [loadingText, setLoadingText] = useState(lang !== "vi");
  const [isFallback, setIsFallback] = useState(false);
  const [favorite, setFavorite] = useState(false);
  const [favBusy, setFavBusy] = useState(false);

  useEffect(() => {
    let active = true;
    setLoadingText(true);
    setDescription(place.description);
    setIsFallback(lang !== "vi");

    // Narration auto-translates; poll until target lang text is ready (or timeout).
    void (async () => {
      const deadline = Date.now() + 60_000;
      try {
        let n = await api.getNarration(place.id, lang);
        while (
          active &&
          lang !== "vi" &&
          Date.now() < deadline &&
          (n.isFallback || !n.text?.trim())
        ) {
          await new Promise((r) => setTimeout(r, 2000));
          if (!active) return;
          n = await api.getNarration(place.id, lang);
        }
        if (!active) return;
        if (n.text?.trim()) setDescription(n.text);
        setIsFallback(Boolean(n.isFallback));
      } catch {
        /* keep list description */
      } finally {
        if (active) setLoadingText(false);
      }
    })();

    return () => {
      active = false;
    };
  }, [place.id, place.description, lang]);

  useEffect(() => {
    if (!session) {
      setFavorite(false);
      return;
    }
    let active = true;
    void api
      .getFavorites()
      .then((list) => {
        if (active) setFavorite(list.some((f) => f.placeId === place.id));
      })
      .catch(() => {
        if (active) setFavorite(false);
      });
    return () => {
      active = false;
    };
  }, [session, place.id]);

  const toggleFavorite = async () => {
    if (!session || favBusy) return;
    setFavBusy(true);
    try {
      if (favorite) {
        await api.removeFavorite(place.id);
        setFavorite(false);
      } else {
        await api.addFavorite(place.id);
        setFavorite(true);
      }
    } finally {
      setFavBusy(false);
    }
  };

  return (
    <article className="place-detail">
      <button type="button" className="btn btn--ghost place-detail__back" onClick={onBack}>
        &larr; {t("detail.back")}
      </button>

      <div className="place-detail__title-row">
        <h2 className="place-detail__title">{place.name}</h2>
        {session ? (
          <button
            type="button"
            className={"place-detail__fav" + (favorite ? " place-detail__fav--on" : "")}
            onClick={() => void toggleFavorite()}
            disabled={favBusy}
          >
            {favorite ? t("detail.unfavorite") : t("detail.favorite")}
          </button>
        ) : (
          <Link className="place-detail__fav" to="/login?from=/">
            {t("detail.favorite")}
          </Link>
        )}
      </div>
      <p className="place-detail__meta">
        {place.category} · {formatWard(place.ward, t("places.ward"))}
      </p>
      <GoogleStyleRating
        className="place-detail__rating"
        rating={place.averageRating}
        reviewCount={place.reviewCount}
        showReviewsLabel
        size="md"
      />

      <p className="place-detail__hint">{t("detail.playHint")}</p>
      {loadingText && <p className="place-detail__hint">{t("detail.translating")}</p>}
      {isFallback && !loadingText && (
        <p className="place-detail__hint">{t("detail.fallbackLang")}</p>
      )}

      {place.imageUrl && (
        <img className="place-detail__image" src={place.imageUrl} alt={place.name} />
      )}

      <dl className="place-detail__facts">
        <div>
          <dt>{t("places.ward")}</dt>
          <dd>{place.ward}</dd>
        </div>
        <div>
          <dt>{t("detail.address")}</dt>
          <dd>{place.address}</dd>
        </div>
        <div>
          <dt>{t("detail.openingHours")}</dt>
          <dd>{facts.hours || "—"}</dd>
        </div>
        <div>
          <dt>{t("detail.ticketPrice")}</dt>
          <dd>{facts.tickets || "—"}</dd>
        </div>
      </dl>

      <p className="place-detail__description">
        {description || t("detail.noDescription")}
      </p>
    </article>
  );
}

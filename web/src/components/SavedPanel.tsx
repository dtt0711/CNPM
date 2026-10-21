import { FormEvent, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import type { FavoriteDto, TourDto } from "../api/types";
import { useAuth } from "../auth/AuthContext";

export interface SavedPanelProps {
  onSelectPlace: (placeId: string) => void;
  userPosition: { lat: number; lng: number } | null;
}

type Tab = "favorites" | "tours";

export function SavedPanel({ onSelectPlace, userPosition }: SavedPanelProps) {
  const { t } = useTranslation();
  const { session } = useAuth();
  const [tab, setTab] = useState<Tab>("favorites");
  const [favorites, setFavorites] = useState<FavoriteDto[]>([]);
  const [tours, setTours] = useState<TourDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [composing, setComposing] = useState(false);
  const [tourName, setTourName] = useState("");
  const [picked, setPicked] = useState<string[]>([]);
  const rootRef = useRef<HTMLDivElement>(null);

  const scrollPanelTop = () => {
    rootRef.current?.closest(".app__sidebar")?.scrollTo({ top: 0 });
  };

  const reload = async () => {
    if (!session) return;
    const [favs, savedTours] = await Promise.all([api.getFavorites(), api.getTours()]);
    setFavorites(favs);
    setTours(savedTours);
  };

  useEffect(() => {
    if (!session) return;
    let active = true;
    setError(null);
    void reload()
      .then(() => {
        if (!active) return;
      })
      .catch((err: unknown) => {
        if (!active) return;
        setError(err instanceof Error ? err.message : t("saved.error"));
      });
    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.userId]);

  if (!session) {
    return (
      <div className="saved-panel">
        <h2 className="place-list-title">{t("saved.title")}</h2>
        <p className="place-list-subtitle">{t("saved.login")}</p>
        <Link className="btn btn--accent" to="/login?from=/">
          {t("account.signIn")}
        </Link>
      </div>
    );
  }

  const togglePick = (placeId: string) => {
    setPicked((prev) => (prev.includes(placeId) ? prev.filter((id) => id !== placeId) : [...prev, placeId]));
  };

  const resetComposer = () => {
    setComposing(false);
    setTourName("");
    setPicked([]);
  };

  const startComposer = () => {
    setTab("tours");
    setComposing(true);
    setError(null);
    scrollPanelTop();
  };

  const onCreateTour = async (e: FormEvent) => {
    e.preventDefault();
    if (picked.length < 2) {
      setError(t("saved.needPlaces"));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await api.createTour({
        name: tourName.trim() || t("saved.untitledTour"),
        placeIds: picked,
        startLatitude: userPosition?.lat,
        startLongitude: userPosition?.lng,
      });
      resetComposer();
      await reload();
      setTab("tours");
      scrollPanelTop();
    } catch (err) {
      setError(err instanceof Error ? err.message : t("saved.error"));
    } finally {
      setBusy(false);
    }
  };

  const onDeleteTour = async (id: string) => {
    setBusy(true);
    setError(null);
    try {
      await api.deleteTour(id);
      await reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : t("saved.error"));
    } finally {
      setBusy(false);
    }
  };

  const canCreate = favorites.length >= 2;

  return (
    <div className="saved-panel" ref={rootRef}>
      <h2 className="place-list-title">{t("saved.title")}</h2>
      <div className="saved-panel__tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={tab === "favorites"}
          className={"saved-panel__tab" + (tab === "favorites" ? " saved-panel__tab--active" : "")}
          onClick={() => {
            setTab("favorites");
            scrollPanelTop();
          }}
        >
          {t("saved.favorites")}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === "tours"}
          className={"saved-panel__tab" + (tab === "tours" ? " saved-panel__tab--active" : "")}
          onClick={() => {
            setTab("tours");
            scrollPanelTop();
          }}
        >
          {t("saved.tours")}
        </button>
      </div>
      {error && <p className="saved-panel__error">{error}</p>}

      {tab === "favorites" &&
        (favorites.length === 0 ? (
          <p className="place-list-subtitle">{t("saved.emptyFavorites")}</p>
        ) : (
          <ul className="place-list">
            {favorites.map((fav) => (
              <li key={fav.placeId}>
                <button type="button" className="place-card" onClick={() => onSelectPlace(fav.placeId)}>
                  {fav.imageUrl ? (
                    <img className="place-card__image" src={fav.imageUrl} alt="" loading="lazy" />
                  ) : (
                    <div className="place-card__image place-card__image--placeholder" aria-hidden="true" />
                  )}
                  <div className="place-card__body">
                    <h3 className="place-card__title">{fav.placeName}</h3>
                    <span className="place-card__cta">{t("places.viewDetail")}</span>
                  </div>
                </button>
              </li>
            ))}
          </ul>
        ))}

      {tab === "tours" && (
        <>
          {composing ? (
            <form className="saved-panel__composer" onSubmit={(e) => void onCreateTour(e)}>
              <p className="saved-panel__hint">{t("saved.composerHint")}</p>
              {favorites.length === 0 ? (
                <p className="place-list-subtitle">{t("saved.emptyFavorites")}</p>
              ) : (
                <ul className="saved-panel__picker">
                  {favorites.map((fav) => {
                    const selected = picked.includes(fav.placeId);
                    const order = selected ? picked.indexOf(fav.placeId) + 1 : null;
                    return (
                      <li key={fav.placeId}>
                        <button
                          type="button"
                          className={"saved-panel__pick-row" + (selected ? " saved-panel__pick-row--on" : "")}
                          aria-pressed={selected}
                          onClick={() => togglePick(fav.placeId)}
                        >
                          <span className="saved-panel__pick-order" aria-hidden="true">
                            {order ?? ""}
                          </span>
                          <span className="saved-panel__pick-name">{fav.placeName}</span>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
              <p className="saved-panel__picked">{t("saved.pickedCount", { count: picked.length })}</p>
              <input
                type="text"
                value={tourName}
                onChange={(e) => setTourName(e.target.value)}
                placeholder={t("saved.tourName")}
                maxLength={120}
                aria-label={t("saved.tourName")}
              />
              <div className="saved-panel__composer-actions">
                <button type="submit" className="btn btn--accent" disabled={busy || picked.length < 2}>
                  {t("saved.createTour")}
                </button>
                <button type="button" className="btn btn--ghost" disabled={busy} onClick={resetComposer}>
                  {t("saved.cancelCreate")}
                </button>
              </div>
            </form>
          ) : (
            <>
              <div className="saved-panel__toolbar">
                <button
                  type="button"
                  className="btn btn--accent"
                  disabled={!canCreate}
                  onClick={startComposer}
                >
                  {t("saved.createTour")}
                </button>
                {!canCreate && <p className="saved-panel__hint">{t("saved.needMoreFavorites")}</p>}
              </div>
              {tours.length === 0 ? (
                <p className="place-list-subtitle">{t("saved.emptyTours")}</p>
              ) : (
                <ul className="saved-panel__tours">
                  {tours.map((tour) => (
                    <li key={tour.id} className="saved-panel__tour">
                      <div className="saved-panel__tour-head">
                        <div>
                          <strong>{tour.name}</strong>
                          <p className="saved-panel__tour-meta">
                            {t("saved.stopCount", { count: tour.stops.length })}
                          </p>
                        </div>
                        <button
                          type="button"
                          className="btn btn--ghost"
                          disabled={busy}
                          onClick={() => void onDeleteTour(tour.id)}
                        >
                          {t("saved.deleteTour")}
                        </button>
                      </div>
                      <ol className="saved-panel__stops">
                        {tour.stops.map((stop) => (
                          <li key={stop.id}>
                            <button
                              type="button"
                              className="saved-panel__stop"
                              onClick={() => onSelectPlace(stop.placeId)}
                            >
                              <span className="saved-panel__stop-num">{stop.order + 1}</span>
                              <span>{stop.placeName}</span>
                            </button>
                          </li>
                        ))}
                      </ol>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </>
      )}
    </div>
  );
}

import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { usePlaces } from "./hooks/usePlaces";
import { useNarrationEngine } from "./hooks/useNarrationEngine";
import { setUiLanguage, type UiLang } from "./i18n";
import { MapView } from "./components/MapView";
import { PlaceList } from "./components/PlaceList";
import { PlaceDetail } from "./components/PlaceDetail";
import { LanguageSelect } from "./components/LanguageSelect";
import { OfflineButton } from "./components/OfflineButton";

type ViewMode = "map" | "list";

export default function App() {
  const { t, i18n } = useTranslation();
  const lang = (i18n.language?.slice(0, 2) || "vi") as UiLang;

  const { places, loading, error, refetch } = usePlaces();
  const [selectedPlaceId, setSelectedPlaceId] = useState<string | null>(null);
  const [view, setView] = useState<ViewMode>("map");

  const {
    narration,
    tier,
    error: narrationError,
    geofenceStatus,
    enableGeofence,
    disableGeofence,
    playPlace,
    stop,
  } = useNarrationEngine(lang);

  const selectedPlace = useMemo(
    () => places.find((p) => p.id === selectedPlaceId) ?? null,
    [places, selectedPlaceId],
  );

  const geofenceActive = geofenceStatus === "watching";

  return (
    <div className="app">
      <header className="app__header">
        <div className="app__brand">
          <h1>{t("app.title")}</h1>
          <p className="app__tagline">{t("app.tagline")}</p>
        </div>

        <nav className="app__nav" aria-label="view switch">
          <button
            type="button"
            className={"app__nav-btn" + (view === "map" ? " app__nav-btn--active" : "")}
            onClick={() => setView("map")}
          >
            {t("nav.map")}
          </button>
          <button
            type="button"
            className={"app__nav-btn" + (view === "list" ? " app__nav-btn--active" : "")}
            onClick={() => setView("list")}
          >
            {t("nav.places")}
          </button>
        </nav>

        <div className="app__toolbar">
          <LanguageSelect value={lang} onChange={setUiLanguage} />
        </div>
      </header>

      {narration && (
        <div className="app__banner">
          {t("geofence.newNarration", { name: narration.name ?? narration.placeId })}
        </div>
      )}
      {narrationError && (
        <div className="app__banner app__banner--error">{narrationError}</div>
      )}

      <main className="app__body">
        <section className="app__sidebar">
          {selectedPlace ? (
            <PlaceDetail
              place={selectedPlace}
              lang={lang}
              onBack={() => setSelectedPlaceId(null)}
              narrationProps={{ narration, tier, error: narrationError, onPlay: playPlace, onStop: stop }}
            />
          ) : (
            <>
              <h2 className="place-list-title">{t("places.title")}</h2>
              <PlaceList
                places={places}
                loading={loading}
                error={error}
                selectedPlaceId={selectedPlaceId}
                onSelectPlace={setSelectedPlaceId}
                onRetry={refetch}
              />
            </>
          )}
        </section>

        {view === "map" && (
          <section className="app__map-pane">
            <MapView
              places={places}
              selectedPlaceId={selectedPlaceId}
              onSelectPlace={setSelectedPlaceId}
              userPosition={null}
            />
          </section>
        )}
      </main>

      <footer className="app__footer-bar">
        <button
          type="button"
          className="btn btn--ghost"
          onClick={() => (geofenceActive ? disableGeofence() : enableGeofence())}
        >
          {geofenceActive ? t("geofence.disable") : t("geofence.enable")}
        </button>
        <span className="app__status">
          {geofenceStatus === "watching" && t("geofence.watching")}
          {geofenceStatus === "idle" && t("geofence.disabled")}
          {geofenceStatus === "unsupported" && t("geofence.unsupported")}
          {geofenceStatus === "denied" && t("geofence.permissionDenied")}
        </span>

        <OfflineButton lang={lang} />
      </footer>
    </div>
  );
}

import { useEffect, useMemo, useState } from "react";
import { AccountMenu } from "../components/AccountMenu";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import { usePlaces } from "../hooks/usePlaces";
import { useNarrationEngine } from "../hooks/useNarrationEngine";
import { mergeUiBundleForLocale, setUiLanguage, type UiLang } from "../i18n";
import { MapView } from "../components/MapView";
import { PlaceList } from "../components/PlaceList";
import { PlaceDetail } from "../components/PlaceDetail";
import { AudioControls } from "../components/AudioControls";
import { LanguageSelect } from "../components/LanguageSelect";
import { OfflineButton } from "../components/OfflineButton";
import { MapModeSelect } from "../components/MapModeSelect";
import { SavedPanel } from "../components/SavedPanel";
import { AnalyticsConsentBanner } from "../components/AnalyticsConsentBanner";
import { NetworkToast } from "../components/NetworkToast";
import { useInternetStatus } from "../hooks/useInternetStatus";
import { loadMapMode, saveMapMode, type MapMode } from "../maps/mapConfig";
import { formatDistanceKm } from "../geo/distance";

type ViewMode = "map" | "list" | "saved";

export default function MapApp() {
  const { t, i18n } = useTranslation();
  const lang = (i18n.language?.slice(0, 2) || "vi") as UiLang;

  const { places, loading, error, refetch } = usePlaces();
  const [selectedPlaceId, setSelectedPlaceId] = useState<string | null>(null);
  const [view, setView] = useState<ViewMode>("map");
  const [mapMode, setMapMode] = useState<MapMode>(() => loadMapMode());
  const [offlinePackReady, setOfflinePackReady] = useState<boolean | null>(null);
  const internetOnline = useInternetStatus();

  const {
    narration,
    source,
    tier,
    mediaUrl,
    error: narrationError,
    geofenceStatus,
    userPosition,
    nearbyHint,
    enableGeofence,
    disableGeofence,
    playPlace,
    stop,
  } = useNarrationEngine(lang);

  const selectedPlace = useMemo(
    () => places.find((p) => p.id === selectedPlaceId) ?? null,
    [places, selectedPlaceId],
  );

  const geofenceActive = geofenceStatus === "watching" || geofenceStatus === "locating";

  useEffect(() => {
    void mergeUiBundleForLocale(lang);
  }, [lang]);

  // Warm translate+TTS + hotset for nearest places when language changes (real GPS only).
  useEffect(() => {
    if (!places.length || lang === "vi" || !userPosition) return;
    const ids = [...places]
      .map((p) => ({
        id: p.id,
        d: Math.abs(p.latitude - userPosition.lat) + Math.abs(p.longitude - userPosition.lng),
      }))
      .sort((a, b) => a.d - b.d)
      .slice(0, 5)
      .map((x) => x.id);
    void api.localizationsWarmup({ lang, placeIds: ids }).catch(() => undefined);
    void api
      .localizationsPrepareHotset({
        lat: userPosition.lat,
        lng: userPosition.lng,
        lang,
      })
      .catch(() => undefined);
  }, [lang, places, userPosition]);

  useEffect(() => {
    if (source !== "geofence" || !narration?.placeId) return;
    // Set selection directly — do NOT call selectPlace() (that stops playback).
    setSelectedPlaceId(narration.placeId);
    setView("map");
  }, [source, narration?.placeId]);

  const handleMapModeChange = (mode: MapMode) => {
    setMapMode(mode);
    saveMapMode(mode);
    if (mode !== "offline") setOfflinePackReady(null);
  };

  const openMap = () => {
    setView("map");
  };

  const openList = () => {
    setView("list");
    setSelectedPlaceId(null);
  };

  const openSaved = () => {
    setView("saved");
    setSelectedPlaceId(null);
  };

  const selectPlace = (placeId: string) => {
    // Manual open: clear any previous geofence/play banner so it doesn't look like
    // "detected near you" for the place the user just tapped.
    if (narration || source) {
      stop();
    }
    setSelectedPlaceId(placeId);
  };

  // Once geofence already detected a place and started narration, hide the
  // distance/status strip — the "Phát hiện điểm…" banner is enough.
  const geofenceNarrationActive = source === "geofence" && Boolean(narration?.placeId);

  const geofenceStatusText = (() => {
    if (geofenceNarrationActive) return null;

    if (geofenceStatus === "locating") return t("geofence.locating");
    if (geofenceStatus === "unsupported") return t("geofence.unsupported");
    if (geofenceStatus === "denied") return t("geofence.permissionDenied");
    if (geofenceStatus === "error") return t("geofence.error");

    if (tier === "loading" || tier === "poll-task") {
      const name = narration?.name ?? nearbyHint?.name;
      if (name) {
        return t("geofence.inRange", {
          name,
          distance: formatDistanceKm(nearbyHint?.distanceMeters ?? 0),
        });
      }
    }
    if (nearbyHint?.inRange && nearbyHint.cooldownActive) {
      return t("geofence.cooldown", { name: nearbyHint.name });
    }
    if (nearbyHint?.inRange) {
      return t("geofence.inRange", {
        name: nearbyHint.name,
        distance: formatDistanceKm(nearbyHint.distanceMeters),
      });
    }
    if (nearbyHint) {
      return t("geofence.nearest", {
        name: nearbyHint.name,
        distance: formatDistanceKm(nearbyHint.distanceMeters),
        radius: nearbyHint.triggerRadiusMeters,
      });
    }
    if (userPosition) return t("geofence.watchingNoNearby");
    if (geofenceStatus === "watching") return t("geofence.watching");
    return t("geofence.disabled");
  })();

  const mapVisible = view !== "list";

  return (
    <div
      className={
        "app" +
        (selectedPlace ? " app--place-open" : "") +
        (view === "list" ? " app--list" : " app--map") +
        (view === "saved" ? " app--saved" : "")
      }
    >
      <header className="app__header">
        <div className="app__brand">
          <h1>{t("app.title")}</h1>
          <p className="app__tagline">{t("app.tagline")}</p>
        </div>

        <nav className="app__nav" aria-label="view switch">
          <button
            type="button"
            className={"app__nav-btn" + (view === "map" ? " app__nav-btn--active" : "")}
            onClick={openMap}
          >
            {t("nav.map")}
          </button>
          <button
            type="button"
            className={"app__nav-btn" + (view === "list" ? " app__nav-btn--active" : "")}
            onClick={openList}
          >
            {t("nav.places")}
          </button>
          <button
            type="button"
            className={"app__nav-btn" + (view === "saved" ? " app__nav-btn--active" : "")}
            onClick={openSaved}
          >
            {t("nav.saved")}
          </button>
        </nav>

        <div className="app__toolbar">
          <MapModeSelect value={mapMode} onChange={handleMapModeChange} />
          <LanguageSelect value={lang} onChange={setUiLanguage} />
          <AccountMenu />
        </div>
      </header>

      <NetworkToast online={internetOnline} />

      <AnalyticsConsentBanner />

      {mapMode === "offline" && offlinePackReady === false && (
        <div className="app__banner app__banner--warn">
          {t("map.offlinePackMissing")}
        </div>
      )}

      {(geofenceActive || userPosition) && geofenceStatusText && (
        <div className="app__banner app__banner--geo">{geofenceStatusText}</div>
      )}

      {/* Only for GPS/geofence triggers — not when the user merely opens a place or presses play. */}
      {source === "geofence" && narration?.placeId && (
        <div className="app__banner">
          {t("geofence.newNarration", { name: narration.name ?? narration.placeId })}
          {tier === "loading" || tier === "poll-task"
            ? ` · ${t("audio.status.loading")}`
            : tier === "ready-url" || tier === "web-speech" || tier === "tts-stream"
              ? ` · ${t(`audio.status.${tier}` as const)}`
              : null}
        </div>
      )}
      {narrationError && (
        <div className="app__banner app__banner--error">{narrationError}</div>
      )}

      <main className={"app__body app__body--" + (view === "list" ? "list" : view === "saved" ? "saved" : "map")}>
        <section className="app__sidebar">
          {selectedPlace ? (
            <PlaceDetail
              place={selectedPlace}
              lang={lang}
              onBack={() => setSelectedPlaceId(null)}
            />
          ) : view === "saved" ? (
            <SavedPanel onSelectPlace={selectPlace} userPosition={userPosition} />
          ) : (
            <>
              <h2 className="place-list-title">{t("places.title")}</h2>
              {geofenceActive && (
                <p className="place-list-subtitle">{t("places.sortedByDistance")}</p>
              )}
              <PlaceList
                places={places}
                loading={loading}
                error={error}
                selectedPlaceId={selectedPlaceId}
                onSelectPlace={selectPlace}
                onRetry={refetch}
                origin={geofenceActive ? userPosition : null}
              />
            </>
          )}
        </section>

        <section
          className={"app__map-pane" + (view === "list" ? " app__map-pane--parked" : "")}
          aria-hidden={!mapVisible}
        >
          <MapView
            places={places}
            selectedPlaceId={selectedPlaceId}
            onSelectPlace={selectPlace}
            userPosition={userPosition}
            mapMode={mapMode}
            active={mapVisible}
            followUser={geofenceActive && !selectedPlaceId && view === "map"}
            onOfflinePackReady={setOfflinePackReady}
            internetOnline={internetOnline}
          />
        </section>
      </main>

      {(selectedPlace || (source === "geofence" && narration?.placeId)) && (
        <div className="app__play-dock" role="region" aria-label={t("places.playNarration")}>
          <p className="app__play-dock-title">
            {selectedPlace?.name ?? narration?.name ?? t("places.playNarration")}
          </p>
          <AudioControls
            placeId={selectedPlace?.id ?? narration!.placeId}
            placeName={selectedPlace?.name ?? narration?.name ?? narration!.placeId}
            narration={narration}
            tier={tier}
            mediaUrl={mediaUrl}
            error={narrationError}
            onPlay={playPlace}
            onStop={stop}
          />
        </div>
      )}

      <footer className="app__footer-bar">
        <button
          type="button"
          className="btn btn--ghost"
          onClick={() => {
            if (geofenceActive) {
              disableGeofence();
            } else {
              enableGeofence();
              setView("map");
            }
          }}
        >
          {geofenceActive ? t("geofence.disable") : t("geofence.enable")}
        </button>
        <span className="app__status">{geofenceStatusText}</span>

        <OfflineButton lang={lang} />
      </footer>
    </div>
  );
}

import { useEffect, useRef, useState } from "react";
import maplibregl, { Map as MapLibreMap, Marker, Popup } from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import { useTranslation } from "react-i18next";
import type { PlaceDto } from "../api/types";
import type { MapMode } from "../maps/mapConfig";
import { ensurePmtilesProtocol } from "../maps/pmtilesProtocol";
import {
  isBrowserOnline,
  resolveCloudMapStyle,
  resolveInitialMapStyle,
  resolveOfflineMapStyle,
} from "../maps/resolveMapStyle";
import { formatWard } from "../places/ward";

const HCMC_CENTER: [number, number] = [106.700981, 10.776889];

export interface MapViewProps {
  places: PlaceDto[];
  selectedPlaceId: string | null;
  onSelectPlace: (placeId: string) => void;
  userPosition: { lat: number; lng: number } | null;
  mapMode?: MapMode;
  /** When false the map stays mounted but parked; call resize when it becomes true again. */
  active?: boolean;
  /** Pan/zoom to the user marker when GPS updates. */
  followUser?: boolean;
  /** Fired when offline mode resolves whether local PMTiles pack is available. */
  onOfflinePackReady?: (ready: boolean) => void;
  /** Shared Internet probe from MapApp (`null` while first check runs). */
  internetOnline?: boolean | null;
}

function desiredBasemap(mapMode: MapMode, internetOnline: boolean | null): "cloud" | "offline" {
  const cloudDown =
    mapMode === "hybrid" &&
    (internetOnline === false || (internetOnline === null && !isBrowserOnline()));
  return mapMode === "offline" || cloudDown ? "offline" : "cloud";
}

export function MapView({
  places,
  selectedPlaceId,
  onSelectPlace,
  userPosition,
  mapMode = "cloud",
  active = true,
  followUser = false,
  onOfflinePackReady,
  internetOnline = null,
}: MapViewProps) {
  const { t, i18n } = useTranslation();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const markersRef = useRef<Map<string, Marker>>(new Map());
  const userMarkerRef = useRef<Marker | null>(null);
  const onSelectRef = useRef(onSelectPlace);
  const placesRef = useRef(places);
  const userPositionRef = useRef(userPosition);
  const selectedPlaceIdRef = useRef(selectedPlaceId);
  const hybridUsingOfflineRef = useRef(false);
  const mapModeRef = useRef(mapMode);
  const appliedBasemapRef = useRef<"cloud" | "offline" | null>(null);
  const onOfflinePackReadyRef = useRef(onOfflinePackReady);
  const wardWordRef = useRef(t("places.ward"));
  /** One recenter per location-on session — GPS ticks must not steal the camera. */
  const didRecenterOnFollowRef = useRef(false);
  const [mapEpoch, setMapEpoch] = useState(0);

  onSelectRef.current = onSelectPlace;
  placesRef.current = places;
  userPositionRef.current = userPosition;
  selectedPlaceIdRef.current = selectedPlaceId;
  mapModeRef.current = mapMode;
  onOfflinePackReadyRef.current = onOfflinePackReady;
  wardWordRef.current = t("places.ward");

  const syncPlaceMarkers = (map: MapLibreMap) => {
    const list = placesRef.current;
    const existing = markersRef.current;
    const nextIds = new Set(list.map((p) => p.id));

    for (const [id, marker] of existing) {
      const detached = !marker.getElement().isConnected;
      if (!nextIds.has(id) || detached) {
        marker.remove();
        existing.delete(id);
      }
    }

    list.forEach((place, index) => {
      const order = index + 1;
      let marker = existing.get(place.id);

      if (!marker) {
        const el = document.createElement("button");
        el.type = "button";
        el.className = "map-pin";
        el.setAttribute("aria-label", place.name);
        el.innerHTML = `<span class="map-pin__badge">${order}</span><span class="map-pin__label">${escapeHtml(place.name)}</span>`;
        el.addEventListener("click", (evt) => {
          evt.stopPropagation();
          onSelectRef.current(place.id);
        });

        const popup = new Popup({ offset: 18, closeButton: false, maxWidth: "240px" }).setHTML(
          placePopupHtml(place, wardWordRef.current),
        );

        marker = new maplibregl.Marker({ element: el, anchor: "bottom" })
          .setLngLat([place.longitude, place.latitude])
          .setPopup(popup)
          .addTo(map);

        existing.set(place.id, marker);
      } else {
        marker.setLngLat([place.longitude, place.latitude]);
        marker.getPopup()?.setHTML(placePopupHtml(place, wardWordRef.current));
        const badge = marker.getElement().querySelector(".map-pin__badge");
        if (badge) badge.textContent = String(order);
      }

      marker.getElement().classList.toggle("map-pin--active", place.id === selectedPlaceIdRef.current);
    });
  };

  const syncUserMarker = (map: MapLibreMap) => {
    const pos = userPositionRef.current;
    if (!pos) return;

    if (!userMarkerRef.current || !userMarkerRef.current.getElement().isConnected) {
      userMarkerRef.current?.remove();
      const el = document.createElement("div");
      el.className = "map-pin map-pin--user";
      el.title = "You";
      userMarkerRef.current = new maplibregl.Marker({ element: el, anchor: "center" })
        .setLngLat([pos.lng, pos.lat])
        .addTo(map);
      return;
    }

    userMarkerRef.current.setLngLat([pos.lng, pos.lat]);
  };

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;

    let cancelled = false;
    ensurePmtilesProtocol();

    void (async () => {
      const { style, offlinePackReady } = await resolveInitialMapStyle(mapModeRef.current);
      if (cancelled || !containerRef.current) return;

      if (mapModeRef.current === "offline" && offlinePackReady !== null) {
        onOfflinePackReadyRef.current?.(offlinePackReady);
      }

      const map = new maplibregl.Map({
        container: containerRef.current,
        style,
        center: HCMC_CENTER,
        zoom: 12.2,
      });

      map.addControl(new maplibregl.NavigationControl({ showCompass: true }), "top-right");
      map.addControl(new maplibregl.ScaleControl({ maxWidth: 120 }), "bottom-left");
      mapRef.current = map;
      appliedBasemapRef.current = desiredBasemap(mapModeRef.current, isBrowserOnline() ? true : false);
      hybridUsingOfflineRef.current = appliedBasemapRef.current === "offline";

      const attachOverlays = () => {
        syncPlaceMarkers(map);
        syncUserMarker(map);
        const list = placesRef.current;
        if (list.length === 0 || selectedPlaceIdRef.current) return;
        const bounds = new maplibregl.LngLatBounds();
        for (const p of list) bounds.extend([p.longitude, p.latitude]);
        map.fitBounds(bounds, { padding: 56, maxZoom: 13.5, duration: 0 });
      };

      map.on("load", attachOverlays);
      map.on("style.load", () => {
        syncPlaceMarkers(map);
        syncUserMarker(map);
      });

      map.on("error", () => {
        if (mapModeRef.current !== "hybrid" || hybridUsingOfflineRef.current) return;
        hybridUsingOfflineRef.current = true;
        void resolveOfflineMapStyle().then((result) => {
          if (mapRef.current !== map || mapModeRef.current !== "hybrid") return;
          appliedBasemapRef.current = "offline";
          map.setStyle(result.style);
        });
      });

      if (!cancelled) setMapEpoch((n) => n + 1);
    })();

    return () => {
      cancelled = true;
      for (const marker of markersRef.current.values()) marker.remove();
      markersRef.current.clear();
      userMarkerRef.current?.remove();
      userMarkerRef.current = null;
      mapRef.current?.remove();
      mapRef.current = null;
      appliedBasemapRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    const next = desiredBasemap(mapMode, internetOnline);
    if (appliedBasemapRef.current === next) return;

    void (async () => {
      if (next === "offline") {
        hybridUsingOfflineRef.current = true;
        const result = await resolveOfflineMapStyle();
        if (mapRef.current !== map) return;
        onOfflinePackReadyRef.current?.(result.packReady);
        appliedBasemapRef.current = "offline";
        map.setStyle(result.style);
        return;
      }
      hybridUsingOfflineRef.current = false;
      appliedBasemapRef.current = "cloud";
      map.setStyle(resolveCloudMapStyle());
    })();
  }, [mapMode, internetOnline, mapEpoch]);

  useEffect(() => {
    if (!active) return;
    const map = mapRef.current;
    if (!map) return;

    const resize = () => {
      map.resize();
      map.triggerRepaint();
    };
    const raf = window.requestAnimationFrame(resize);
    const timer = window.setTimeout(resize, 120);
    window.addEventListener("resize", resize);

    return () => {
      window.cancelAnimationFrame(raf);
      window.clearTimeout(timer);
      window.removeEventListener("resize", resize);
    };
  }, [active, mapEpoch]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    syncPlaceMarkers(map);

    if (places.length > 0 && map.isStyleLoaded() && !selectedPlaceId && !followUser) {
      const bounds = new maplibregl.LngLatBounds();
      for (const p of places) bounds.extend([p.longitude, p.latitude]);
      map.fitBounds(bounds, { padding: 56, maxZoom: 13.5, duration: 600 });
    }
  }, [places, selectedPlaceId, mapEpoch, i18n.language, followUser]);

  useEffect(() => {
    for (const [id, marker] of markersRef.current) {
      marker.getElement().classList.toggle("map-pin--active", id === selectedPlaceId);
    }
    if (!selectedPlaceId || !mapRef.current) return;
    const place = places.find((p) => p.id === selectedPlaceId);
    if (place) {
      mapRef.current.flyTo({ center: [place.longitude, place.latitude], zoom: 15.2, duration: 700 });
      markersRef.current.get(selectedPlaceId)?.togglePopup();
    }
  }, [selectedPlaceId, places, mapEpoch]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !userPosition) return;
    syncUserMarker(map);

    if (!followUser || !active) {
      didRecenterOnFollowRef.current = false;
      return;
    }

    if (didRecenterOnFollowRef.current) return;
    didRecenterOnFollowRef.current = true;
    map.easeTo({
      center: [userPosition.lng, userPosition.lat],
      zoom: Math.max(map.getZoom(), 14),
      duration: 700,
    });
  }, [userPosition, followUser, active, mapEpoch]);

  return (
    <div className="map-view">
      <div ref={containerRef} className="map-view__canvas" aria-label={t("map.loading")} />
    </div>
  );
}

function placePopupHtml(place: PlaceDto, wardWord: string): string {
  const ward = formatWard(place.ward, wardWord);
  return `<strong>${escapeHtml(place.name)}</strong><br/><span class="map-popup-meta">${escapeHtml(ward)} · ${escapeHtml(place.category)}</span>`;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

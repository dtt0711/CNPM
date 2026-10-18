import { useEffect, useRef } from "react";
import maplibregl, { Map as MapLibreMap, Marker, Popup } from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import { useTranslation } from "react-i18next";
import type { PlaceDto } from "../api/types";

/** Clear street map (OpenFreeMap Liberty) — readable labels for HCMC. */
const DEFAULT_STYLE_URL = "https://tiles.openfreemap.org/styles/liberty";
const HCMC_CENTER: [number, number] = [106.700981, 10.776889];

export interface MapViewProps {
  places: PlaceDto[];
  selectedPlaceId: string | null;
  onSelectPlace: (placeId: string) => void;
  userPosition: { lat: number; lng: number } | null;
}

function resolveMapStyle(): string | maplibregl.StyleSpecification {
  const configured = (import.meta.env.VITE_MAP_TILE_URL as string | undefined)?.trim();
  if (!configured) return DEFAULT_STYLE_URL;

  // Full MapLibre style JSON URL
  if (configured.includes("style") || configured.endsWith(".json")) {
    return configured;
  }

  // Raster tile template e.g. https://tile.openstreetmap.org/{z}/{x}/{y}.png
  if (configured.includes("{z}") && configured.includes("{x}")) {
    return {
      version: 8,
      sources: {
        osm: {
          type: "raster",
          tiles: [configured],
          tileSize: 256,
          attribution: "© OpenStreetMap",
        },
      },
      layers: [{ id: "osm", type: "raster", source: "osm" }],
    };
  }

  return configured;
}

export function MapView({ places, selectedPlaceId, onSelectPlace, userPosition }: MapViewProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const markersRef = useRef<Map<string, Marker>>(new Map());
  const userMarkerRef = useRef<Marker | null>(null);
  const onSelectRef = useRef(onSelectPlace);
  const placesRef = useRef(places);
  onSelectRef.current = onSelectPlace;
  placesRef.current = places;

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;

    const map = new maplibregl.Map({
      container: containerRef.current,
      style: resolveMapStyle(),
      center: HCMC_CENTER,
      zoom: 12.2,
    });

    map.addControl(new maplibregl.NavigationControl({ showCompass: true }), "top-right");
    map.addControl(new maplibregl.ScaleControl({ maxWidth: 120 }), "bottom-left");
    mapRef.current = map;

    map.on("load", () => {
      // Fit all places once markers exist
      const list = placesRef.current;
      if (list.length > 0) {
        const bounds = new maplibregl.LngLatBounds();
        for (const p of list) bounds.extend([p.longitude, p.latitude]);
        map.fitBounds(bounds, { padding: 56, maxZoom: 13.5, duration: 0 });
      }
    });

    return () => {
      map.remove();
      mapRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    const existing = markersRef.current;
    const nextIds = new Set(places.map((p) => p.id));

    for (const [id, marker] of existing) {
      if (!nextIds.has(id)) {
        marker.remove();
        existing.delete(id);
      }
    }

    places.forEach((place, index) => {
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
          `<strong>${escapeHtml(place.name)}</strong><br/><span class="map-popup-meta">${escapeHtml(place.district)} · ${escapeHtml(place.category)}</span>`,
        );

        marker = new maplibregl.Marker({ element: el, anchor: "bottom" })
          .setLngLat([place.longitude, place.latitude])
          .setPopup(popup)
          .addTo(map);

        existing.set(place.id, marker);
      } else {
        marker.setLngLat([place.longitude, place.latitude]);
        const badge = marker.getElement().querySelector(".map-pin__badge");
        if (badge) badge.textContent = String(order);
      }
    });

    if (places.length > 0 && map.isStyleLoaded()) {
      const bounds = new maplibregl.LngLatBounds();
      for (const p of places) bounds.extend([p.longitude, p.latitude]);
      if (!selectedPlaceId) {
        map.fitBounds(bounds, { padding: 56, maxZoom: 13.5, duration: 600 });
      }
    }
  }, [places, selectedPlaceId]);

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
  }, [selectedPlaceId, places]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !userPosition) return;

    if (!userMarkerRef.current) {
      const el = document.createElement("div");
      el.className = "map-pin map-pin--user";
      el.title = "You";
      userMarkerRef.current = new maplibregl.Marker({ element: el, anchor: "center" })
        .setLngLat([userPosition.lng, userPosition.lat])
        .addTo(map);
    } else {
      userMarkerRef.current.setLngLat([userPosition.lng, userPosition.lat]);
    }
  }, [userPosition]);

  return (
    <div className="map-view">
      <div ref={containerRef} className="map-view__canvas" aria-label={t("map.loading")} />
    </div>
  );
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

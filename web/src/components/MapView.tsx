import { useEffect, useRef } from "react";
import maplibregl, { Map as MapLibreMap, Marker } from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import { useTranslation } from "react-i18next";
import type { PlaceDto } from "../api/types";

const DEFAULT_STYLE_URL = "https://demotiles.maplibre.org/style.json";
const HCMC_CENTER: [number, number] = [106.700981, 10.776889]; // Ben Thanh market area.

export interface MapViewProps {
  places: PlaceDto[];
  selectedPlaceId: string | null;
  onSelectPlace: (placeId: string) => void;
  userPosition: { lat: number; lng: number } | null;
}

export function MapView({ places, selectedPlaceId, onSelectPlace, userPosition }: MapViewProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const markersRef = useRef<Map<string, Marker>>(new Map());
  const userMarkerRef = useRef<Marker | null>(null);
  const onSelectRef = useRef(onSelectPlace);
  onSelectRef.current = onSelectPlace;

  // Create the map instance once.
  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;

    const tileUrl = import.meta.env.VITE_MAP_TILE_URL || DEFAULT_STYLE_URL;

    const map = new maplibregl.Map({
      container: containerRef.current,
      style: tileUrl,
      center: HCMC_CENTER,
      zoom: 12.5,
    });

    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), "top-right");
    mapRef.current = map;

    return () => {
      map.remove();
      mapRef.current = null;
    };
  }, []);

  // Sync place markers whenever the list changes.
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

    for (const place of places) {
      if (existing.has(place.id)) continue;

      const el = document.createElement("button");
      el.type = "button";
      el.className = "map-pin";
      el.setAttribute("aria-label", place.name);
      el.innerHTML = `<span class="map-pin__dot"></span>`;
      el.addEventListener("click", (evt) => {
        evt.stopPropagation();
        onSelectRef.current(place.id);
      });

      const marker = new maplibregl.Marker({ element: el, anchor: "bottom" })
        .setLngLat([place.longitude, place.latitude])
        .addTo(map);

      existing.set(place.id, marker);
    }
  }, [places]);

  // Highlight the selected place and fly to it.
  useEffect(() => {
    for (const [id, marker] of markersRef.current) {
      marker.getElement().classList.toggle("map-pin--active", id === selectedPlaceId);
    }
    if (!selectedPlaceId || !mapRef.current) return;
    const place = places.find((p) => p.id === selectedPlaceId);
    if (place) {
      mapRef.current.flyTo({ center: [place.longitude, place.latitude], zoom: 15.5, duration: 700 });
    }
  }, [selectedPlaceId, places]);

  // Track the user's live location marker.
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !userPosition) return;

    if (!userMarkerRef.current) {
      const el = document.createElement("div");
      el.className = "map-pin map-pin--user";
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

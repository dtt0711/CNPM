import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "../api/client";
import type { PlaceDto } from "../api/types";

export interface UsePlacesState {
  places: PlaceDto[];
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

/** Loads places from GET /api/v1/places (first page, generous page size for the map/list view). */
export function usePlaces(pageSize = 100): UsePlacesState {
  const [places, setPlaces] = useState<PlaceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const reloadToken = useRef(0);

  const refetch = useCallback(() => {
    reloadToken.current += 1;
    setLoading(true);
    setError(null);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;

    setLoading(true);
    setError(null);

    api
      .listPlaces({ page: 1, pageSize, status: "Published" }, controller.signal)
      .then((result) => {
        if (!active) return;
        setPlaces(result.items);
      })
      .catch((err: unknown) => {
        if (!active) return;
        if (err instanceof DOMException && err.name === "AbortError") return;
        setError(err instanceof Error ? err.message : "Failed to load places");
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
      controller.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageSize, reloadToken.current]);

  return { places, loading, error, refetch };
}

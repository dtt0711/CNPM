import { getOrCreateClientId } from "./clientId";
import type {
  AudioTaskDto,
  LanguageInfoDto,
  NearbyPlaceDto,
  OfflinePackManifest,
  PagedResult,
  PlaceDetailDto,
  PlaceDto,
  PlaceQueryParams,
  TriggerRequestBody,
  TriggerResponse,
} from "./types";

// Base URL of the backend API. Empty string means "same origin" which, in
// dev, goes through the Vite proxy configured in vite.config.ts (/api -> backend).
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/+$/, "");
const API_PREFIX = `${API_BASE_URL}/api/v1`;

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
}

function buildQuery(params: object): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params as Record<string, unknown>)) {
    if (value === undefined || value === null || value === "") continue;
    if (Array.isArray(value)) {
      for (const v of value) search.append(key, String(v));
    } else {
      search.set(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

async function request<T>(
  path: string,
  init: RequestInit = {},
  signal?: AbortSignal,
): Promise<T> {
  const clientId = getOrCreateClientId();
  const headers = new Headers(init.headers);
  headers.set("X-Client-Id", clientId);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const res = await fetch(`${API_PREFIX}${path}`, { ...init, headers, signal });

  if (res.status === 204) {
    return null as T;
  }

  const contentType = res.headers.get("content-type") ?? "";
  const isJson = contentType.includes("application/json");
  const payload = isJson ? await res.json().catch(() => null) : await res.text();

  if (!res.ok) {
    const message =
      (isJson && payload && typeof payload === "object" && "message" in payload
        ? String((payload as { message?: unknown }).message)
        : null) ?? `Request failed with status ${res.status}`;
    throw new ApiError(res.status, message);
  }

  return payload as T;
}

export const api = {
  clientId: () => getOrCreateClientId(),

  /** GET /api/v1/places - paged list/search of places. */
  listPlaces(params: PlaceQueryParams = {}, signal?: AbortSignal) {
    return request<PagedResult<PlaceDto>>(`/places${buildQuery(params)}`, {}, signal);
  },

  /** GET /api/v1/places/nearby */
  nearbyPlaces(
    params: { lat: number; lng: number; radiusMeters?: number; limit?: number },
    signal?: AbortSignal,
  ) {
    return request<NearbyPlaceDto[]>(`/places/nearby${buildQuery(params)}`, {}, signal);
  },

  /** GET /api/v1/places/{id} */
  getPlace(id: string, signal?: AbortSignal) {
    return request<PlaceDetailDto>(`/places/${id}`, {}, signal);
  },

  /** GET /api/v1/places/{id}/narration?lang= */
  getNarration(id: string, lang: string, signal?: AbortSignal) {
    return request(`/places/${id}/narration${buildQuery({ lang })}`, {}, signal) as ReturnType<
      typeof request<import("./types").NarrationResponse>
    >;
  },

  /** POST /api/v1/places/trigger - geofence evaluation for current position. */
  trigger(body: TriggerRequestBody, signal?: AbortSignal) {
    return request<TriggerResponse | null>(
      "/places/trigger",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    );
  },

  /** GET /api/v1/audio/languages */
  getLanguages(signal?: AbortSignal) {
    return request<LanguageInfoDto[]>("/audio/languages", {}, signal);
  },

  /** GET /api/v1/audio/tasks/{placeId}?lang= - poll generation status. */
  getAudioTaskStatus(placeId: string, lang: string, signal?: AbortSignal) {
    return request<AudioTaskDto | null>(
      `/audio/tasks/${placeId}${buildQuery({ lang })}`,
      {},
      signal,
    );
  },

  /** POST /api/v1/audio/tts - request/trigger generation; may return a task
   * (JSON) or, on backends that support it, a streamed audio response. */
  async requestTts(
    body: { placeId: string; lang: string },
    signal?: AbortSignal,
  ): Promise<{ kind: "task"; task: AudioTaskDto } | { kind: "stream"; blob: Blob }> {
    const clientId = getOrCreateClientId();
    const res = await fetch(`${API_PREFIX}/audio/tts`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Client-Id": clientId,
      },
      body: JSON.stringify(body),
      signal,
    });

    if (!res.ok) {
      throw new ApiError(res.status, `TTS request failed with status ${res.status}`);
    }

    const contentType = res.headers.get("content-type") ?? "";
    if (contentType.startsWith("audio/")) {
      const blob = await res.blob();
      return { kind: "stream", blob };
    }

    const task = (await res.json()) as AudioTaskDto;
    return { kind: "task", task };
  },

  /** GET /api/v1/offline/pack?langs=vi&langs=en - offline manifest for given languages. */
  getOfflinePack(langs: string[], signal?: AbortSignal) {
    return request<OfflinePackManifest>(`/offline/pack${buildQuery({ langs })}`, {}, signal);
  },
};

export function apiAudioUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  return `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`;
}

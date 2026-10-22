import { getOrCreateClientId } from "./clientId";
import { clearSession, getAccessToken, loadSession, saveSession } from "../auth/session";
import type {
  AdminUserDto,
  AiUsageDto,
  AnalyticsAdminSummaryDto,
  AnalyticsCollectRequest,
  AnalyticsPresenceRequest,
  AuditLogDto,
  AudioTaskDto,
  AuthResponse,
  CreateTourRequest,
  EnhanceDescriptionRequest,
  FavoriteDto,
  EnhanceDescriptionResponse,
  HotsetResult,
  LanguageInfoDto,
  LoginRequest,
  MapOfflineOptionsDto,
  MapPackManifestDto,
  NearbyPlaceDto,
  OfflinePackManifest,
  OnDemandLocalizationResult,
  PagedResult,
  PlaceDetailDto,
  PlaceDto,
  PlaceQueryParams,
  RefreshRequest,
  RegisterRequest,
  TourDto,
  TriggerRequestBody,
  TriggerResponse,
  UiBundleDto,
  UpdatePlaceRequest,
  UpdateTranslationRequest,
  AdminRoleDto,
  WarmupResult,
} from "./types";

// Base URL of the backend API. Empty string means "same origin" which, in
// dev, goes through the Vite proxy configured in vite.config.ts (/api -> backend).
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/+$/, "");
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

function authHeaders(headers: Headers): void {
  const token = getAccessToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
}

let refreshInFlight: Promise<boolean> | null = null;

/** Exchange refresh token for a new access token (single-flight). */
async function tryRefreshSession(): Promise<boolean> {
  const session = loadSession();
  if (!session?.refreshToken) return false;

  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      try {
        const res = await fetch(`${API_PREFIX}/auth/refresh`, {
          method: "POST",
          headers: { "Content-Type": "application/json", "X-Client-Id": getOrCreateClientId() },
          credentials: "include",
          body: JSON.stringify({ refreshToken: session.refreshToken }),
        });
        if (!res.ok) return false;
        const data = (await res.json()) as AuthResponse;
        saveSession(data);
        return true;
      } catch {
        return false;
      } finally {
        refreshInFlight = null;
      }
    })();
  }

  return refreshInFlight;
}

function redirectToLogin(): void {
  clearSession();
  if (typeof window === "undefined") return;
  const path = window.location.pathname + window.location.search;
  if (path.startsWith("/login")) return;
  // Avoid hard navigation loops while React is still mounting admin routes.
  window.setTimeout(() => {
    if (!window.location.pathname.startsWith("/login")) {
      window.location.assign(`/login?from=${encodeURIComponent(path)}`);
    }
  }, 0);
}

async function request<T>(
  path: string,
  init: RequestInit = {},
  signal?: AbortSignal,
  retried = false,
): Promise<T> {
  const clientId = getOrCreateClientId();
  const headers = new Headers(init.headers);
  headers.set("X-Client-Id", clientId);
  authHeaders(headers);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const res = await fetch(`${API_PREFIX}${path}`, {
    ...init,
    headers,
    signal,
    credentials: "include",
  });

  if (res.status === 204) {
    return null as T;
  }

  const contentType = res.headers.get("content-type") ?? "";
  const isJson = contentType.includes("application/json");
  const payload = isJson ? await res.json().catch(() => null) : await res.text();

  if (!res.ok) {
    const isAuthPath = path.startsWith("/auth/") || path.startsWith("/admin/auth/");
    if (res.status === 401 && !retried && !isAuthPath) {
      const refreshed = await tryRefreshSession();
      if (refreshed) {
        return request<T>(path, init, signal, true);
      }
      redirectToLogin();
      throw new ApiError(401, "Session expired. Please sign in again.");
    }

    // ASP.NET ProblemDetails uses `detail` / `title`; some handlers use `message`.
    const problemMessage =
      isJson && payload && typeof payload === "object"
        ? (() => {
            const p = payload as { detail?: unknown; message?: unknown; title?: unknown };
            if (typeof p.detail === "string" && p.detail.trim()) return p.detail;
            if (typeof p.message === "string" && p.message.trim()) return p.message;
            if (typeof p.title === "string" && p.title.trim() && p.title !== "unauthorized")
              return p.title;
            return null;
          })()
        : null;

    const message =
      problemMessage ??
      (res.status === 401
        ? isAuthPath
          ? "Invalid email or password."
          : "Session expired. Please sign in again."
        : `Request failed with status ${res.status}`);
    throw new ApiError(res.status, message);
  }

  return payload as T;
}

export const api = {
  clientId: () => getOrCreateClientId(),

  /** POST /api/v1/auth/login */
  login(body: LoginRequest, signal?: AbortSignal) {
    return request<AuthResponse>(
      "/auth/login",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    ).then((res) => {
      saveSession(res);
      return res;
    });
  },

  /** POST /api/v1/auth/register */
  register(body: RegisterRequest, signal?: AbortSignal) {
    return request<AuthResponse>(
      "/auth/register",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    ).then((res) => {
      saveSession(res);
      return res;
    });
  },

  /** POST /api/v1/auth/refresh */
  refresh(body: RefreshRequest, signal?: AbortSignal) {
    return request<AuthResponse>(
      "/auth/refresh",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    ).then((res) => {
      saveSession(res);
      return res;
    });
  },

  /** POST /api/v1/auth/logout */
  logout(body: RefreshRequest, signal?: AbortSignal) {
    return request<void>("/auth/logout", { method: "POST", body: JSON.stringify(body) }, signal);
  },

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

  /** PUT /api/v1/places/{id} — editor/admin update place facts. */
  updatePlace(id: string, body: UpdatePlaceRequest, signal?: AbortSignal) {
    return request<PlaceDetailDto>(
      `/places/${id}`,
      { method: "PUT", body: JSON.stringify(body) },
      signal,
    );
  },

  getFavorites(signal?: AbortSignal) {
    return request<FavoriteDto[]>("/favorites", {}, signal);
  },

  addFavorite(placeId: string, signal?: AbortSignal) {
    return request<void>(`/favorites/${placeId}`, { method: "PUT" }, signal);
  },

  removeFavorite(placeId: string, signal?: AbortSignal) {
    return request<void>(`/favorites/${placeId}`, { method: "DELETE" }, signal);
  },

  getTours(signal?: AbortSignal) {
    return request<TourDto[]>("/tours", {}, signal);
  },

  createTour(body: CreateTourRequest, signal?: AbortSignal) {
    return request<TourDto>("/tours", { method: "POST", body: JSON.stringify(body) }, signal);
  },

  deleteTour(id: string, signal?: AbortSignal) {
    return request<void>(`/tours/${id}`, { method: "DELETE" }, signal);
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

  /** POST /api/v1/audio/tts */
  async requestTts(
    body: { placeId: string; lang: string },
    signal?: AbortSignal,
  ): Promise<{ kind: "task"; task: AudioTaskDto } | { kind: "stream"; blob: Blob }> {
    const clientId = getOrCreateClientId();
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
      "X-Client-Id": clientId,
    };
    const token = getAccessToken();
    if (token) headers.Authorization = `Bearer ${token}`;

    const res = await fetch(`${API_PREFIX}/audio/tts`, {
      method: "POST",
      headers,
      body: JSON.stringify(body),
      signal,
      credentials: "include",
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

  /** GET /api/v1/offline/pack?langs=vi&langs=en */
  getOfflinePack(langs: string[], signal?: AbortSignal) {
    return request<OfflinePackManifest>(`/offline/pack${buildQuery({ langs })}`, {}, signal);
  },

  /** GET /api/v1/audio/pack-manifest?lang= */
  getAudioPackManifest(lang: string, signal?: AbortSignal) {
    return request<{ lang: string; files: { placeId: string; path: string; sha256: string }[] }>(
      `/audio/pack-manifest${buildQuery({ lang })}`,
      {},
      signal,
    );
  },

  // --- Admin ---

  listUsers(page = 1, pageSize = 50, signal?: AbortSignal) {
    return request<PagedResult<AdminUserDto>>(
      `/admin/users${buildQuery({ page, pageSize })}`,
      {},
      signal,
    );
  },

  auditLogs(
    params: { userId?: string; entityType?: string; page?: number; pageSize?: number } = {},
    signal?: AbortSignal,
  ) {
    return request<PagedResult<AuditLogDto>>(
      `/admin/audit-logs${buildQuery(params)}`,
      {},
      signal,
    );
  },

  analyticsSummary(signal?: AbortSignal) {
    return request<AnalyticsAdminSummaryDto>("/analytics/admin/summary", {}, signal);
  },

  async exportAnalyticsCsv(signal?: AbortSignal): Promise<Blob> {
    const headers = new Headers({ Accept: "text/csv" });
    authHeaders(headers);
    const res = await fetch(`${API_PREFIX}/analytics/export`, {
      method: "GET",
      headers,
      credentials: "include",
      signal,
    });
    if (!res.ok) {
      throw new ApiError(res.status, `Export failed (${res.status})`);
    }
    return res.blob();
  },

  listAdminRoles(signal?: AbortSignal) {
    return request<AdminRoleDto[]>("/admin/roles", {}, signal);
  },

  setUserActive(userId: string, isActive: boolean, signal?: AbortSignal) {
    return request<void>(
      `/admin/users/${userId}/active`,
      { method: "PUT", body: JSON.stringify({ isActive }) },
      signal,
    );
  },

  assignUserRoles(userId: string, roles: string[], signal?: AbortSignal) {
    return request<void>(
      `/admin/users/${userId}/roles`,
      { method: "PUT", body: JSON.stringify({ roles }) },
      signal,
    );
  },

  updateTranslation(body: UpdateTranslationRequest, signal?: AbortSignal) {
    return request<void>(
      "/admin/translations",
      { method: "PUT", body: JSON.stringify(body) },
      signal,
    );
  },

  regeneratePlaceAudio(placeId: string, signal?: AbortSignal) {
    return request<void>(
      `/admin/places/${placeId}/regenerate-audio`,
      { method: "POST", body: "{}" },
      signal,
    );
  },

  listAudioTasks(take = 50, signal?: AbortSignal) {
    return request<AudioTaskDto[]>(`/admin/audio-tasks${buildQuery({ take })}`, {}, signal);
  },

  pauseAudioTask(taskId: string, signal?: AbortSignal) {
    return request<AudioTaskDto>(
      `/admin/audio-tasks/${taskId}/pause`,
      { method: "POST", body: "{}" },
      signal,
    );
  },

  resumeAudioTask(taskId: string, signal?: AbortSignal) {
    return request<AudioTaskDto>(
      `/admin/audio-tasks/${taskId}/resume`,
      { method: "POST", body: "{}" },
      signal,
    );
  },

  cancelAudioTask(taskId: string, signal?: AbortSignal) {
    return request<AudioTaskDto>(
      `/admin/audio-tasks/${taskId}/cancel`,
      { method: "POST", body: "{}" },
      signal,
    );
  },

  /** Fetch-based SSE (Bearer) for admin audio task fan-out. */
  async streamAudioTasks(
    onTasks: (tasks: AudioTaskDto[]) => void,
    signal?: AbortSignal,
  ): Promise<void> {
    const headers = new Headers({ Accept: "text/event-stream" });
    authHeaders(headers);
    const res = await fetch(`${API_PREFIX}/admin/audio-tasks/stream`, {
      method: "GET",
      headers,
      credentials: "include",
      signal,
    });
    if (!res.ok || !res.body) {
      throw new ApiError(res.status, `SSE failed (${res.status})`);
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const chunks = buffer.split("\n\n");
      buffer = chunks.pop() ?? "";
      for (const chunk of chunks) {
        const dataLine = chunk
          .split("\n")
          .find((line) => line.startsWith("data:"));
        if (!dataLine) continue;
        const json = dataLine.slice(5).trim();
        try {
          onTasks(JSON.parse(json) as AudioTaskDto[]);
        } catch {
          /* ignore malformed */
        }
      }
    }
  },

  // --- AI ---

  aiUsage(signal?: AbortSignal) {
    return request<AiUsageDto>("/ai/usage", {}, signal);
  },

  enhanceDescription(body: EnhanceDescriptionRequest, signal?: AbortSignal) {
    return request<EnhanceDescriptionResponse>(
      "/ai/enhance-description",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    );
  },

  // --- Maps ---

  offlineOptions(signal?: AbortSignal) {
    return request<MapOfflineOptionsDto>("/maps/offline/options", {}, signal);
  },

  offlineManifest(packId: string, signal?: AbortSignal) {
    return request<MapPackManifestDto>(`/maps/offline/manifest/${encodeURIComponent(packId)}`, {}, signal);
  },

  // --- Analytics ---

  collect(body: AnalyticsCollectRequest, signal?: AbortSignal) {
    return request<void>(
      "/analytics/collect",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    );
  },

  recordPresence(body: AnalyticsPresenceRequest, signal?: AbortSignal) {
    return request<void>(
      "/analytics/presence",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    );
  },

  // --- UI bundles ---

  getBundle(locale: string, signal?: AbortSignal) {
    return request<UiBundleDto>(`/ui-bundles/${encodeURIComponent(locale)}`, {}, signal);
  },

  // --- Localizations ---

  localizationsOnDemand(body: { placeId: string; lang: string }, signal?: AbortSignal) {
    return request<OnDemandLocalizationResult>(
      "/localizations/on-demand",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    );
  },

  localizationsWarmup(body: { lang: string; placeIds?: string[] }, signal?: AbortSignal) {
    return request<WarmupResult>(
      "/localizations/warmup",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    );
  },

  localizationsPrepareHotset(
    body: { lat: number; lng: number; lang: string },
    signal?: AbortSignal,
  ) {
    return request<HotsetResult>(
      "/localizations/prepare-hotset",
      { method: "POST", body: JSON.stringify(body) },
      signal,
    );
  },
};

export function apiAudioUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  return `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`;
}

export function staticAssetUrl(path: string): string {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  return `${API_BASE_URL}${normalized}`;
}

// Mirrors the TourismHcm backend contracts (TourismHcm.Contracts.*).
// Field names use camelCase to match System.Text.Json's default output.

export type LangCode = "vi" | "en" | "ja" | "ko" | "zh";

export const SUPPORTED_LANGS: LangCode[] = ["vi", "en", "ja", "ko", "zh"];

export interface FavoriteDto {
  placeId: string;
  placeName: string;
  imageUrl: string;
  latitude: number;
  longitude: number;
  createdAt: string;
}

export interface TourStopDto {
  id: string;
  placeId: string;
  placeName: string;
  latitude: number;
  longitude: number;
  order: number;
}

export interface TourDto {
  id: string;
  userId: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  stops: TourStopDto[];
}

export interface CreateTourRequest {
  name: string;
  placeIds: string[];
  startLatitude?: number | null;
  startLongitude?: number | null;
}

export interface PlaceDto {
  id: string;
  name: string;
  description: string;
  category: string;
  ward: string;
  address: string;
  latitude: number;
  longitude: number;
  triggerRadiusMeters: number;
  openingHours: string;
  ticketPrice: string;
  imageUrl: string;
  tags: string[];
  averageRating: number;
  reviewCount: number;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface PlaceTranslationDto {
  lang: string;
  text: string;
  source: string;
  updatedAt: string;
}

export interface PlaceDetailDto extends PlaceDto {
  translations: PlaceTranslationDto[];
  availableAudioLanguages: string[];
}

export interface NearbyPlaceDto extends PlaceDto {
  distanceMeters: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PlaceQueryParams {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: string;
  ward?: string;
  status?: string;
  sortBy?: string;
  descending?: boolean;
}

export interface AudioInfoDto {
  available: boolean;
  url?: string | null;
  durationSec: number;
  status: string;
}

export interface NarrationResponse {
  placeId: string;
  name: string;
  requestedLang: string;
  actualLang: string;
  isFallback: boolean;
  text: string;
  audio: AudioInfoDto;
}

export interface TriggerRequestBody {
  latitude: number;
  longitude: number;
  lang: string;
  clientId?: string | null;
}

export interface TriggerResponse {
  placeId: string;
  name: string;
  distanceMeters: number;
  triggered: boolean;
  cooldownActive: boolean;
  narration: NarrationResponse | null;
}

export interface AudioTaskDto {
  id: string;
  placeId: string;
  placeName?: string | null;
  lang: string;
  status: string;
  attempts: number;
  error?: string | null;
  progressPercent?: number;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
}

export interface AdminRoleDto {
  id: string;
  name: string;
  permissions: string[];
}

export interface UpdateTranslationRequest {
  placeId: string;
  lang: string;
  text: string;
}

export interface UpdatePlaceRequest {
  name?: string;
  descriptionVi?: string;
  category?: string;
  ward?: string;
  address?: string;
  latitude?: number;
  longitude?: number;
  triggerRadiusMeters?: number;
  openingHours?: string;
  ticketPrice?: string;
  imageUrl?: string;
  tags?: string;
  status?: string;
}

export interface LanguageInfoDto {
  code: string;
  name: string;
  voiceId?: string | null;
  isPriority: boolean;
}

export interface OfflinePlaceItem {
  placeId: string;
  name: string;
  category: string;
  latitude: number;
  longitude: number;
  triggerRadiusMeters: number;
  texts: Record<string, string>;
  audioUrls: Record<string, string>;
}

export interface OfflinePackManifest {
  version: string;
  generatedAt: string;
  places: OfflinePlaceItem[];
}

// --- Auth ---

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface AuthResponse {
  userId: string;
  email: string;
  displayName: string;
  roles: string[];
  permissions: string[];
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

// --- Admin ---

export interface AdminUserDto {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
}

export interface AuditLogDto {
  id: string;
  userId?: string | null;
  action: string;
  entityType: string;
  entityId: string;
  details?: string | null;
  createdAt: string;
}

export interface AnalyticsHourlyMetricDto {
  hourUtc: string;
  metricKey: string;
  count: number;
}

export interface AnalyticsAdminSummaryDto {
  hourly: AnalyticsHourlyMetricDto[];
}

// --- AI ---

export interface AiUsageDto {
  usedToday: number;
  dailyLimit?: number | null;
  isUnlimited: boolean;
}

export interface EnhanceDescriptionRequest {
  sourceText: string;
  lang?: string;
}

export interface EnhanceDescriptionResponse {
  enhancedText: string;
  usedToday: number;
  dailyLimit?: number | null;
}

// --- Maps ---

export interface MapOfflineOptionsDto {
  defaultScope: string;
  availablePacks: string[];
}

export interface MapPackFileDto {
  path: string;
  sha256?: string | null;
  sizeBytes?: number | null;
}

export interface MapPackManifestDto {
  packId: string;
  scope: string;
  version: string;
  files: MapPackFileDto[];
}

// --- Analytics collect ---

export interface AnalyticsEventInput {
  eventName: string;
  payloadJson?: string | null;
  occurredAt?: string | null;
}

export interface AnalyticsCollectRequest {
  deviceId: string;
  sessionId: string;
  consent: boolean;
  events: AnalyticsEventInput[];
}

// --- UI bundles ---

export interface UiBundleDto {
  locale: string;
  status: string;
  payloadJson: string;
  isFallback: boolean;
}

// --- Localizations ---

export interface OnDemandLocalizationResult {
  placeId: string;
  lang: string;
  status: string;
  audioUrl?: string | null;
  taskId?: string | null;
  retryAfterSeconds?: number | null;
}

export interface WarmupResult {
  lang: string;
  queued: number;
  alreadyReady: number;
}

export interface HotsetResult {
  lang: string;
  placeIds: string[];
  readyCount: number;
  isReady: boolean;
}

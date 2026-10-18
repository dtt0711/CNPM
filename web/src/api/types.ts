// Mirrors the TourismHcm backend contracts (TourismHcm.Contracts.*).
// Field names use camelCase to match System.Text.Json's default output.

export type LangCode = "vi" | "en" | "ja" | "ko" | "zh";

export const SUPPORTED_LANGS: LangCode[] = ["vi", "en", "ja", "ko", "zh"];

export interface PlaceDto {
  id: string;
  name: string;
  description: string;
  category: string;
  district: string;
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
  district?: string;
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
  lang: string;
  status: string;
  attempts: number;
  error?: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
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

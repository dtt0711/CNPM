# TourismHcm — Thuyết minh tự động đa ngôn ngữ (TP.HCM)

Hệ thống audio guide theo GPS cho địa điểm du lịch Thành phố Hồ Chí Minh.

**Stack:** ASP.NET Core 10 Web API · SQL Server · EF Core · Redis · React + Vite PWA · MapLibre + PMTiles · Edge-TTS / free Translate (Python sidecar) · optional Google Cloud Translation / Azure Speech / Gemini · Docker · GitHub Actions / GHCR

## Cấu trúc

```
src/
  TourismHcm.Domain/          # Entities, enums, helpers
  TourismHcm.Contracts/       # DTOs
  TourismHcm.Application/     # Business services, validators, interfaces
  TourismHcm.Infrastructure/  # EF, repos, providers, JWT, seed
  TourismHcm.Api/             # Controllers, middleware, Program.cs
services/providers/           # Edge-TTS + deep-translator sidecar
tests/                        # Unit, Integration, Architecture
web/                          # React PWA (+ /admin portal)
docs/                         # architecture, assumptions, deploy, maps, presentation
```

Chi tiết hướng phụ thuộc và sơ đồ Mermaid: [docs/architecture.md](docs/architecture.md).

## Chạy local (Docker Compose)

```bash
cp .env.example .env
docker compose up --build -d
```

| Service | URL |
|---------|-----|
| API + Swagger (Dev) | http://localhost:8080/swagger |
| Health ready | http://localhost:8080/health/ready |
| Providers (Edge-TTS / Translate) | http://localhost:8090/health |
| Web PWA | http://localhost:8081 |
| Admin portal | http://localhost:8081/admin |

Docker Compose mặc định: **Edge** TTS + **Free** Translate qua sidecar `providers` (không còn Fake). Cần `docker compose up --build` để có service `providers` trên `:8090`. Seed 15 địa điểm + admin từ `.env`.

### Chạy API không Docker

```bash
# SQL Server + Redis phải sẵn sàng; chỉnh ConnectionStrings trong appsettings.Development.json
dotnet ef database update --project src/TourismHcm.Infrastructure --startup-project src/TourismHcm.Api
dotnet run --project src/TourismHcm.Api
```

### Frontend (cùng stack với `dotnet run`)

```bash
cd web
cp .env.example .env
npm ci
npm run dev    # http://localhost:5173 — proxy /api /static → :5020
```

**Một stack thôi khi demo:** Vite mặc định `VITE_API_PROXY_TARGET=http://localhost:5020`. Docker Compose là stack khác (`:8080` + web `:8081`). Đừng chạy `dotnet run` và API Docker cùng lúc rồi để Vite trỏ nhầm.

Nền bản đồ Offline cần `hcm.pmtiles` (gitignore, không commit). Xem [docs/maps.md](docs/maps.md). Compose bind-mount `src/TourismHcm.Api/wwwroot/static/maps` để container thấy cùng pack/fonts.

## Tests

```bash
# Unit + Architecture + coverage Application >= 80%
dotnet test tests/TourismHcm.UnitTests tests/TourismHcm.ArchitectureTests \
  /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura \
  /p:Include="[TourismHcm.Application]*" /p:Threshold=80 /p:ThresholdType=line

# Integration (cần Docker cho Testcontainers SQL Server)
dotnet test tests/TourismHcm.IntegrationTests

# Frontend
cd web && npm test && npm run build
```

```bash
dotnet format --verify-no-changes
dotnet build -warnaserror
```

## Biến môi trường chính

Xem [.env.example](.env.example).

| Biến / Config | Mô tả |
|---------------|--------|
| `ConnectionStrings:Default` | SQL Server |
| `ConnectionStrings:Redis` | Redis (cooldown geofence) |
| `Jwt:SigningKey` | HMAC ≥ 32 ký tự |
| `Admin:Email` / `Admin:Password` | Seed admin (Development) |
| `Providers:BaseUrl` | Sidecar Edge-TTS / Free Translate |
| `Translation:Provider` / `Translation:ApiKey` | `Free` (sidecar) \| `Google` |
| `Speech:Provider` / Azure keys | `Edge` (sidecar) \| `Azure` |
| `Ai:GeminiApiKey` | Gemini enhance-description (owner quota 10/day) |
| `MapPack:DataDir` | PMTiles / styles root ([docs/maps.md](docs/maps.md)) |
| `AudioStorage:RootPath` | Thư mục MP3 |
| `VITE_API_BASE_URL` / `VITE_MAP_TILE_URL` | Frontend |

**Production:** thiếu key TTS/Translation → HTTP **503** rõ ràng, không trả dữ liệu giả.

## Google Cloud Translation

1. Tạo project tại [Google Cloud Console](https://console.cloud.google.com/).
2. Bật **Cloud Translation API**.
3. Tạo API key (restrict Translation API) **hoặc** Service Account với role Cloud Translation + JSON key.
4. Đặt `Translation__ApiKey` / secret `GOOGLE_TRANSLATION_API_KEY`, hoặc mount JSON và set `GOOGLE_APPLICATION_CREDENTIALS`.
5. Không commit file JSON vào git.

Ánh xạ ngôn ngữ: `vi→vi`, `en→en`, `ja→ja`, `ko→ko`, `zh→zh-CN`. Chỉ dịch khi `contentHash` đổi; kết quả lưu `PlaceTranslation`.

## Azure AI Speech (TTS)

1. Tạo resource **Speech** trên Azure Portal (ví dụ region `southeastasia`).
2. Copy Key + Region vào `Speech__AzureKey` / `Speech__AzureRegion`.
3. Voice mặc định trong `appsettings.json` (`Speech:Voices`).

## API nổi bật (`/api/v1`)

- `GET /places`, `GET /places/nearby`, `GET /places/trigger`
- `GET /places/{id}/narration?lang=`
- `GET /audio/languages`, `POST /audio/tts`, `GET /audio/tasks/{id}/events` (SSE)
- `GET /offline/pack?lang=`
- Auth JWT (cookie + Bearer): register / login / refresh · RBAC `super_admin` / `admin` / `user`
- Reviews, Favorites, Tours · Admin portal · Gemini AI · Analytics consent · UI bundles · PMTiles maps
- `POST /localizations/on-demand|warmup|prepare-hotset`

Khách ẩn danh được đọc places, trigger, narration, audio, offline, maps.

## CI/CD

| Workflow | Khi | Việc |
|----------|-----|------|
| [.github/workflows/ci.yml](.github/workflows/ci.yml) | push / PR | format, build, test+coverage, vitest, docker build |
| [.github/workflows/cd-staging.yml](.github/workflows/cd-staging.yml) | merge `develop` | push GHCR, migrate idempotent, deploy, smoke `/health/ready` |
| [.github/workflows/cd-production.yml](.github/workflows/cd-production.yml) | tag `vX.Y.Z` + Environment approval | deploy image, migrate, health, **rollback** nếu fail |

Nhánh: `main` (production), `develop` (staging), `feature/*`. Commit: Conventional Commits.

Chi tiết secrets & host: [docs/deploy.md](docs/deploy.md). Giả định: [docs/assumptions.md](docs/assumptions.md). Slide: [docs/presentation.md](docs/presentation.md).

## Nghiệm thu nhanh

```bash
curl -s http://localhost:8080/health/ready
curl -s "http://localhost:8080/api/v1/places/trigger?lat=10.7769&lng=106.6959&lang=ja" \
  -H "X-Client-Id: 11111111-1111-1111-1111-111111111111"
# Gọi lại trong 5 phút → cooldownActive=true
```

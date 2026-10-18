# TourismHcm — Thuyết minh tự động đa ngôn ngữ (TP.HCM)

Hệ thống audio guide theo GPS cho địa điểm du lịch Thành phố Hồ Chí Minh.

**Stack:** ASP.NET Core 10 Web API · SQL Server · EF Core · Redis · React + Vite PWA · MapLibre · Google Cloud Translation · Azure AI Speech · Docker · GitHub Actions / GHCR

## Cấu trúc

```
src/
  TourismHcm.Domain/          # Entities, enums, helpers
  TourismHcm.Contracts/       # DTOs
  TourismHcm.Application/     # Business services, validators, interfaces
  TourismHcm.Infrastructure/  # EF, repos, providers, JWT, seed
  TourismHcm.Api/             # Controllers, middleware, Program.cs
tests/                        # Unit, Integration, Architecture
web/                          # React PWA
docs/                         # architecture, assumptions, deploy, presentation
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
| Web PWA | http://localhost:8081 |

Mặc định Development dùng **Fake** Translation/TTS (không cần key). Seed 15 địa điểm + admin từ `.env`.

### Chạy API không Docker

```bash
# SQL Server + Redis phải sẵn sàng; chỉnh ConnectionStrings trong appsettings.Development.json
dotnet ef database update --project src/TourismHcm.Infrastructure --startup-project src/TourismHcm.Api
dotnet run --project src/TourismHcm.Api
```

### Frontend

```bash
cd web
cp .env.example .env
npm ci
npm run dev    # http://localhost:5173 — proxy /api → :8080
```

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
| `Translation:Provider` / `Translation:ApiKey` | `Google` hoặc `Fake` |
| `GOOGLE_APPLICATION_CREDENTIALS` | Đường dẫn JSON service account (tuỳ chọn) |
| `Speech:Provider` / `Speech:AzureKey` / `Speech:AzureRegion` | `Azure` hoặc `Fake` |
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
- Auth JWT: register / login / refresh · RBAC admin / editor / user
- Reviews, Favorites, Tours · Admin duyệt / bản dịch Manual / regenerate audio / audit

Khách ẩn danh được đọc places, trigger, narration, audio, offline.

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

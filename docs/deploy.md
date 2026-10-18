# Deploy guide (staging / production)

This project ships GitHub Actions workflows that push images to GHCR and SSH into a host running `docker compose`. There is no cloud server provisioned by the repo — you must fill secrets and place compose files on the host.

## GitHub Environments & Secrets

Create Environments: `staging`, `production` (production should require reviewer approval).

| Secret | Where | Purpose |
|--------|-------|---------|
| `STAGING_CONNECTION_STRING` | staging | SQL Server connection for migrate script |
| `STAGING_URL` | staging | Base URL for smoke test, e.g. `https://staging.example.com` |
| `PRODUCTION_URL` | production | Base URL for health check |
| `DEPLOY_HOST` | both | SSH host |
| `DEPLOY_USER` | both | SSH user |
| `DEPLOY_SSH_KEY` | both | Private key |
| `GOOGLE_TRANSLATION_API_KEY` | both | Google Cloud Translation |
| `AZURE_SPEECH_KEY` / `AZURE_SPEECH_REGION` | both | Azure AI Speech |
| `JWT_SIGNING_KEY` | both | JWT HMAC key (≥32 chars) |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | staging (dev seed) | Seeded admin |

Do **not** commit service-account JSON. Prefer `GOOGLE_TRANSLATION_API_KEY` or mount credentials via host secret path + `GOOGLE_APPLICATION_CREDENTIALS`.

## Host layout

```
/opt/tourismhcm/staging/docker-compose.staging.yml
/opt/tourismhcm/production/docker-compose.production.yml
/opt/tourismhcm/production/.previous-image-tag   # written by CD for rollback
```

Compose files should pull `ghcr.io/<owner>/tourismhcm-api:<tag>` and `tourismhcm-web:<tag>`, inject connection strings and provider keys from the host env / Docker secrets.

## Migration policy (expand/contract)

- Production **never** auto-migrates on app startup.
- CD generates `dotnet ef migrations script --idempotent` and applies it before/at deploy.
- Image rollback does **not** roll back the database. Migrations must be backward-compatible (expand → deploy → contract later). See `docs/architecture.md`.

## Local compose (no remote server)

```bash
cp .env.example .env
docker compose up --build -d
curl http://localhost:8080/health/ready
```

Dev uses `ASPNETCORE_ENVIRONMENT=Development` so Fake TTS/Translation are allowed when keys are empty. For production-like behavior:

```bash
# in compose override
ASPNETCORE_ENVIRONMENT=Production
SPEECH_PROVIDER=Azure
TRANSLATION_PROVIDER=Google
```

Missing keys in Production → API returns **503** with ProblemDetails (no silent fake data).

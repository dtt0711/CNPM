# Architecture — TourismHcm

## 3-layer dependency direction

```mermaid
flowchart TB
  subgraph presentation [Lớp 1 - Presentation]
    Api[TourismHcm.Api<br/>Controllers / Middleware / Program]
  end
  subgraph business [Lớp 2 - Business]
    App[TourismHcm.Application<br/>Services / Validators / Interfaces]
    Contracts[TourismHcm.Contracts<br/>DTOs]
  end
  subgraph domain [Domain]
    Dom[TourismHcm.Domain<br/>Entities / Enums / Helpers]
  end
  subgraph data [Lớp 3 - Data Access]
    Infra[TourismHcm.Infrastructure<br/>EF / Repos / Providers / JWT]
  end
  subgraph sidecar [Sidecar]
    Prov[services/providers<br/>Edge-TTS + deep-translator]
  end

  Api --> App
  Api -.->|composition root only<br/>AddInfrastructure| Infra
  App --> Dom
  App --> Contracts
  Contracts --> Dom
  Infra --> App
  Infra -->|HTTP| Prov
```

Platform extras (HTML parity, domain TourismHcm): RBAC permissions, Admin portal, Gemini AI, PMTiles maps (`docs/maps.md`), analytics consent, UI bundles, localization on-demand/warmup.

Rules enforced by `TourismHcm.ArchitectureTests`:

- Controllers never use `DbContext` / repository types.
- Application never references EF Core or ASP.NET Core.
- Domain has no project references to other TourismHcm assemblies.
- Infrastructure implements Application interfaces.

## Geofence → narration → audio fallback

```mermaid
sequenceDiagram
  participant PWA as PWA / GeofenceEngine
  participant API as Api Controllers
  participant Geo as GeofenceService
  participant Nar as NarrationService
  participant Aud as AudioService
  participant Cache as IDistributedCache
  participant TTS as ITextToSpeechProvider
  participant Tr as ITranslationProvider
  participant Store as IAudioStorage

  PWA->>API: GET /places/trigger?lat&lng&lang + X-Client-Id
  API->>Geo: TriggerAsync
  Geo->>Geo: Bounding box + Haversine, nearest in radius
  Geo->>Cache: cooldown:{clientId}:{placeId}
  alt cooldown active
    Geo-->>PWA: cooldownActive=true
  else new trigger
    Geo->>Nar: GetNarration(placeId, lang)
    Nar->>Nar: text: lang -> en -> vi (isFallback)
    Nar->>Aud: resolve audio status/url
    alt audio Ready
      Aud-->>PWA: audio.url
    else enqueue generation
      Aud->>Tr: translate if contentHash changed
      Aud->>Aud: Channel task + BackgroundService
      Aud-->>PWA: taskId / status Pending
      PWA->>API: GET /audio/tasks/{id}/events (SSE)
      Aud->>TTS: Synthesize
      Aud->>Store: {placeId}/{lang}/{hash}.mp3
    else on-demand stream
      PWA->>API: POST /audio/tts {placeId,lang}
    else offline
      PWA->>PWA: Web Speech API
    end
  end
```

## CI/CD pipeline

```mermaid
flowchart LR
  feat[feature/*] --> ci[ci.yml<br/>format build test coverage docker]
  ci --> develop[develop]
  develop --> stg[cd-staging.yml<br/>GHCR + migrate + smoke]
  stg --> main[main / tag vX.Y.Z]
  main --> prod[cd-production.yml<br/>approve + deploy + health]
  prod -->|health fail| rb[auto rollback image]
```

### Migration expand/contract

Rollback of a container image does **not** undo SQL. Every migration must remain compatible with the previous app version until a later contract phase removes unused columns.

## Provider policy

| Environment | Missing Google/Azure keys |
|-------------|---------------------------|
| Development | Sidecar **Free** translate + **Edge** TTS (`providers` on :8090) |
| Testing | Fake stubs (no network sidecar) |
| Production | Real providers; `IsConfigured=false` → **503** ProblemDetails, never fake data |

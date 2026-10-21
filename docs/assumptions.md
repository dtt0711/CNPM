# Assumptions

Documented decisions made when the specification did not mandate a single choice.

1. **Solution format**: .NET 10 SDK produces `TourismHcm.slnx` (XML solution). All `dotnet` commands use the solution in the repo root.
2. **Password hashing**: BCrypt (`BCrypt.Net-Next`) instead of ASP.NET Identity full stack; JWT is custom with refresh-token rotation stored hashed (SHA-256) in SQL Server.
3. **TTS provider default**: Edge sidecar (`Speech:Provider=Edge`) or Azure when `Speech:Provider=Azure`. Fake TTS/Translate exist **only** in the Testing host. Google Cloud TTS is not implemented.
4. **Translation language map**: internal `zh` → Google `zh-CN`.
5. **Offline pack query**: API accepts language via query (`lang` or `langs`); frontend sends the active language.
6. **Health `/health`**: liveness with no dependency checks (always 200 if process up). `/health/ready` checks SQL Server, Redis (when configured), and writable audio directory.
7. **Map tiles**: default OpenStreetMap raster tiles via `VITE_MAP_TILE_URL`; operators should use a tile provider that matches their license/ToS for production traffic.
8. **Place images**: seed uses Wikimedia Commons (or similar public) photos of the actual venues, not picsum placeholders.
9. **Ticket prices / opening hours**: seeded from publicly posted visitor info at implementation time; venues can change prices — Admin Places can edit.
10. **Coordinates**: WGS84 points commonly published for each venue (Củ Chi / Cần Giờ included in the full HCM map bbox).
11. **Deploy target**: no cloud server is provisioned by this repo; CD expects SSH + docker compose on a host you control (secrets listed in `.env.example` / `docs/deploy.md`).
12. **Swagger in Production**: disabled by default (Development only) to reduce attack surface.
13. **Rate limits**: global 100 req/min/IP; named policy `tts` = 10 req/min/IP for expensive endpoints.
14. **Integration tests**: use Testcontainers SQL Server; they skip when Docker engine is unavailable.
15. **NuGet audit**: transitive advisories `NU1901–NU1904` are warnings-not-errors so EF SQL Client restore succeeds; direct package audit remains enabled.
16. **Program.cs Infrastructure usage**: besides `AddInfrastructure`, Development bootstrap calls `MigrateAsync` + `DataSeeder` (never in Production). Controllers remain free of Infrastructure types.
17. **No cloud API keys**: Development uses the `providers` sidecar (Free translate + Edge TTS). Manual English seed translations still enable fallback (`ja`→`en`, `isFallback=true`) plus browser **Web Speech API**. Real ja/ko/zh MP3 quality depends on the sidecar / optional Google+Azure keys.

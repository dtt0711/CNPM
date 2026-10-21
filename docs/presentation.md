# TourismHcm — Slide deck (Markdown)

> Import vào Marp / Slidev / PowerPoint. Mỗi `---` là một slide.

---

## Hệ thống thuyết minh tự động đa ngôn ngữ
### Địa điểm du lịch TP. Hồ Chí Minh

Audio guide theo GPS · ASP.NET Core 10 · React PWA · Edge-TTS · PMTiles

---

## Vấn đề

- Du khách đến gần điểm tham quan nhưng thiếu thuyết minh đúng ngôn ngữ
- Nội dung tĩnh / QR không biết vị trí
- Chi phí dịch + TTS cần kiểm soát (hash, cache, rate limit)

---

## Giải pháp

- Geofence GPS (bán kính cấu hình, mặc định 30 m)
- Thuyết minh vi / en / ja / ko / zh
- Fallback nội dung 3 tầng + fallback audio 4 tầng (kể cả Web Speech offline)
- PWA offline pack + PMTiles (Cloud / Offline / Hybrid)
- Admin portal · Gemini AI enhance · Analytics consent

---

## Kiến trúc

1. **Api** — Controllers, cookie+Bearer JWT, RBAC permissions, health
2. **Application** — geofence, narration, audio queue, admin, AI, maps, analytics
3. **Infrastructure** — EF Core, Redis, providers sidecar (Edge-TTS + free Translate), optional Azure/Google/Gemini
4. **providers** — Python FastAPI microservice

Hướng phụ thuộc một chiều · ArchitectureTests bắt buộc

---

## Luồng chính

GPS → `/places/trigger` → cooldown cache → narration  
→ audio sẵn có **hoặc** hàng đợi TTS (SSE) **hoặc** Edge stream **hoặc** Web Speech

---

## Platform

- Roles: `super_admin` / `admin` / `user`
- Admin quản lý địa điểm, bản dịch, audio, users, analytics
- Maps: `/static/maps` + `/api/v1/maps/*`

---

## Dữ liệu mẫu

≥ 15 địa điểm thật: Dinh Độc Lập, Nhà thờ Đức Bà, Bến Thành, Củ Chi, Cần Giờ, Landmark 81…

Ảnh Wikimedia · giờ mở cửa / giá vé theo thông tin công bố (Admin Places có thể sửa)

---

## Chất lượng & DevOps

- Unit (Application ≥ 80% coverage) · Integration (Testcontainers) · ArchitectureTests
- CI: format, warnaserror, test, docker build
- CD staging (develop) · CD production (tag + duyệt) + rollback image
- Migration expand/contract — không auto-migrate Production

---

## Demo checklist

1. `docker compose up` → `/health/ready` 200 + providers `/health`  
2. Trigger tại Dinh Độc Lập `lang=ja`  
3. Gọi lại trong 5 phút → `cooldownActive=true`  
4. Đổi mô tả → task audio + SSE  
5. PWA: vào vùng tự phát · offline pack · map mode Offline/Hybrid  
6. `/admin` · Places · AI enhance mô tả

---

## Cần cung cấp để chạy Production

- Sidecar providers (mặc định) **hoặc** Google Translate + Azure Speech keys
- `GEMINI_API_KEY` nếu bật AI Advisor
- JWT signing key, admin password, PII encryption key
- PMTiles pack thật (xem `docs/maps.md`)
- Máy chủ SSH + secrets GHCR / Environments

---

## Kết luận

Audio guide đa ngôn ngữ end-to-end: free-core TTS/dịch, offline maps, Admin, test + CI/CD — sẵn sàng gắn key thật và deploy staging.

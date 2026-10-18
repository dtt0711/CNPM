# TourismHcm — Slide deck (Markdown)

> Import vào Marp / Slidev / PowerPoint. Mỗi `---` là một slide.

---

## Hệ thống thuyết minh tự động đa ngôn ngữ
### Địa điểm du lịch TP. Hồ Chí Minh

Audio guide theo GPS · ASP.NET Core 10 · React PWA

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
- PWA tải gói offline theo ngôn ngữ

---

## Kiến trúc 3 lớp

1. **Api** — Controllers, auth, middleware, health
2. **Application** — nghiệp vụ (geofence, narration, audio queue, tours…)
3. **Infrastructure** — EF Core, Redis, Google Translate, Azure Speech, disk storage

Hướng phụ thuộc một chiều · ArchitectureTests bắt buộc

---

## Luồng chính

GPS → `/places/trigger` → cooldown cache → narration  
→ audio sẵn có **hoặc** hàng đợi TTS (SSE) **hoặc** stream **hoặc** Web Speech

---

## Dữ liệu mẫu

≥ 15 địa điểm thật: Dinh Độc Lập, Nhà thờ Đức Bà, Bến Thành, Củ Chi, Cần Giờ, Landmark 81…

Tọa độ cần VERIFY · Giá vé / giờ mở cửa = SAMPLE DATA

---

## Chất lượng & DevOps

- Unit (Application ≥ 80% coverage) · Integration (Testcontainers) · ArchitectureTests
- CI: format, warnaserror, test, docker build
- CD staging (develop) · CD production (tag + duyệt) + rollback image
- Migration expand/contract — không auto-migrate Production

---

## Demo checklist

1. `docker compose up` → `/health/ready` 200  
2. Trigger tại Dinh Độc Lập `lang=ja`  
3. Gọi lại trong 5 phút → `cooldownActive=true`  
4. Đổi mô tả → task audio + SSE  
5. PWA: vào vùng tự phát · offline pack

---

## Cần cung cấp để chạy Production

- Google Cloud Translation API key (hoặc service account)
- Azure AI Speech key + region
- JWT signing key, admin password
- Máy chủ SSH + secrets GHCR / Environments

---

## Kết luận

Hệ thống audio guide đa ngôn ngữ end-to-end, có test, CI/CD và tài liệu vận hành — sẵn sàng gắn key thật và deploy staging.

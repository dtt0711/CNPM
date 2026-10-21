# Bản đồ offline (PMTiles) — hướng dẫn chi tiết

TourismHcm dùng **MapLibre** + file **PMTiles** (vector tiles một file) để hiện nền bản đồ khi chọn chế độ **Offline**.

---

## 1. Hiện trạng trong repo

| File | Có trong git? | Vai trò |
|------|---------------|---------|
| `wwwroot/static/maps/styles/hcm-offline.json` | Có | Style MapLibre (lớp nước, đường, nhà…) |
| `wwwroot/static/maps/packs/current/manifest.json` | Có | Metadata pack (stub) |
| `wwwroot/static/maps/packs/current/hcm.pmtiles` | **Không** (`.gitignore`) | Tile thật — **bạn phải tạo/copy vào** |
| `wwwroot/static/maps/fonts/Noto Sans Regular/*.pbf` | Có (subset Latin + Việt) | Glyph labels Offline |

Nếu thiếu `hcm.pmtiles`:

- API trả **404** khi tải tile
- App **tạm hiện nền OSM** (cần mạng) và hiện banner cảnh báo
- Marker địa điểm vẫn hiện bình thường

Muốn Offline **thật** (không phụ thuộc tile OSM): cần file `hcm.pmtiles` đúng chỗ.

---

## 2. Ba chế độ Map trên UI

Trên header app: **Map → Cloud / Offline / Hybrid**.

| Mode | Hành vi |
|------|---------|
| **Cloud** | Nền OSM (hoặc `VITE_MAP_TILE_URL` nếu cấu hình) — cần mạng |
| **Offline** | Đọc `hcm.pmtiles` qua protocol `pmtiles://`. Thiếu file → fallback OSM + banner |
| **Hybrid** | Có mạng → OSM. Sự kiện `offline` → PMTiles ngay; `online` → OSM lại |

Lựa chọn được lưu `localStorage` (`tourismhcm.mapMode`).

---

## 3. Cấu trúc thư mục

Đường dẫn mặc định (`MapPack:DataDir` = `wwwroot/static/maps`):

```
src/TourismHcm.Api/wwwroot/static/maps/
├── packs/
│   └── current/
│       ├── manifest.json      ← đã có
│       └── hcm.pmtiles        ← BẠN THÊM FILE NÀY
├── styles/
│   └── hcm-offline.json       ← đã có (schema OpenMapTiles)
├── fonts/Noto Sans Regular/   ← glyph PBF (Latin + tiếng Việt)
└── sprites/                   ← tùy chọn
```

URL khi API chạy (ví dụ port 5020):

- Style: `http://localhost:5020/static/maps/styles/hcm-offline.json`
- Pack: `http://localhost:5020/static/maps/packs/current/hcm.pmtiles`
- Options: `GET /api/v1/maps/offline/options`
- Manifest: `GET /api/v1/maps/offline/manifest/current`

Vite (`:5173`) proxy `/static` → API (`VITE_API_PROXY_TARGET`, mặc định `:5020`), nên mở app qua `http://localhost:5173` là đủ.

Docker Compose bind-mount thư mục `wwwroot/static/maps` vào API (không dùng named volume rỗng). Pack + fonts trên máy host hiện luôn trong container `:8080`.

**Quan trọng:** đừng mở URL `…/hcm.pmtiles` trên tab trình duyệt — trình duyệt sẽ **tải file về** (đúng với file binary). Bản đồ chỉ hiện trong app (MapLibre + Range request).

`web/.env` phải trỏ cùng API đang có file pack:

```env
VITE_API_PROXY_TARGET=http://localhost:5020
```

Nếu Vite vẫn proxy sang Docker `:8080` mà pack chỉ nằm ở `dotnet run` `:5020` → app 404 / map trống dù mở `:5020` vẫn tải được file.

Đổi thư mục gốc (tùy chọn):

```json
// appsettings.Development.json
"MapPack": {
  "DataDir": "wwwroot/static/maps",
  "DefaultScope": "hcm"
}
```

Hoặc biến môi trường `MAP_PACK_DATA_DIR` (nếu bạn map env → config).

---

## 4. Tạo `hcm.pmtiles` (Windows) — cách khuyến nghị

Mục tiêu: extract OSM khu vực TP.HCM → MBTiles (OpenMapTiles) → PMTiles.

### 4.1. Chuẩn bị công cụ

1. **Java 21+** (Planetiler mới compile class file 65.0 — **Java 8/11/17 sẽ lỗi**)  
   - Cài (Admin PowerShell): `winget install Microsoft.OpenJDK.21`  
   - Đóng hết cửa sổ PowerShell cũ, mở lại, chạy `java -version` → phải thấy `21.x`  
   - Nếu vẫn ra Java cũ: dùng full path, ví dụ  
     `& "C:\Program Files\Microsoft\jdk-21*\bin\java.exe" -version`  
   - Lỗi `UnsupportedClassVersionError` / `version 65.0` vs `52.0` = đang chạy Java 8

2. **Planetiler** (JAR)  
   - Release: https://github.com/onthegomap/planetiler/releases  
   - Tải `planetiler.jar` vào thư mục tạm, ví dụ `C:\tools\planetiler\`

3. **pmtiles CLI** (convert)  
   - https://github.com/protomaps/go-pmtiles/releases  
   - Tải bản Windows, đặt `pmtiles.exe` vào PATH hoặc cùng thư mục làm việc

4. **OSM data Việt Nam** (Geofabrik)  
   - https://download.geofabrik.de/asia/vietnam.html  
   - Tải `vietnam-latest.osm.pbf` (~200–400 MB tùy thời điểm)

### 4.2. Chọn bbox — pack nhỏ vs pack **full TP.HCM**

`--bounds` là **west,south,east,north**. Pack hiện tại (~11 MB) dùng bbox **trung tâm** nên zoom ra / Củ Chi / Cần Giờ bị **cắt trắng**.

| Pack | `--bounds=` | Phủ | Kích thước ước lượng |
|------|-------------|-----|----------------------|
| Trung tâm (cũ) | `106.55,10.70,106.85,10.90` | Phường trung tâm, thiếu Củ Chi & Cần Giờ | ~10–20 MB |
| **Full TP.HCM (khuyên dùng)** | `106.36,10.35,107.03,11.16` | Củ Chi → Cần Giờ, Chợ Lớn, Landmark 81 | ~50–150 MB |
| Cả Việt Nam | *bỏ `--bounds`* | Cả nước | rất lớn, chậm |

Bbox full đã chừa mép quanh 15 điểm seed (Củ Chi ~11.06/106.53, Cần Giờ ~10.41/106.95).

Osmium (tùy chọn):

```powershell
osmium extract -b 106.36,10.35,107.03,11.16 vietnam-latest.osm.pbf -o hcm.osm.pbf
```

### 4.3. Build MBTiles bằng Planetiler

PowerShell, trong thư mục có `planetiler.jar` (dùng **Java 21**). Pack **full không cắt**:

```powershell
java -Xmx6g -jar planetiler.jar `
  --download `
  --area=vietnam `
  --bounds=106.36,10.35,107.03,11.16 `
  --output=hcm.mbtiles
```

Hoặc nếu đã có `hcm.osm.pbf`:

```powershell
java -Xmx4g -jar planetiler.jar `
  --osm-path=hcm.osm.pbf `
  --output=hcm.mbtiles
```

Kết quả: `hcm.mbtiles` (schema **OpenMapTiles** — khớp `hcm-offline.json`).

> Máy yếu: tăng thời gian chờ; cần vài GB RAM. Có thể giảm `--maxzoom` nếu Planetiler hỗ trợ flag tương ứng trên bản bạn dùng (xem `--help`).

### 4.4. Đổi sang PMTiles

```powershell
pmtiles convert hcm.mbtiles hcm.pmtiles
```

Kiểm tra:

```powershell
pmtiles show hcm.pmtiles
```

### 4.5. Đặt vào project

```powershell
$dest = "src\TourismHcm.Api\wwwroot\static\maps\packs\current"
Copy-Item .\hcm.pmtiles $dest\hcm.pmtiles -Force
```

### 4.6. Cập nhật `manifest.json`

Sửa `packs/current/manifest.json`:

```json
{
  "id": "hcm-v1",
  "scope": "hcm",
  "label": "TP. Ho Chi Minh",
  "version": "1.0.0",
  "source_date": "2026-10-19",
  "bbox": [106.36, 10.35, 107.03, 11.16],
  "fingerprint": "thay-bang-hash-ngan",
  "assets": [
    {
      "path": "hcm.pmtiles",
      "sha256": "HASH_SHA256_O_DAY",
      "bytes": 123456789,
      "optional": false
    }
  ],
  "style_url": "/static/maps/styles/hcm-offline.json",
  "note": "Generated with Planetiler + pmtiles convert"
}
```

Tính hash & kích thước (PowerShell):

```powershell
$f = Get-Item "$dest\hcm.pmtiles"
$f.Length
(Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower()
```

Gán `bytes` = Length, `sha256` = hash, `fingerprint` có thể lấy 12 ký tự đầu của hash.

### 4.7. Khởi động lại & kiểm tra

1. Restart API: `dotnet run --project .\src\TourismHcm.Api\`
2. Mở trình duyệt:

```text
http://localhost:5020/static/maps/packs/current/hcm.pmtiles
```

   - Phải **không** 404 (có thể tải / hiện binary).  
   - HEAD/Range request nên OK (PMTiles dùng range).

3. Vite: `npm run dev` → `http://localhost:5173`
4. Chọn **Map → Offline**
5. Banner cảnh báo **biến mất**, nền bản đồ vector (đường/nước) hiện — không còn phụ thuộc OSM

---

## 5. Cách nhanh hơn (tippecanoe) — nếu đã có GeoJSON/MBTiles

Nếu bạn có sẵn `.mbtiles` từ tippecanoe / khác:

```bash
pmtiles convert input.mbtiles hcm.pmtiles
```

**Lưu ý schema:** `hcm-offline.json` giả định lớp OpenMapTiles (`water`, `transportation`, `building`, …). Pack Protomaps basemap dùng tên lớp khác → cần style khác hoặc đổi `source-layer` trong JSON.

---

## 6. Nút “Tải gói offline” trên app khác gì?

| | Pack **bản đồ** (`hcm.pmtiles`) | Nút **Tải gói offline** |
|--|--------------------------------|-------------------------|
| Nội dung | Nền đường/sông/nhà | Manifest địa điểm + text + URL audio |
| API | `/static/maps…`, `/api/v1/maps/offline/*` | `/api/v1/offline/pack?langs=` |
| Mục đích | Vẽ map khi Offline | Cache nội dung thuyết minh khi mất mạng |

Hai thứ bổ sung nhau; chỉ tải nút Offline **không** tạo `hcm.pmtiles`.

---

## 7. Troubleshooting

| Hiện tượng | Nguyên nhân | Cách xử lý |
|------------|-------------|------------|
| Offline trống / chỉ màu be | Thiếu pmtiles **và** bản cũ chưa fallback | Cập nhật code + F5; hoặc thêm `hcm.pmtiles` |
| Banner “Chưa có file hcm.pmtiles” + vẫn thấy đường OSM | Đúng hành vi khi thiếu pack | Tạo pack theo mục 4 |
| Có file nhưng vẫn banner / log `hcm.pmtiles … 404` | ASP.NET không map MIME `.pmtiles` (đã fix trong `Program.cs`) hoặc chưa restart API | Restart `dotnet run`; mở URL mục 4.7 phải **200** |
| Có pmtiles nhưng zoom ra bị **cắt trắng** / thiếu Củ Chi–Cần Giờ | Pack build với bbox trung tâm nhỏ | Rebuild `--bounds=106.36,10.35,107.03,11.16` (mục 4.2), copy đè `hcm.pmtiles`, restart API |
| Có pmtiles nhưng chỉ nền, không đường | Style lệch schema (không phải OpenMapTiles) | Dùng Planetiler như mục 4, hoặc sửa `source-layer` trong `hcm-offline.json` |
| CORS / tile lỗi trên Vite | Proxy `/static` chưa tới API | Xem `web/vite.config.ts` proxy `/static` |
| File quá lớn, git chậm | Đúng — **không commit** pmtiles | `.gitignore` đã có `*.pmtiles`; chia sẻ ngoài repo |
| Offline có đường nhưng **không có chữ** | Thiếu glyph `{fontstack}/{range}.pbf` | Repo đã có subset `Noto Sans Regular`; rebuild fonts nếu thiếu CJK |

Kiểm tra nhanh PowerShell:

```powershell
try {
  (Invoke-WebRequest "http://localhost:5020/static/maps/packs/current/hcm.pmtiles" -Method Head).StatusCode
} catch {
  $_.Exception.Response.StatusCode.value__
}
# Mong đợi: 200 (không phải 404)
```

---

## 8. Bảo mật

`IMapPackService` chỉ resolve path **trong** `DataDir` (chặn `../`). Không expose thư mục ngoài pack maps.

---

## 9. Checklist demo / bảo vệ đồ án

- [ ] `hcm.pmtiles` nằm cạnh `manifest.json`
- [ ] Manifest có `bytes` / `sha256` thật
- [ ] `GET .../hcm.pmtiles` → 200
- [ ] UI **Offline**: hết banner, thấy đường phố vector
- [ ] Tắt Wi‑Fi (sau khi đã load pack): map vẫn pan/zoom trong vùng đã cache trình duyệt (PMTiles range); lần đầu vẫn cần đã tải file về máy chủ local

Chúc dựng pack thành công. Nếu chỉ cần demo nhanh khi còn mạng: dùng **Cloud** hoặc để Offline fallback OSM như hiện tại.

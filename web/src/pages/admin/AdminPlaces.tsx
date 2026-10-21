import { useEffect, useMemo, useState } from "react";
import { api } from "../../api/client";
import type { PlaceDto, UpdatePlaceRequest } from "../../api/types";

const CATEGORIES = [
  "Historical",
  "Religious",
  "Museum",
  "Park",
  "Market",
  "Landmark",
  "Entertainment",
  "Nature",
] as const;

const STATUSES = ["Draft", "Published", "Hidden"] as const;

type FormState = {
  name: string;
  descriptionVi: string;
  category: string;
  address: string;
  latitude: string;
  longitude: string;
  triggerRadiusMeters: string;
  openingHours: string;
  ticketPrice: string;
  imageUrl: string;
  tags: string;
  status: string;
};

function emptyForm(): FormState {
  return {
    name: "",
    descriptionVi: "",
    category: "Historical",
    address: "",
    latitude: "",
    longitude: "",
    triggerRadiusMeters: "50",
    openingHours: "",
    ticketPrice: "",
    imageUrl: "",
    tags: "",
    status: "Published",
  };
}

function fromPlace(place: PlaceDto): FormState {
  return {
    name: place.name ?? "",
    descriptionVi: place.description ?? "",
    category: place.category || "Historical",
    address: place.address ?? "",
    latitude: String(place.latitude ?? ""),
    longitude: String(place.longitude ?? ""),
    triggerRadiusMeters: String(place.triggerRadiusMeters ?? 50),
    openingHours: place.openingHours ?? "",
    ticketPrice: place.ticketPrice ?? "",
    imageUrl: place.imageUrl ?? "",
    tags: Array.isArray(place.tags) ? place.tags.join(", ") : "",
    status: place.status || "Published",
  };
}

function toRequest(form: FormState): UpdatePlaceRequest {
  const lat = Number(form.latitude);
  const lng = Number(form.longitude);
  const radius = Number(form.triggerRadiusMeters);
  return {
    name: form.name.trim(),
    descriptionVi: form.descriptionVi.trim(),
    category: form.category,
    address: form.address.trim(),
    latitude: Number.isFinite(lat) ? lat : undefined,
    longitude: Number.isFinite(lng) ? lng : undefined,
    triggerRadiusMeters: Number.isFinite(radius) ? Math.round(radius) : undefined,
    openingHours: form.openingHours.trim(),
    ticketPrice: form.ticketPrice.trim(),
    imageUrl: form.imageUrl.trim(),
    tags: form.tags
      .split(/[,;]/)
      .map((t) => t.trim())
      .filter(Boolean)
      .join(","),
    status: form.status,
  };
}

export default function AdminPlaces() {
  const [places, setPlaces] = useState<PlaceDto[]>([]);
  const [selectedId, setSelectedId] = useState<string>("");
  const [form, setForm] = useState<FormState>(emptyForm);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadPlaces = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.listPlaces({ page: 1, pageSize: 200, sortBy: "name" });
      setPlaces(res.items);
      if (!selectedId && res.items[0]) {
        setSelectedId(res.items[0].id);
        setForm(fromPlace(res.items[0]));
      } else if (selectedId) {
        const current = res.items.find((p) => p.id === selectedId);
        if (current) setForm(fromPlace(current));
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load places");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadPlaces();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return places;
    return places.filter(
      (p) =>
        p.name.toLowerCase().includes(q) ||
        p.id.toLowerCase().includes(q) ||
        p.address.toLowerCase().includes(q),
    );
  }, [places, search]);

  const selectPlace = (place: PlaceDto) => {
    setSelectedId(place.id);
    setForm(fromPlace(place));
    setMessage(null);
    setError(null);
  };

  const setField = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const save = async () => {
    if (!selectedId) return;
    setBusy(true);
    setMessage(null);
    setError(null);
    try {
      const updated = await api.updatePlace(selectedId, toRequest(form));
      setPlaces((prev) => prev.map((p) => (p.id === selectedId ? { ...p, ...updated } : p)));
      setForm(fromPlace(updated));
      setMessage("Đã lưu thay đổi địa điểm.");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message.includes("401") || err.message.toLowerCase().includes("session")
            ? "Phiên đăng nhập hết hạn. Đăng nhập lại admin rồi thử tiếp."
            : err.message
          : "Save failed",
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="portal-card admin-places">
      <h2>Chỉnh sửa địa điểm</h2>
      <p className="portal-muted">
        Cập nhật địa chỉ, giờ mở cửa, giá vé, ảnh và tọa độ. Thay đổi áp dụng ngay trên bản đồ công
        khai.
      </p>

      {error && <p className="portal-error">{error}</p>}
      {message && <p className="portal-success">{message}</p>}

      <div className="admin-places__layout">
        <aside className="admin-places__list">
          <input
            type="search"
            placeholder="Tìm theo tên / GUID / địa chỉ…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search places"
          />
          {loading ? (
            <p className="portal-muted">Đang tải…</p>
          ) : (
            <ul>
              {filtered.map((place) => (
                <li key={place.id}>
                  <button
                    type="button"
                    className={
                      "admin-places__item" +
                      (place.id === selectedId ? " admin-places__item--active" : "")
                    }
                    onClick={() => selectPlace(place)}
                  >
                    <strong>{place.name}</strong>
                    <span>
                      {place.category} · {place.status}
                    </span>
                  </button>
                </li>
              ))}
              {filtered.length === 0 && <li className="portal-muted">Không có điểm khớp.</li>}
            </ul>
          )}
        </aside>

        <section className="admin-places__editor">
          {!selectedId ? (
            <p className="portal-muted">Chọn một điểm bên trái để sửa.</p>
          ) : (
            <form
              className="portal-form"
              onSubmit={(e) => {
                e.preventDefault();
                void save();
              }}
            >
              <label>
                GUID
                <input value={selectedId} readOnly tabIndex={0} className="portal-form__readonly" />
              </label>

              <label>
                Tên
                <input
                  value={form.name}
                  onChange={(e) => setField("name", e.target.value)}
                  required
                />
              </label>

              <div className="portal-form-row">
                <label>
                  Loại
                  <select
                    value={form.category}
                    onChange={(e) => setField("category", e.target.value)}
                  >
                    {CATEGORIES.map((c) => (
                      <option key={c} value={c}>
                        {c}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Trạng thái
                  <select value={form.status} onChange={(e) => setField("status", e.target.value)}>
                    {STATUSES.map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              <label>
                Bán kính geofence (m)
                <input
                  type="number"
                  min={1}
                  value={form.triggerRadiusMeters}
                  onChange={(e) => setField("triggerRadiusMeters", e.target.value)}
                />
              </label>

              <label>
                Địa chỉ
                <textarea
                  rows={2}
                  value={form.address}
                  onChange={(e) => setField("address", e.target.value)}
                />
              </label>

              <div className="portal-form-row">
                <label>
                  Latitude
                  <input
                    type="number"
                    step="any"
                    value={form.latitude}
                    onChange={(e) => setField("latitude", e.target.value)}
                  />
                </label>
                <label>
                  Longitude
                  <input
                    type="number"
                    step="any"
                    value={form.longitude}
                    onChange={(e) => setField("longitude", e.target.value)}
                  />
                </label>
              </div>

              <label>
                Giờ mở cửa
                <textarea
                  rows={2}
                  value={form.openingHours}
                  onChange={(e) => setField("openingHours", e.target.value)}
                />
              </label>

              <label>
                Giá vé
                <textarea
                  rows={3}
                  value={form.ticketPrice}
                  onChange={(e) => setField("ticketPrice", e.target.value)}
                />
              </label>

              <label>
                Ảnh minh họa (URL)
                <input
                  value={form.imageUrl}
                  onChange={(e) => setField("imageUrl", e.target.value)}
                  placeholder="https://…"
                />
              </label>
              {form.imageUrl.trim() && (
                <img
                  className="admin-places__preview"
                  src={form.imageUrl.trim()}
                  alt="Preview"
                  loading="lazy"
                />
              )}

              <label>
                Tags (phân tách bằng dấu phẩy)
                <input
                  value={form.tags}
                  onChange={(e) => setField("tags", e.target.value)}
                  placeholder="lich-su, di-tich"
                />
              </label>

              <label>
                Mô tả (tiếng Việt)
                <textarea
                  rows={6}
                  value={form.descriptionVi}
                  onChange={(e) => setField("descriptionVi", e.target.value)}
                />
              </label>

              <div className="admin-places__actions">
                <button type="submit" className="btn btn--primary" disabled={busy}>
                  {busy ? "Đang lưu…" : "Lưu thay đổi"}
                </button>
                <button
                  type="button"
                  className="btn btn--ghost"
                  disabled={busy || !selectedId}
                  onClick={() => {
                    const current = places.find((p) => p.id === selectedId);
                    if (current) setForm(fromPlace(current));
                    setMessage(null);
                    setError(null);
                  }}
                >
                  Hoàn tác
                </button>
              </div>
            </form>
          )}
        </section>
      </div>
    </div>
  );
}

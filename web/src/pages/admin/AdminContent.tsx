import { useEffect, useState } from "react";
import { api } from "../../api/client";
import type { PlaceDto } from "../../api/types";

const LANGS = ["en", "ja", "ko", "zh"] as const;

export default function AdminContent() {
  const [places, setPlaces] = useState<PlaceDto[]>([]);
  const [placeId, setPlaceId] = useState("");
  const [lang, setLang] = useState<(typeof LANGS)[number]>("en");
  const [text, setText] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void api
      .listPlaces({ page: 1, pageSize: 100 })
      .then((res) => {
        setPlaces(res.items);
        if (res.items[0]) setPlaceId(res.items[0].id);
      })
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load places"));
  }, []);

  useEffect(() => {
    if (!placeId) return;
    let cancelled = false;
    void api
      .getNarration(placeId, lang)
      .then((n) => {
        if (!cancelled) setText(n.text ?? "");
      })
      .catch(() => {
        if (!cancelled) setText("");
      });
    return () => {
      cancelled = true;
    };
  }, [placeId, lang]);

  const saveTranslation = async () => {
    if (!placeId || !text.trim()) return;
    setBusy(true);
    setMessage(null);
    setError(null);
    try {
      await api.updateTranslation({ placeId, lang, text: text.trim() });
      setMessage("Translation saved.");
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

  const regenerate = async () => {
    if (!placeId) return;
    setBusy(true);
    setMessage(null);
    setError(null);
    try {
      await api.regeneratePlaceAudio(placeId);
      setMessage("Queued TTS for vi/en/ja/ko/zh. Open Admin → Audio tasks to watch progress.");
      // Soft-refresh translation/audio status after a short wait.
      window.setTimeout(() => {
        void api.getNarration(placeId, lang).then((n) => setText(n.text ?? text));
      }, 2500);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message.includes("401") || err.message.toLowerCase().includes("session")
            ? "Phiên đăng nhập hết hạn. Đăng nhập lại admin rồi thử tiếp."
            : err.message
          : "Regenerate failed",
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="portal-card">
      <h2>Content — translations &amp; audio</h2>
      {error && <p className="portal-error">{error}</p>}
      {message && <p className="portal-success">{message}</p>}

      <div className="portal-form">
        <label>
          Place
          <select value={placeId} onChange={(e) => setPlaceId(e.target.value)}>
            {places.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Language
          <select value={lang} onChange={(e) => setLang(e.target.value as (typeof LANGS)[number])}>
            {LANGS.map((code) => (
              <option key={code} value={code}>
                {code}
              </option>
            ))}
          </select>
        </label>
        <label>
          Translation text
          <textarea rows={8} value={text} onChange={(e) => setText(e.target.value)} />
        </label>
        <div className="portal-table-actions">
          <button type="button" className="btn btn--accent" disabled={busy} onClick={() => void saveTranslation()}>
            Save translation
          </button>
          <button type="button" className="btn btn--ghost" disabled={busy} onClick={() => void regenerate()}>
            Regenerate all audio
          </button>
        </div>
      </div>
    </div>
  );
}

"""TourismHcm providers sidecar: Edge-TTS + multi-engine free translation."""

from __future__ import annotations

import asyncio
import hashlib
import logging
import os
import time
from pathlib import Path

import edge_tts
import httpx
from deep_translator import GoogleTranslator, MyMemoryTranslator
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel, Field

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("providers")

CACHE_DIR = Path(os.environ.get("PROVIDERS_CACHE_DIR", "/data/cache"))
CACHE_DIR.mkdir(parents=True, exist_ok=True)

VOICES: dict[str, str] = {
    "vi": "vi-VN-HoaiMyNeural",
    "en": "en-US-JennyNeural",
    "zh": "zh-CN-XiaoxiaoNeural",
    "ja": "ja-JP-NanamiNeural",
    "ko": "ko-KR-SunHiNeural",
}

# Google free tier codes
GOOGLE_LANG: dict[str, str] = {
    "vi": "vi",
    "en": "en",
    "ja": "ja",
    "ko": "ko",
    "zh": "zh-CN",
}

# MyMemory prefers locale-style codes
MYMEMORY_LANG: dict[str, str] = {
    "vi": "vi-VN",
    "en": "en-GB",
    "ja": "ja-JP",
    "ko": "ko-KR",
    "zh": "zh-CN",
}

app = FastAPI(title="TourismHcm Providers", version="1.1.0")

_translate_lock = asyncio.Lock()
_last_translate_at = 0.0


class TtsRequest(BaseModel):
    text: str = Field(min_length=1, max_length=8000)
    lang: str = Field(min_length=2, max_length=16)


class TranslateRequest(BaseModel):
    text: str = Field(min_length=1, max_length=8000)
    source_lang: str = Field(min_length=2, max_length=16)
    target_lang: str = Field(min_length=2, max_length=16)


def _normalize_lang(lang: str) -> str:
    code = (lang or "en").strip().lower().split("-")[0]
    return code if code in VOICES else "en"


def _cache_key(text: str, lang: str) -> str:
    return hashlib.md5(f"{text}:{lang}".encode("utf-8")).hexdigest()


def _mymemory(text: str, src: str, tgt: str) -> str | None:
    try:
        out = MyMemoryTranslator(
            source=MYMEMORY_LANG[src],
            target=MYMEMORY_LANG[tgt],
        ).translate(text)
        return out.strip() if out and out.strip() else None
    except Exception as exc:  # noqa: BLE001
        log.warning("MyMemory %s->%s failed: %s", src, tgt, exc)
        return None


def _google(text: str, src: str, tgt: str) -> str | None:
    try:
        out = GoogleTranslator(source=GOOGLE_LANG[src], target=GOOGLE_LANG[tgt]).translate(text)
        return out.strip() if out and out.strip() else None
    except Exception as exc:  # noqa: BLE001
        log.warning("Google %s->%s failed: %s", src, tgt, exc)
        return None


def _google_gtx(text: str, src: str, tgt: str) -> str | None:
    """Unofficial Google translate endpoint (client=gtx) — often bypasses deep-translator 429."""
    try:
        params = {
            "client": "gtx",
            "sl": GOOGLE_LANG[src],
            "tl": GOOGLE_LANG[tgt],
            "dt": "t",
            "q": text,
        }
        with httpx.Client(timeout=30.0) as client:
            resp = client.get("https://translate.googleapis.com/translate_a/single", params=params)
            resp.raise_for_status()
            data = resp.json()
            parts = [chunk[0] for chunk in data[0] if chunk and chunk[0]]
            out = "".join(parts).strip()
            return out or None
    except Exception as exc:  # noqa: BLE001
        log.warning("Google GTX %s->%s failed: %s", src, tgt, exc)
        return None


def _translate_sync(text: str, source: str, target: str) -> tuple[str, str]:
    """Prefer GTX/Google, then MyMemory (+ en pivot). Returns (translated, engine)."""
    src = _normalize_lang(source)
    tgt = _normalize_lang(target)
    payload = text[:4500]

    out = _google_gtx(payload, src, tgt)
    if out:
        return out, "google-gtx"

    out = _google(payload, src, tgt)
    if out:
        return out, "google"

    out = _mymemory(payload, src, tgt)
    if out:
        return out, "mymemory"

    if src != "en" and tgt != "en":
        mid = _google_gtx(payload, src, "en") or _mymemory(payload, src, "en")
        if mid:
            final = _google_gtx(mid, "en", tgt) or _mymemory(mid, "en", tgt)
            if final:
                return final, "pivot-en"

    time.sleep(1.5)
    out = _google_gtx(payload, src, tgt)
    if out:
        return out, "google-gtx-retry"

    raise RuntimeError("All translation engines failed")


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok", "edge_tts": "7.3.1"}


@app.post("/tts")
async def synthesize(body: TtsRequest) -> Response:
    lang = _normalize_lang(body.lang)
    voice = VOICES[lang]
    key = _cache_key(body.text, lang)
    cache_path = CACHE_DIR / f"{key}.mp3"

    if cache_path.is_file() and cache_path.stat().st_size > 0:
        data = cache_path.read_bytes()
        return Response(
            content=data,
            media_type="audio/mpeg",
            headers={"X-Cache": "HIT", "X-Voice": voice},
        )

    last_error: Exception | None = None
    for attempt in range(3):
        try:
            communicate = edge_tts.Communicate(body.text, voice)
            await communicate.save(str(cache_path))
            if cache_path.is_file() and cache_path.stat().st_size > 0:
                data = cache_path.read_bytes()
                return Response(
                    content=data,
                    media_type="audio/mpeg",
                    headers={"X-Cache": "MISS", "X-Voice": voice, "X-Attempt": str(attempt + 1)},
                )
        except Exception as exc:  # noqa: BLE001
            last_error = exc
            log.warning("Edge-TTS attempt %s failed: %s", attempt + 1, exc)
            await asyncio.sleep(0.8 * (attempt + 1))

    raise HTTPException(status_code=503, detail=f"Edge-TTS failed: {last_error}")


@app.post("/translate")
async def translate(body: TranslateRequest) -> dict[str, str]:
    global _last_translate_at

    source = _normalize_lang(body.source_lang)
    target = _normalize_lang(body.target_lang)

    if source == target:
        return {"translated_text": body.text, "provider": "passthrough"}

    async with _translate_lock:
        # Soft rate-limit (~2 req/s) to reduce Google 429s
        elapsed = time.monotonic() - _last_translate_at
        if elapsed < 0.55:
            await asyncio.sleep(0.55 - elapsed)
        try:
            translated, engine = await asyncio.to_thread(
                _translate_sync, body.text, source, target
            )
        except Exception as exc:  # noqa: BLE001
            raise HTTPException(status_code=503, detail=f"Translate failed: {exc}") from exc
        finally:
            _last_translate_at = time.monotonic()

    return {"translated_text": translated, "provider": engine}

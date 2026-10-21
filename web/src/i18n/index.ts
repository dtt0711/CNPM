import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";

import vi from "./locales/vi.json";
import en from "./locales/en.json";
import ja from "./locales/ja.json";
import ko from "./locales/ko.json";
import zh from "./locales/zh.json";

export const UI_LANGS = ["vi", "en", "ja", "ko", "zh"] as const;
export type UiLang = (typeof UI_LANGS)[number];

const STORAGE_KEY = "tourismhcm.uiLang";

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      vi: { translation: vi },
      en: { translation: en },
      ja: { translation: ja },
      ko: { translation: ko },
      zh: { translation: zh },
    },
    fallbackLng: "en",
    supportedLngs: UI_LANGS,
    detection: {
      order: ["localStorage", "navigator"],
      lookupLocalStorage: STORAGE_KEY,
      caches: ["localStorage"],
    },
    interpolation: { escapeValue: false },
  });

export function setUiLanguage(lang: UiLang): void {
  void i18n.changeLanguage(lang);
  try {
    window.localStorage.setItem(STORAGE_KEY, lang);
  } catch {
    // ignore storage failures (private mode, etc.)
  }
  void mergeUiBundleForLocale(lang);
}

/** Optionally merge server UI bundle when status is ready; local JSON stays primary. */
export async function mergeUiBundleForLocale(lang: UiLang): Promise<void> {
  try {
    const { api } = await import("../api/client");
    const bundle = await api.getBundle(lang);
    if (bundle.status !== "ready") {
      console.info("[i18n] UI bundle", lang, "status:", bundle.status);
      return;
    }
    let extra: Record<string, unknown> = {};
    try {
      extra = JSON.parse(bundle.payloadJson) as Record<string, unknown>;
    } catch {
      console.warn("[i18n] UI bundle payload is not valid JSON");
      return;
    }
    i18n.addResourceBundle(lang, "translation", extra, true, true);
    console.info("[i18n] merged UI bundle for", lang, bundle.isFallback ? "(fallback)" : "");
  } catch (err) {
    console.debug("[i18n] UI bundle fetch skipped:", err);
  }
}

export default i18n;

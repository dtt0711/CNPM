import { useTranslation } from "react-i18next";
import { UI_LANGS, type UiLang } from "../i18n";

export interface LanguageSelectProps {
  value: UiLang;
  onChange: (lang: UiLang) => void;
}

export function LanguageSelect({ value, onChange }: LanguageSelectProps) {
  const { t } = useTranslation();

  return (
    <label className="lang-select">
      <span className="lang-select__label">{t("language.label")}</span>
      <select
        className="lang-select__input"
        value={value}
        onChange={(e) => onChange(e.target.value as UiLang)}
        aria-label={t("language.label")}
      >
        {UI_LANGS.map((code) => (
          <option key={code} value={code}>
            {t(`language.${code}`)}
          </option>
        ))}
      </select>
    </label>
  );
}

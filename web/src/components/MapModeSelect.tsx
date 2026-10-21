import { useTranslation } from "react-i18next";
import type { MapMode } from "../maps/mapConfig";

export interface MapModeSelectProps {
  value: MapMode;
  onChange: (mode: MapMode) => void;
}

export function MapModeSelect({ value, onChange }: MapModeSelectProps) {
  const { t } = useTranslation();
  const modes: { value: MapMode; label: string }[] = [
    { value: "cloud", label: t("map.cloud") },
    { value: "offline", label: t("map.offline") },
    { value: "hybrid", label: t("map.hybrid") },
  ];

  return (
    <label className="map-mode-select" title={t(`map.${value}Hint`)}>
      <span className="map-mode-select__label">{t("map.mode")}</span>
      <select
        className="map-mode-select__control"
        value={value}
        onChange={(e) => onChange(e.target.value as MapMode)}
        aria-label={t("map.mode")}
      >
        {modes.map((m) => (
          <option key={m.value} value={m.value} title={t(`map.${m.value}Hint`)}>
            {m.label}
          </option>
        ))}
      </select>
    </label>
  );
}

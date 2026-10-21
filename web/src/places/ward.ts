/** Format a locality name as a ward label without duplicating the prefix. */
export function formatWard(name: string, wardWord: string): string {
  const trimmed = name.trim();
  if (!trimmed) return "";
  if (/^(phường|xã|thị trấn|ward)\b/i.test(trimmed)) return trimmed;
  return `${wardWord} ${trimmed}`;
}

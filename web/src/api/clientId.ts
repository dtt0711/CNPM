// Generates and persists a per-device client id (UUID v4) in localStorage.
// Sent as the "X-Client-Id" header (and as TriggerRequest.clientId in the
// request body) so the backend can enforce the per-client geofence cooldown.

const STORAGE_KEY = "tourismhcm.clientId";

function generateUuidV4(): string {
  const cryptoObj: Crypto | undefined =
    typeof crypto !== "undefined" ? crypto : undefined;

  if (cryptoObj?.randomUUID) {
    return cryptoObj.randomUUID();
  }

  // Fallback for environments without crypto.randomUUID (older browsers).
  const bytes = new Uint8Array(16);
  if (cryptoObj?.getRandomValues) {
    cryptoObj.getRandomValues(bytes);
  } else {
    for (let i = 0; i < bytes.length; i += 1) {
      bytes[i] = Math.floor(Math.random() * 256);
    }
  }

  bytes[6] = (bytes[6] & 0x0f) | 0x40; // version 4
  bytes[8] = (bytes[8] & 0x3f) | 0x80; // variant 10

  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
  return [
    hex.slice(0, 8),
    hex.slice(8, 12),
    hex.slice(12, 16),
    hex.slice(16, 20),
    hex.slice(20, 32),
  ].join("-");
}

/** Returns the persisted client id, generating and storing a new one if absent. */
export function getOrCreateClientId(storage: Storage = window.localStorage): string {
  try {
    const existing = storage.getItem(STORAGE_KEY);
    if (existing) {
      return existing;
    }
    const created = generateUuidV4();
    storage.setItem(STORAGE_KEY, created);
    return created;
  } catch {
    // Storage unavailable (e.g. privacy mode) - fall back to a session-only id.
    return generateUuidV4();
  }
}

export { generateUuidV4 };

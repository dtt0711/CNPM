import type { AuthResponse } from "../api/types";

const STORAGE_KEY = "tourismhcm.auth";

export interface StoredSession {
  userId: string;
  email: string;
  displayName: string;
  roles: string[];
  permissions: string[];
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
}

let memoryAccessToken: string | null = null;

function fromAuthResponse(res: AuthResponse): StoredSession {
  return {
    userId: res.userId,
    email: res.email,
    displayName: res.displayName,
    roles: [...res.roles],
      permissions: [...res.permissions],
    accessToken: res.accessToken,
    accessTokenExpiresAt: res.accessTokenExpiresAt,
    refreshToken: res.refreshToken,
    refreshTokenExpiresAt: res.refreshTokenExpiresAt,
  };
}

export function loadSession(): StoredSession | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StoredSession>;
    if (!parsed.accessToken) return null;
    const session: StoredSession = {
      userId: parsed.userId ?? "",
      email: parsed.email ?? "",
      displayName: parsed.displayName ?? "",
      roles: Array.isArray(parsed.roles) ? parsed.roles.map(String) : [],
      permissions: Array.isArray(parsed.permissions) ? parsed.permissions.map(String) : [],
      accessToken: parsed.accessToken,
      accessTokenExpiresAt: parsed.accessTokenExpiresAt ?? "",
      refreshToken: parsed.refreshToken ?? "",
      refreshTokenExpiresAt: parsed.refreshTokenExpiresAt ?? "",
    };
    memoryAccessToken = session.accessToken;
    return session;
  } catch {
    return null;
  }
}

export function saveSession(res: AuthResponse): StoredSession {
  const session = fromAuthResponse(res);
  memoryAccessToken = session.accessToken;
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    // private mode / quota
  }
  return session;
}

export function clearSession(): void {
  memoryAccessToken = null;
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // ignore
  }
}

export function getAccessToken(): string | null {
  if (memoryAccessToken) return memoryAccessToken;
  const session = loadSession();
  return session?.accessToken ?? null;
}

export function hasAnyRole(session: StoredSession | null, roles: string[]): boolean {
  if (!session) return false;
  const current = Array.isArray(session.roles) ? session.roles : [];
  return roles.some((r) => current.includes(r));
}

export function isAdmin(session: StoredSession | null): boolean {
  return hasAnyRole(session, ["admin", "super_admin"]);
}

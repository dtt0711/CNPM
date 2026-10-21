import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import type { AuthResponse, LoginRequest, RegisterRequest } from "../api/types";
import { api } from "../api/client";
import {
  clearSession,
  isAdmin,
  loadSession,
  saveSession,
  type StoredSession,
} from "./session";

interface AuthContextValue {
  session: StoredSession | null;
  isAdmin: boolean;
  login: (body: LoginRequest) => Promise<AuthResponse>;
  register: (body: RegisterRequest) => Promise<AuthResponse>;
  logout: () => Promise<void>;
  applyAuth: (res: AuthResponse) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(() => loadSession());

  const applyAuth = useCallback((res: AuthResponse) => {
    setSession(saveSession(res));
  }, []);

  const login = useCallback(async (body: LoginRequest) => {
    const res = await api.login(body);
    applyAuth(res);
    return res;
  }, [applyAuth]);

  const register = useCallback(async (body: RegisterRequest) => {
    const res = await api.register(body);
    applyAuth(res);
    return res;
  }, [applyAuth]);

  const logout = useCallback(async () => {
    const refresh = session?.refreshToken;
    clearSession();
    setSession(null);
    if (refresh) {
      try {
        await api.logout({ refreshToken: refresh });
      } catch {
        // cookies cleared server-side when possible
      }
    }
  }, [session?.refreshToken]);

  const value = useMemo(
    () => ({
      session,
      isAdmin: isAdmin(session),
      login,
      register,
      logout,
      applyAuth,
    }),
    [session, login, register, logout, applyAuth],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

import type { AuthSession } from "./api";

const sessionKey = "ebr.auth.session";

export function loadSession(): AuthSession | null {
  const serialized = sessionStorage.getItem(sessionKey);
  if (!serialized) return null;

  try {
    const session = JSON.parse(serialized) as AuthSession;
    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      clearSession();
      return null;
    }
    return session;
  } catch {
    clearSession();
    return null;
  }
}

export function saveSession(session: AuthSession): void {
  sessionStorage.setItem(sessionKey, JSON.stringify(session));
}

export function clearSession(): void {
  sessionStorage.removeItem(sessionKey);
}

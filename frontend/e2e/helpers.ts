import { type APIRequestContext, type Page, expect, request as playwrightRequest } from "@playwright/test";

/**
 * URL de la API .NET. Debe estar corriendo aparte de Vite (ver playwright.config.ts).
 * El frontend recibe la misma URL por `VITE_API_URL` desde `webServer.env`.
 */
export const API_URL = (process.env.E2E_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export const DEMO_PASSWORD = "EbrLocal2026!";

/** Cuentas de demostración sembradas por `DevelopmentDataSeeder` en el primer arranque. */
export const DEMO_USERS = {
  admin: { email: "admin@ebr.local", roleLabel: "Administrador del Sistema" },
  empresa: { email: "empresa@ebr.local", roleLabel: "Admin de Empresa" },
  delegado: { email: "delegado@ebr.local", roleLabel: "Usuario Delegado" },
  coordinador: { email: "coordinador@ebr.local", roleLabel: "Coordinador" },
  tecnico: { email: "tecnico@ebr.local", roleLabel: "Técnico Evaluador" },
} as const;

export type DemoUserKey = keyof typeof DEMO_USERS;

export interface ApiSession {
  accessToken: string;
  refreshToken: string;
  role: string;
  fullName: string;
  email: string;
}

const sessionCache = new Map<string, ApiSession>();

/**
 * Inicia sesión contra la API y devuelve los tokens. Cachea la sesión por correo para no repetir
 * autenticaciones (el endpoint `/api/auth/*` tiene un límite de 20 peticiones por minuto fuera del
 * entorno de pruebas). Ante un `429` espera a que se abra la ventana y reintenta una vez.
 */
export async function apiLogin(
  api: APIRequestContext,
  email: string,
  password: string = DEMO_PASSWORD,
): Promise<ApiSession> {
  const key = email.trim().toLowerCase();
  const cached = sessionCache.get(key);
  if (cached) return cached;

  for (let attempt = 0; attempt < 2; attempt++) {
    const response = await api.post(`${API_URL}/api/auth/login`, { data: { email: key, password } });
    if (response.ok()) {
      const session = (await response.json()) as ApiSession;
      sessionCache.set(key, session);
      return session;
    }
    if (response.status() === 429 && attempt === 0) {
      await new Promise((resolve) => setTimeout(resolve, 61_000));
      continue;
    }
    expect(response.ok(), `login ${email} => ${response.status()}`).toBeTruthy();
  }
  throw new Error(`login ${email}: agotados los reintentos`);
}

/** Cabecera Authorization lista para usar. */
export function bearer(session: ApiSession): Record<string, string> {
  return { Authorization: `Bearer ${session.accessToken}` };
}

/** Contexto de API independiente del navegador, para las etapas que el frontend aún no cubre. */
export async function newApiContext(): Promise<APIRequestContext> {
  return playwrightRequest.newContext();
}

/**
 * Inicia sesión por la interfaz. La pantalla de acceso usa un input con `<label>` asociado, por lo
 * que los selectores son estables por rol/etiqueta y no por clase.
 */
export async function uiLogin(page: Page, email: string, password: string = DEMO_PASSWORD): Promise<void> {
  await page.goto("/");
  await page.getByLabel("Correo electrónico").fill(email);
  await page.getByLabel("Contraseña").fill(password);

  const loginButton = page.getByRole("button", { name: "Iniciar sesión", exact: true });
  // El endpoint de autenticación limita a 20 peticiones por minuto. En una corrida de varios
  // navegadores ese límite puede alcanzarse; se espera a que se abra la ventana y se reintenta.
  const rateLimited = page.getByText(/API esté disponible/i);
  for (let attempt = 0; attempt < 3; attempt++) {
    await loginButton.click();
    const outcome = await Promise.race([
      loginButton.waitFor({ state: "hidden", timeout: 15_000 }).then(() => "ok" as const).catch(() => "timeout" as const),
      rateLimited.waitFor({ state: "visible", timeout: 15_000 }).then(() => "limited" as const).catch(() => "timeout" as const),
    ]);
    if (outcome === "ok") return;
    if (outcome === "limited") {
      await page.waitForTimeout(61_000);
      continue;
    }
    break;
  }
  await expect(loginButton).toBeHidden({ timeout: 15_000 });
}

/**
 * Determina si la base tiene sembrados la plantilla BPM publicada y una versión de reglas de riesgo.
 * Sin eso, las etapas de evaluación en adelante no pueden ejecutarse (ver README).
 */
export async function catalogsAreSeeded(api: APIRequestContext, admin: ApiSession): Promise<boolean> {
  const templates = await api.get(`${API_URL}/api/evaluation-templates/`, { headers: bearer(admin) });
  if (!templates.ok()) return false;
  const list = (await templates.json()) as Array<{ status?: string }>;
  const hasPublishedTemplate = list.some((t) => (t.status ?? "").toUpperCase() === "PUBLISHED");

  const factors = await api.get(`${API_URL}/api/catalogs/structural-factors`, { headers: bearer(admin) });
  const hasFactors = factors.ok() && ((await factors.json()) as unknown[]).length > 0;

  return hasPublishedTemplate && hasFactors;
}

/** Comprueba que la API responde; si no, las specs que dependen del stack se marcan como omitidas. */
export async function apiIsUp(api: APIRequestContext): Promise<boolean> {
  try {
    const health = await api.get(`${API_URL}/health`, { timeout: 4_000 });
    return health.ok();
  } catch {
    return false;
  }
}

export function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

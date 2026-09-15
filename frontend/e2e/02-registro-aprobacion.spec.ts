import { test, expect, type APIRequestContext } from "@playwright/test";
import {
  API_URL,
  DEMO_USERS,
  DEMO_PASSWORD,
  apiIsUp,
  apiLogin,
  bearer,
  newApiContext,
  uiLogin,
  uniqueSuffix,
} from "./helpers";

/**
 * Etapas: registro de usuario y aprobación administrativa.
 *
 * El registro se hace por la API (`POST /api/auth/register`) porque el formulario multipaso de la
 * interfaz incluye la subida de la carta de autorización; la aprobación sí se ejerce por la interfaz
 * de administración (`UserValidationModal`), que es donde el requisito la sitúa.
 */
test.describe.serial("Registro y aprobación administrativa", () => {
  let api: APIRequestContext;
  const suffix = uniqueSuffix();
  const candidate = {
    fullName: `Solicitante E2E ${suffix}`,
    email: `e2e.registro.${suffix}@ebr.local`,
    documentNumber: `E2E-${suffix}`,
    phoneNumber: "8090000000",
    password: DEMO_PASSWORD,
    requestedRole: "ADMINISTRADOR_EMPRESA",
    authorizationLetterFileName: "carta-autorizacion.pdf",
    authorizationLetterMimeType: "application/pdf",
    authorizationLetterSizeBytes: 2048,
    authorizationLetterHash: `sha256-${suffix}`,
    authorizationLetterStorageReference: `registro/${suffix}/carta.pdf`,
  };

  test.beforeAll(async () => {
    api = await newApiContext();
    test.skip(!(await apiIsUp(api)), "La API no responde; arranca API + PostgreSQL.");
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test("el registro sin carta de autorización se rechaza (RF-04/RF-05)", async () => {
    const response = await api.post(`${API_URL}/api/auth/register`, {
      data: {
        fullName: candidate.fullName,
        email: `sin-carta.${suffix}@ebr.local`,
        documentNumber: `NC-${suffix}`,
        phoneNumber: candidate.phoneNumber,
        password: candidate.password,
        requestedRole: candidate.requestedRole,
      },
    });
    expect(response.status(), "sin metadatos de carta debe ser 400").toBe(400);
  });

  test("un registro válido queda pendiente de aprobación", async () => {
    const response = await api.post(`${API_URL}/api/auth/register`, { data: candidate });
    expect(response.ok(), `register => ${response.status()}`).toBeTruthy();

    // Todavía no puede iniciar sesión: está pendiente.
    const login = await api.post(`${API_URL}/api/auth/login`, {
      data: { email: candidate.email, password: candidate.password },
    });
    expect(login.ok(), "un usuario no aprobado no debe poder iniciar sesión").toBeFalsy();
  });

  test("aparece en la bandeja del administrador (API)", async () => {
    const admin = await apiLogin(api, DEMO_USERS.admin.email);
    const pending = await api.get(`${API_URL}/api/users/pending`, { headers: bearer(admin) });
    expect(pending.ok()).toBeTruthy();
    const list = (await pending.json()) as Array<{ email: string }>;
    expect(list.some((u) => u.email.toLowerCase() === candidate.email.toLowerCase())).toBeTruthy();
  });

  test("el administrador aprueba la solicitud desde la interfaz", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.admin.email);

    await page.getByRole("button", { name: /Configuración/i }).first().click();
    await page.getByRole("button", { name: /Revisar solicitudes/i }).click();
    await expect(page.getByRole("heading", { name: /Solicitudes de acceso pendientes/i })).toBeVisible();

    const card = page.locator("article", { hasText: candidate.email });
    await expect(card).toBeVisible();
    await card.getByRole("button", { name: "Aprobar" }).click();
    await expect(card).toBeHidden({ timeout: 10_000 });
  });

  test("tras la aprobación el usuario ya puede iniciar sesión", async () => {
    const login = await api.post(`${API_URL}/api/auth/login`, {
      data: { email: candidate.email, password: candidate.password },
    });
    expect(login.ok(), `login post-aprobación => ${login.status()}`).toBeTruthy();
  });
});

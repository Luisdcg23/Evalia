import { test, expect } from "@playwright/test";
import { DEMO_USERS, apiIsUp, newApiContext, uiLogin } from "./helpers";

/**
 * Etapa: acceso y enrutado por rol.
 * Cada cuenta de demostración inicia sesión por la interfaz y aterriza en el panel de su rol.
 */
test.describe("Autenticación y enrutado por rol", () => {
  test.beforeAll(async () => {
    const api = await newApiContext();
    const up = await apiIsUp(api);
    await api.dispose();
    test.skip(!up, "La API no responde en /health; arranca la API y PostgreSQL antes de las E2E.");
  });

  test("credenciales inválidas muestran un error y no crean sesión", async ({ page }) => {
    await page.goto("/");
    await page.getByLabel("Correo electrónico").fill(DEMO_USERS.admin.email);
    await page.getByLabel("Contraseña").fill("clave-incorrecta");
    await page.getByRole("button", { name: "Iniciar sesión", exact: true }).click();
    await expect(page.getByText(/incorrect/i)).toBeVisible();
    await expect(page.getByRole("button", { name: "Iniciar sesión", exact: true })).toBeVisible();
  });

  test("administrador entra al panel de administración", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.admin.email);
    await expect(page.getByRole("button", { name: /Configuración/i }).first()).toBeVisible();
  });

  test("coordinador entra a su panel", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.coordinador.email);
    await expect(page.getByText(/Asignaciones|Coordinaci/i).first()).toBeVisible();
  });

  test("técnico evaluador entra a su panel", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.tecnico.email);
    await expect(page.getByText(/Evaluaci/i).first()).toBeVisible();
  });

  test("administrador de empresa entra al portal de empresa", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.empresa.email);
    await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible();
  });

  test("usuario delegado entra al portal de empresa", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.delegado.email);
    await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible();
  });
});

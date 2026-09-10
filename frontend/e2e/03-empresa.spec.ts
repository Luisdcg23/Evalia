import { test, expect } from "@playwright/test";
import { DEMO_USERS, apiIsUp, newApiContext, uiLogin } from "./helpers";

/**
 * Etapa: empresa. El administrador de empresa consulta y edita el perfil real de su empresa
 * (`GET /api/companies/{id}`, `PUT /api/companies/{id}` con control de concurrencia optimista).
 */
test.describe("Portal de empresa", () => {
  test.beforeAll(async () => {
    const api = await newApiContext();
    const up = await apiIsUp(api);
    await api.dispose();
    test.skip(!up, "La API no responde; arranca API + PostgreSQL.");
  });

  test("carga el perfil de la empresa con datos reales", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.empresa.email);

    await page.getByRole("navigation").getByRole("button", { name: "Mi Perfil" }).click();
    await expect(page.getByRole("heading", { name: "Perfil de Empresa" }).first()).toBeVisible();
    // El botón de guardar del perfil real (no el simulado) dice "Guardar Cambios".
    await expect(page.getByRole("button", { name: /Guardar Cambios/i })).toBeVisible();
  });

  test("muestra el historial de evaluaciones de la empresa", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.empresa.email);
    await page.getByRole("navigation").getByRole("button", { name: "Evaluaciones" }).click();
    await expect(page.getByRole("heading", { name: "Historial de Evaluaciones" })).toBeVisible();
  });
});

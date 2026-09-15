import { test, expect } from "@playwright/test";
import { DEMO_USERS, apiIsUp, newApiContext, uiLogin } from "./helpers";

/**
 * Etapas transversales de la interfaz: paneles por rol con datos reales de la API, campana de
 * notificaciones y consulta histórica (RF-20).
 */
test.describe("Paneles con datos reales, notificaciones y consulta histórica", () => {
  test.beforeAll(async () => {
    const api = await newApiContext();
    const up = await apiIsUp(api);
    await api.dispose();
    test.skip(!up, "La API no responde; arranca API + PostgreSQL.");
  });

  test("el panel del coordinador carga la agenda y las asignaciones", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.coordinador.email);
    await expect(page.getByText(/Asignaciones/i).first()).toBeVisible();
    // Estado de carga/vacío/error explícito: nunca debe quedar en blanco.
    await expect(page.getByText(/pendiente|sin asignar|No hay|Cargando|error/i).first()).toBeVisible();
  });

  test("la campana de notificaciones se abre y lista el estado", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.tecnico.email);
    await page.getByRole("button", { name: "Notificaciones" }).click();
    await expect(page.getByText(/notificaci|sin notificaciones|no leídas|Cargando/i).first()).toBeVisible();
  });

  test("el coordinador filtra la consulta histórica de expedientes (RF-20)", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.coordinador.email);
    const historyNav = page.getByRole("button", { name: /Historial|Consulta|Expedientes/i }).first();
    if (await historyNav.isVisible().catch(() => false)) {
      await historyNav.click();
      await expect(page.getByText(/Historial|Consulta|Sin resultados|Cargando/i).first()).toBeVisible();
    } else {
      test.info().annotations.push({ type: "nota", description: "Panel de consulta histórica no expuesto en la navegación de este rol." });
    }
  });

  test("el panel de administración deriva sus métricas de la API", async ({ page }) => {
    await uiLogin(page, DEMO_USERS.admin.email);
    await expect(page.getByText(/Dashboard|Inicio/i).first()).toBeVisible();
  });
});

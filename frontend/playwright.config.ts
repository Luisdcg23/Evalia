import { defineConfig, devices } from "@playwright/test";

/**
 * Configuración de las pruebas de extremo a extremo del ciclo EBR/BPM.
 *
 * `webServer` levanta Vite automáticamente. La API .NET y PostgreSQL deben estar corriendo aparte:
 *
 *   1. PostgreSQL 18 con la base `ebr_bpm` y las 20 migraciones aplicadas.
 *   2. API en `http://localhost:5080` (entorno Development, que siembra las cuentas de demostración).
 *   3. Catálogos de riesgo y plantilla BPM sembrados para las etapas de evaluación en adelante
 *      (ver README, "Siembra de catálogos normativos").
 *
 * Variables de entorno opcionales:
 *   E2E_BASE_URL  URL de la interfaz (por defecto http://localhost:5183).
 *   E2E_API_URL   URL de la API que consumen las specs y que se pasa a Vite (por defecto
 *                 http://localhost:5080).
 */
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:5183";
const apiURL = process.env.E2E_API_URL ?? "http://localhost:5080";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [["list"], ["html", { open: "never" }]],
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } },
  ],
  webServer: {
    command: `node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5183 --strictPort`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: { VITE_API_URL: apiURL },
  },
});

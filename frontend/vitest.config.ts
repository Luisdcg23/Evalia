import { defineConfig, mergeConfig } from "vitest/config";
import viteConfig from "./vite.config";

// Vitest reutiliza la configuración de Vite (plugins y alias) y solo acota los archivos de prueba:
// las pruebas unitarias viven en src/; las specs de Playwright (e2e/) las ejecuta `playwright test`.
export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      include: ["src/**/*.{test,spec}.{ts,tsx}"],
      exclude: ["e2e/**", "**/node_modules/**", "**/dist/**"],
    },
  }),
);

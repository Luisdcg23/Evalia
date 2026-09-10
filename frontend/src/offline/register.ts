import { syncPendingResponses } from "./sync";

/**
 * Registra el trabajador de servicio que deja la aplicación disponible sin conexión. Devuelve
 * `false` en navegadores que no lo admiten: la aplicación sigue funcionando en línea, solo pierde
 * la instalación y el arranque sin red.
 */
export async function registerServiceWorker(
  container: ServiceWorkerContainer | undefined = globalThis.navigator?.serviceWorker,
): Promise<boolean> {
  if (!container) return false;
  await container.register("/sw.js", { scope: "/" });
  return true;
}

/**
 * Vacía la cola en cuanto vuelve la red. El envío requiere sesión: sin credenciales la
 * sincronización se pospone hasta el siguiente aviso, con lo capturado intacto en la cola.
 */
export function startAutoSync(
  getAccessToken: () => string | null,
  target: EventTarget = globalThis.window,
): () => void {
  const sync = () => {
    const accessToken = getAccessToken();
    if (accessToken) void syncPendingResponses(accessToken);
  };
  target.addEventListener("online", sync);
  return () => target.removeEventListener("online", sync);
}

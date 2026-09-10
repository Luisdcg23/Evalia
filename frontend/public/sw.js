/*
 * Trabajador de servicio de Evalia. Mantiene disponible el armazón de la aplicación cuando el técnico
 * evalúa sin cobertura. Deliberadamente no guarda en caché nada de /api: los datos del expediente se
 * consultan siempre contra el servidor, y lo capturado sin red viaja por la cola local (IndexedDB),
 * no por respuestas almacenadas aquí. Servir una lectura vieja del expediente sería peor que fallar.
 */
const CACHE = "evalia-shell-v1";
const SHELL = ["/", "/index.html", "/manifest.webmanifest", "/icono-192.png", "/icono-512.png"];

self.addEventListener("install", event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(key => key !== CACHE).map(key => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", event => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin || url.pathname.startsWith("/api/")) return;

  // Navegación: se intenta la red y se cae al armazón guardado, para que abrir la aplicación sin
  // conexión muestre la interfaz en lugar del error del navegador.
  if (request.mode === "navigate") {
    event.respondWith(fetch(request).catch(() => caches.match("/index.html").then(cached => cached ?? Response.error())));
    return;
  }

  // Recursos estáticos: los nombres que emite la compilación llevan huella, así que lo almacenado
  // nunca queda obsoleto para una versión dada.
  event.respondWith(
    caches.match(request).then(cached => cached ?? fetch(request).then(response => {
      if (response.ok) {
        const copy = response.clone();
        caches.open(CACHE).then(cache => cache.put(request, copy));
      }
      return response;
    })),
  );
});

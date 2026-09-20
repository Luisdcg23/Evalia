/**
 * Cola de salida (outbox) de la captura en campo. El técnico evalúa en establecimientos donde la
 * conexión puede faltar, así que cada respuesta se guarda primero en IndexedDB y se envía cuando la
 * red vuelve. La clave es la pregunta —evaluación e ítem—, no el instante de la captura: reguardar
 * la misma pregunta reemplaza lo pendiente en lugar de acumular envíos, y reenviar una entrada ya
 * aplicada es inocuo porque la API guarda respuestas de forma idempotente sobre ese mismo par.
 */
export interface OutboxResponse {
  ownerId?: string;
  operationId?: string;
  evaluationId: number;
  itemId: number;
  optionCode: string;
  observations: string;
  comments: string;
}

export interface OutboxFlushResult {
  sent: number;
  failed: number;
}

const databaseName = "evalia-outbox";
const storeName = "respuestas";

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(databaseName, 1);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(storeName)) {
        request.result.createObjectStore(storeName, { keyPath: "key" });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function run<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

const ownerOf = (response: OutboxResponse) => response.ownerId?.trim() || "anonymous";
const operationId = () => globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
const keyOf = (response: OutboxResponse) => `${ownerOf(response)}:${response.evaluationId}:${response.itemId}`;

export async function enqueueResponse(response: OutboxResponse): Promise<void> {
  const database = await open();
  try {
    await run(database.transaction(storeName, "readwrite").objectStore(storeName)
      .put({ ...response, ownerId: ownerOf(response), operationId: response.operationId ?? operationId(), key: keyOf(response) }));
  } finally {
    database.close();
  }
}

/**
 * Respuestas capturadas que todavía no llegaron a la API. La pantalla de captura las usa para marcar
 * cada pregunta como "pendiente de sincronizar" al retomar la evaluación y para saber cuáles siguen
 * en cola después de un intento de envío.
 */
export async function listPendingResponses(ownerId?: string, evaluationId?: number): Promise<OutboxResponse[]> {
  const database = await open();
  try {
    const entries = await run(database.transaction(storeName, "readonly").objectStore(storeName).getAll() as IDBRequest<OutboxResponse[]>);
    return entries.filter(item =>
      (ownerId === undefined || ownerOf(item) === ownerId) &&
      (evaluationId === undefined || item.evaluationId === evaluationId));
  } finally {
    database.close();
  }
}

/** Respuestas capturadas que todavía no llegaron a la API, para avisarlo en la interfaz. */
export async function countPendingResponses(ownerId?: string): Promise<number> {
  return (await listPendingResponses(ownerId)).length;
}

let inFlight: Promise<OutboxFlushResult> | null = null;

/**
 * Vacía la cola. La sincronización se dispara desde varios lados —volver a tener red, reabrir la
 * aplicación, un guardado manual—, así que solo se admite una en curso: la segunda espera a la
 * primera y no reenvía nada, en lugar de leer la cola antes de que la anterior borre lo ya aplicado.
 */
export function flushOutbox(send: (response: OutboxResponse) => Promise<void>): Promise<OutboxFlushResult>;
export function flushOutbox(ownerId: string, send: (response: OutboxResponse) => Promise<void>): Promise<OutboxFlushResult>;
export function flushOutbox(ownerOrSend: string | ((response: OutboxResponse) => Promise<void>), maybeSend?: (response: OutboxResponse) => Promise<void>): Promise<OutboxFlushResult> {
  const ownerId = typeof ownerOrSend === "string" ? ownerOrSend : undefined;
  const send = typeof ownerOrSend === "function" ? ownerOrSend : maybeSend!;
  if (inFlight) return inFlight.then(() => ({ sent: 0, failed: 0 }));
  inFlight = drain(send, ownerId).finally(() => { inFlight = null; });
  return inFlight;
}

async function drain(send: (response: OutboxResponse) => Promise<void>, ownerId?: string): Promise<OutboxFlushResult> {
  const database = await open();
  try {
    const stored = await run(database.transaction(storeName, "readonly").objectStore(storeName)
      .getAll() as IDBRequest<(OutboxResponse & { key: string })[]>);

    const eligible = ownerId ? stored.filter(item => ownerOf(item) === ownerId) : stored;
    let sent = 0;
    let failed = 0;
    for (let index = 0; index < eligible.length; index += 1) {
      const { key, ...response } = eligible[index];
      try {
        await send(response);
      } catch (error) {
        // Sin red o con la API caída la entrada se conserva tal cual: el siguiente intento la
        // reenvía. Descartarla aquí perdería una respuesta capturada en campo.
        failed += 1;
        if (typeof error === "object" && error !== null && "status" in error && error.status === 401) {
          failed += eligible.length - index - 1;
          break;
        }
        continue;
      }
      await run(database.transaction(storeName, "readwrite").objectStore(storeName).delete(key));
      sent += 1;
    }
    return { sent, failed };
  } finally {
    database.close();
  }
}

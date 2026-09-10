import { saveResponse } from "@/features/evaluations/evaluation-api";
import { enqueueResponse, flushOutbox, type OutboxFlushResult, type OutboxResponse } from "./outbox";

export type CaptureResult = "sent" | "queued";

/**
 * Guarda una respuesta de la evaluación en campo. Si la API responde, la captura queda firme; si el
 * envío falla —sin cobertura en el establecimiento o con la API caída—, la respuesta se conserva en
 * la cola local y se reintenta al sincronizar, de modo que el técnico nunca pierde lo capturado ni
 * necesita repetir el recorrido.
 */
export async function captureResponse(accessToken: string, response: OutboxResponse): Promise<CaptureResult> {
  try {
    await saveResponse(accessToken, response);
    return "sent";
  } catch {
    await enqueueResponse(response);
    return "queued";
  }
}

/** Reenvía lo pendiente. Repetir un envío ya aplicado es inocuo: la API guarda de forma idempotente. */
export function syncPendingResponses(accessToken: string): Promise<OutboxFlushResult> {
  return flushOutbox(response => saveResponse(accessToken, response));
}

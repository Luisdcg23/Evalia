import type { OutboxResponse } from "@/offline/outbox";

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");
const headers = (accessToken: string) => ({ Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" });

/**
 * Guarda la respuesta de una pregunta (autosave, RF-13). La API resuelve la escritura como alta o
 * actualización sobre el par evaluación/pregunta, de modo que repetir el envío de la misma captura
 * —lo que ocurre al sincronizar la cola tras recuperar la red— no duplica respuestas.
 */
export async function saveResponse(accessToken: string, response: OutboxResponse): Promise<void> {
  const result = await fetch(`${baseUrl}/api/evaluations/${response.evaluationId}/responses/${response.itemId}`, {
    method: "PUT",
    headers: headers(accessToken),
    body: JSON.stringify({
      optionCode: response.optionCode,
      observations: response.observations,
      comments: response.comments,
    }),
  });
  if (!result.ok) throw new Error("No fue posible guardar la respuesta.");
}

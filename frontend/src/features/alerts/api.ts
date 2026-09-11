export interface HealthAlert {
  id: number;
  alertNumber: string;
  receivedAt: string;
  product: string;
  companyId: number;
  description: string;
  status: string;
  decisionReason: string | null;
  decidedAt: string | null;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export async function listAlerts(accessToken: string): Promise<HealthAlert[]> {
  const response = await fetch(`${baseUrl}/api/alerts`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar las alertas LAPCH.");
  return response.json() as Promise<HealthAlert[]>;
}

export interface AlertInput {
  alertNumber: string;
  receivedAt: string;
  product: string;
  companyId: number;
  description: string;
}

/** Registra una alerta LAPCH nueva (`POST /api/alerts`, ver `AlertRequest` en `RegulatoryOriginEndpoints.cs`). */
export async function createAlert(accessToken: string, input: AlertInput): Promise<HealthAlert> {
  const response = await fetch(`${baseUrl}/api/alerts`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({
      alertNumber: input.alertNumber.trim(),
      receivedAt: input.receivedAt,
      product: input.product.trim(),
      companyId: input.companyId,
      description: input.description.trim(),
    }),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La empresa seleccionada no existe o está inactiva.");
    if (response.status === 400) throw new Error("Revisa los datos de la alerta: todos los campos son obligatorios.");
    throw new Error("No fue posible registrar la alerta.");
  }
  return response.json() as Promise<HealthAlert>;
}

/** Resultado de decisión que acepta el backend para una alerta (`DecisionRequest.Result`). */
export type AlertDecisionResult = "PROCEED" | "NOT_PROCEED";

/**
 * Decide una alerta (`POST /api/alerts/{id}/decision`). `PROCEED` genera el expediente asociado; el
 * `caseId` del expediente viene en la respuesta cuando aplica.
 */
export async function decideAlert(
  accessToken: string,
  id: number,
  input: { result: AlertDecisionResult; reason: string },
): Promise<{ status: string; caseId: number | null }> {
  const response = await fetch(`${baseUrl}/api/alerts/${id}/decision`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({ result: input.result, reason: input.reason.trim() }),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La alerta no existe.");
    if (response.status === 400) throw new Error("El resultado o el motivo de la decisión no son válidos.");
    throw new Error("No fue posible registrar la decisión de la alerta.");
  }
  return response.json() as Promise<{ status: string; caseId: number | null }>;
}

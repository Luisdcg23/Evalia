export interface Complaint {
  id: number;
  complaintType: string;
  receivedAt: string;
  complainant: string;
  description: string;
  companyId: number | null;
  status: string;
  decisionReason: string | null;
  decidedAt: string | null;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export async function listComplaints(accessToken: string): Promise<Complaint[]> {
  const response = await fetch(`${baseUrl}/api/complaints`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar las denuncias.");
  return response.json() as Promise<Complaint[]>;
}

export interface ComplaintInput {
  complaintType: string;
  receivedAt: string;
  complainant: string;
  description: string;
  companyId?: number | null;
}

/** Registra una denuncia nueva (`POST /api/complaints`, ver `ComplaintRequest` en `RegulatoryOriginEndpoints.cs`). */
export async function createComplaint(accessToken: string, input: ComplaintInput): Promise<Complaint> {
  const response = await fetch(`${baseUrl}/api/complaints`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({
      complaintType: input.complaintType.trim(),
      receivedAt: input.receivedAt,
      complainant: input.complainant.trim(),
      description: input.description.trim(),
      companyId: input.companyId ?? null,
    }),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La empresa seleccionada no existe o está inactiva.");
    if (response.status === 400) throw new Error("Revisa los datos de la denuncia: tipo, denunciante y descripción son obligatorios.");
    throw new Error("No fue posible registrar la denuncia.");
  }
  return response.json() as Promise<Complaint>;
}

/** Resultado de decisión que acepta el backend para una denuncia (`DecisionRequest.Result`). */
export type ComplaintDecisionResult = "PROCEED" | "NOT_PROCEED" | "REFERRED";

/**
 * Decide una denuncia (`POST /api/complaints/{id}/decision`). `PROCEED` exige que la denuncia ya
 * tenga una empresa asociada (el backend responde 409 si no) y genera el expediente correspondiente.
 */
export async function decideComplaint(
  accessToken: string,
  id: number,
  input: { result: ComplaintDecisionResult; reason: string },
): Promise<{ status: string; caseId: number | null }> {
  const response = await fetch(`${baseUrl}/api/complaints/${id}/decision`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({ result: input.result, reason: input.reason.trim() }),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La denuncia no existe.");
    if (response.status === 409) throw new Error("Debe asociar una empresa antes de generar la evaluación.");
    if (response.status === 400) throw new Error("El resultado o el motivo de la decisión no son válidos.");
    throw new Error("No fue posible registrar la decisión de la denuncia.");
  }
  return response.json() as Promise<{ status: string; caseId: number | null }>;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");
const options = (accessToken: string): RequestInit => ({ headers: { Authorization: `Bearer ${accessToken}` } });

export interface InspectionCase {
  id: number; companyId: number; sourceType: string; sourceReferenceId: number;
  status: string; createdAt: string;
}

export interface ScheduleEntry {
  caseId: number; companyId: number; companyName: string; address: string;
  scheduledFor: string; caseStatus: string; priority: string; technicianId: string;
}

async function parse<T>(response: Response, message: string): Promise<T> {
  if (!response.ok) throw new Error(response.status === 401 ? "Tu sesión expiró. Inicia sesión nuevamente." : message);
  return response.json() as Promise<T>;
}

export async function listCases(accessToken: string): Promise<InspectionCase[]> {
  return parse(await fetch(`${baseUrl}/api/cases`, options(accessToken)), "No fue posible consultar los expedientes.");
}

export async function listSchedule(accessToken: string, from: string, to: string): Promise<ScheduleEntry[]> {
  const query = new URLSearchParams({ from, to });
  return parse(await fetch(`${baseUrl}/api/cases/schedule?${query}`, options(accessToken)), "No fue posible consultar la agenda.");
}

export interface CaseAssignment {
  id: number; caseId: number; technicianId: string; reason: string;
  assignedAt: string; assignedBy: string; isCurrent: boolean;
}

/**
 * Asigna (o reasigna) el técnico evaluador de un expediente vía `POST /api/cases/{id}/assign`
 * (RF-10, solo coordinador). El backend valida el técnico y la transición de estado.
 */
export async function assignTechnician(
  accessToken: string,
  caseId: number,
  input: { technicianId: string; reason: string },
): Promise<CaseAssignment> {
  const response = await fetch(`${baseUrl}/api/cases/${caseId}/assign`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({ technicianId: input.technicianId, reason: input.reason.trim() }),
  });
  if (!response.ok) {
    if (response.status === 400) {
      throw new Error("El técnico seleccionado no es válido o no está activo.");
    }
    if (response.status === 409) {
      throw new Error("El expediente ya no admite asignación en su estado actual.");
    }
    if (response.status === 401) {
      throw new Error("Tu sesión expiró. Inicia sesión nuevamente.");
    }
    if (response.status === 403) {
      throw new Error("Solo el coordinador puede asignar técnicos.");
    }
    throw new Error("No fue posible confirmar la asignación.");
  }
  return response.json() as Promise<CaseAssignment>;
}

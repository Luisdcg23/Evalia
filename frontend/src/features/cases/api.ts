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

export interface InstitutionalCaseInput {
  companyId: number;
  reason: string;
  observations?: string;
}

/**
 * Programación institucional (RF-06, escenario 2): abre un expediente directamente, sin alerta ni
 * denuncia de por medio (`POST /api/cases/institutional`, ver `InstitutionalCaseRequest` en
 * `CaseEndpoints.cs`).
 */
export async function createInstitutionalCase(
  accessToken: string,
  input: InstitutionalCaseInput,
): Promise<InspectionCase> {
  const response = await fetch(`${baseUrl}/api/cases/institutional`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({
      companyId: input.companyId,
      reason: input.reason.trim(),
      observations: input.observations?.trim() || undefined,
    }),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La empresa seleccionada no existe o está inactiva.");
    if (response.status === 400) throw new Error("El motivo admite 1-1000 caracteres y las observaciones hasta 2000.");
    if (response.status === 401) throw new Error("Tu sesión expiró. Inicia sesión nuevamente.");
    throw new Error("No fue posible programar el caso institucional.");
  }
  return response.json() as Promise<InspectionCase>;
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

export interface CaseSchedule {
  id: number; caseId: number; technicianId: string;
  scheduledFor: string; priority: string; reason: string; observations: string;
  isCurrent: boolean; scheduledBy: string; createdAt: string;
  cancelledAt: string | null; cancelledBy: string | null; cancellationReason: string | null;
}

export interface ScheduleCaseInput {
  scheduledFor: string;
  reason: string;
  priority?: string;
  observations?: string;
}

function scheduleBody(input: ScheduleCaseInput) {
  return {
    scheduledFor: input.scheduledFor,
    reason: input.reason.trim(),
    priority: input.priority?.trim() || undefined,
    observations: input.observations?.trim() || undefined,
  };
}

function scheduleErrorMessage(response: Response, fallback: string): string {
  if (response.status === 401) return "Tu sesión expiró. Inicia sesión nuevamente.";
  if (response.status === 403) return "Solo el coordinador puede programar evaluaciones.";
  if (response.status === 404) return "El expediente no existe.";
  if (response.status === 400) return "El caso debe tener un técnico asignado y vigente antes de programarse.";
  if (response.status === 409) return "El técnico ya tiene otra programación vigente que se solapa con ese horario, o el expediente no admite esta operación en su estado actual.";
  return fallback;
}

/** Programa por primera vez la evaluación de un caso ya asignado (RF-07, `POST /api/cases/{id}/schedule`). */
export async function scheduleCase(accessToken: string, caseId: number, input: ScheduleCaseInput): Promise<CaseSchedule> {
  const response = await fetch(`${baseUrl}/api/cases/${caseId}/schedule`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify(scheduleBody(input)),
  });
  if (!response.ok) throw new Error(scheduleErrorMessage(response, "No fue posible programar la evaluación."));
  return response.json() as Promise<CaseSchedule>;
}

/** Reprograma la programación vigente de un caso (RF-07, `POST /api/cases/{id}/reschedule`). */
export async function rescheduleCase(accessToken: string, caseId: number, input: ScheduleCaseInput): Promise<CaseSchedule> {
  const response = await fetch(`${baseUrl}/api/cases/${caseId}/reschedule`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify(scheduleBody(input)),
  });
  if (!response.ok) throw new Error(scheduleErrorMessage(response, "No fue posible reprogramar la evaluación."));
  return response.json() as Promise<CaseSchedule>;
}

/** Cancela la programación vigente de un caso, sin cancelar el expediente (`POST /api/cases/{id}/cancel-schedule`). */
export async function cancelSchedule(accessToken: string, caseId: number, input: { reason: string }): Promise<CaseSchedule> {
  const response = await fetch(`${baseUrl}/api/cases/${caseId}/cancel-schedule`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({ reason: input.reason.trim() }),
  });
  if (!response.ok) {
    if (response.status === 409) throw new Error("El caso no tiene una programación vigente para cancelar.");
    throw new Error(scheduleErrorMessage(response, "No fue posible cancelar la programación."));
  }
  return response.json() as Promise<CaseSchedule>;
}

/** Historial de programaciones de un caso, de la más antigua a la más reciente (`GET /api/cases/{id}/schedules`). */
export async function listCaseSchedules(accessToken: string, caseId: number): Promise<CaseSchedule[]> {
  return parse(await fetch(`${baseUrl}/api/cases/${caseId}/schedules`, options(accessToken)), "No fue posible consultar la programación del expediente.");
}

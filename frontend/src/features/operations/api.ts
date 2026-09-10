const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");
const options = (accessToken: string): RequestInit => ({ headers: { Authorization: `Bearer ${accessToken}` } });

async function parse<T>(response: Response, message: string): Promise<T> {
  if (!response.ok) {
    throw new Error(response.status === 401
      ? "Tu sesión expiró. Inicia sesión nuevamente."
      : response.status === 403 ? "Tu rol no tiene acceso a esta información." : message);
  }
  return response.json() as Promise<T>;
}

/** Métricas del panel según el rol del usuario autenticado (`GET /api/dashboard`). */
export interface DashboardMetrics {
  totalCases: number;
  byStatus: { status: string; count: number }[];
  unreadNotifications: number;
}

export async function getDashboard(accessToken: string): Promise<DashboardMetrics> {
  return parse(await fetch(`${baseUrl}/api/dashboard`, options(accessToken)),
    "No fue posible cargar el resumen del panel.");
}

/** Caso del técnico/empresa autenticado con su instancia de evaluación y resultado (`GET /api/me/cases`). */
export interface MyCaseRow {
  id: number;
  companyId: number;
  companyName: string;
  sourceType: string;
  priority: string;
  status: string;
  createdAt: string;
  evaluationInstanceId: number | null;
  bpmPercentage: number | null;
  classification: string | null;
  frequencyMonths: number | null;
}

export async function listMyCases(accessToken: string): Promise<MyCaseRow[]> {
  return parse(await fetch(`${baseUrl}/api/me/cases`, options(accessToken)),
    "No fue posible consultar tus expedientes.");
}

/** Entrada de agenda del técnico autenticado (`GET /api/me/schedule`). */
export interface MyScheduleRow {
  caseId: number;
  companyId: number;
  companyName: string;
  companyAddress: string | null;
  scheduledFor: string;
  status: string;
  priority: string;
  observations: string | null;
}

export async function listMySchedule(
  accessToken: string,
  from?: string,
  to?: string,
): Promise<MyScheduleRow[]> {
  const query = new URLSearchParams();
  if (from) query.set("from", from);
  if (to) query.set("to", to);
  const suffix = query.toString() ? `?${query}` : "";
  return parse(await fetch(`${baseUrl}/api/me/schedule${suffix}`, options(accessToken)),
    "No fue posible consultar tu agenda.");
}

/** Técnico evaluador aprobado con su carga vigente (`GET /api/technicians`). */
export interface TechnicianRow {
  id: string;
  fullName: string;
  email: string;
  activeCaseCount: number;
}

export async function listTechnicians(accessToken: string): Promise<TechnicianRow[]> {
  return parse(await fetch(`${baseUrl}/api/technicians`, options(accessToken)),
    "No fue posible consultar los técnicos evaluadores.");
}

/** Filtros de la consulta histórica de expedientes (RF-20, `GET /api/cases/history/search`). */
export interface CaseHistoryFilters {
  companyId?: number;
  technicianId?: string;
  status?: string;
  sourceType?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

/** Metadatos del informe oficial emitido para el expediente (RF-19), si existe. */
export interface CaseHistoryOfficialReport {
  generatedAt: string;
  sha256: string;
  fileName: string;
  sizeBytes: number;
}

/** Calificación registrada de la evaluación del expediente (RF-14), si existe. */
export interface CaseHistoryEvaluation {
  bpmPercentage: number;
  qualificationCode: string;
  classification: string;
  bpmRiskScore: number;
  riskLevel: string;
  frequencyMonths: number;
}

export interface CaseHistoryEntry {
  id: number;
  caseId: number;
  companyId: number;
  sourceType: string;
  previousStatus: string | null;
  newStatus: string;
  reason: string | null;
  changedAt: string;
  changedBy: string | null;
  hasOfficialReport: boolean;
  officialReport: CaseHistoryOfficialReport | null;
  evaluation: CaseHistoryEvaluation | null;
}

export interface CaseHistoryPage {
  page: number;
  pageSize: number;
  total: number;
  items: CaseHistoryEntry[];
}

export async function searchCaseHistory(
  accessToken: string,
  filters: CaseHistoryFilters = {},
): Promise<CaseHistoryPage> {
  const query = new URLSearchParams();
  if (filters.companyId != null) query.set("companyId", String(filters.companyId));
  if (filters.technicianId) query.set("technicianId", filters.technicianId);
  if (filters.status) query.set("status", filters.status);
  if (filters.sourceType) query.set("sourceType", filters.sourceType);
  if (filters.from) query.set("from", filters.from);
  if (filters.to) query.set("to", filters.to);
  if (filters.page != null) query.set("page", String(filters.page));
  if (filters.pageSize != null) query.set("pageSize", String(filters.pageSize));
  const suffix = query.toString() ? `?${query}` : "";
  return parse(await fetch(`${baseUrl}/api/cases/history/search${suffix}`, options(accessToken)),
    "No fue posible ejecutar la consulta histórica.");
}

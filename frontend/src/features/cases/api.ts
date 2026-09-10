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

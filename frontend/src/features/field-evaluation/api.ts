/**
 * Cliente de ejecución de la evaluación BPM en campo (RF-12 a RF-16, RF-18):
 * iniciar/retomar la instancia, autosave de respuestas, envío, resultado calculado,
 * evidencias y el informe versionado. Ver `EvaluationInstanceEndpoints.cs`,
 * `EvaluationEvidenceEndpoints.cs` y `EvaluationReportEndpoints.cs` en el backend.
 *
 * El árbol de preguntas se lee de `GET /api/evaluation-templates/{id}/items`, ya accesible
 * para el Técnico Evaluador tras separar las rutas de lectura de las de escritura en
 * `EvaluationTemplateEndpoints.cs`; se reexporta desde `features/templates/api.ts` para no
 * duplicar el contrato.
 */
export { listTemplateItems } from "../templates/api";
export type { EvaluationTemplateItem } from "../templates/api";

export const BPM_OPTION_CODES = ["C", "CP", "IT", "NA"] as const;
export type BpmOptionCode = (typeof BPM_OPTION_CODES)[number];
export const BPM_OPTION_LABEL: Record<BpmOptionCode, string> = {
  C: "Cumple",
  CP: "Cumplimiento parcial",
  IT: "Incumplimiento total",
  NA: "No aplica",
};

export interface EvaluationInstance {
  id: number;
  caseId: number;
  templateId: number;
  templateFamilyId: string;
  riskRuleVersionId: number;
  status: "IN_PROGRESS" | "SUBMITTED";
  startedAt: string;
  startedBy: string;
  submittedAt: string | null;
  submittedBy: string | null;
}

export interface EvaluationProgress {
  id: number;
  caseId: number;
  templateId: number;
  status: "IN_PROGRESS" | "SUBMITTED";
  startedAt: string;
  startedBy: string;
  submittedAt: string | null;
  submittedBy: string | null;
  totalQuestions: number;
  answeredQuestions: number;
  progressPercentage: number | null;
}

export interface EvaluationResponse {
  id: number;
  evaluationInstanceId: number;
  templateItemId: number;
  optionCode: string;
  observations: string;
  comments: string;
  savedAt: string;
  savedBy: string;
}

export interface EvaluationNonConformity {
  id: number;
  evaluationResponseId: number;
  guidanceCriterionId: number;
  itemCode: string;
  criterionCode: string;
  severity: string;
}

export interface EvaluationResult {
  evaluationResultId: number;
  evaluationInstanceId: number;
  caseId: number;
  bpmPoints: number;
  bpmDenominator: number;
  bpmPercentage: number;
  qualificationCode: string;
  classification: string;
  bpmRiskScore: number;
  criticalCount: number;
  majorCount: number;
  minorCount: number;
  riskCalculationId: number;
  productRisk: number;
  establishmentRisk: number;
  totalRisk: number;
  riskLevel: string;
  frequencyMonths: number;
  calculatedAt: string;
  nonConformities: EvaluationNonConformity[];
}

export interface EvaluationEvidence {
  id: number;
  evaluationInstanceId: number;
  evaluationResponseId: number | null;
  templateItemId: number | null;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
  hash: string;
  storageKey: string;
  description: string | null;
  uploadedAt: string;
  uploadedBy: string;
}

export interface EvaluationReport {
  id: number;
  evaluationInstanceId: number;
  version: number;
  status: string;
  executiveSummary: string;
  findings: string;
  recommendations: string;
  createdAt: string;
  createdBy: string;
}

export interface EvaluationReportReview {
  id: number;
  reportId: number;
  reportVersion: number;
  decision: "APPROVED" | "RETURNED" | "CORRECTION_REQUESTED" | string;
  observations: string;
  reviewedAt: string;
  reviewedBy: string;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

const authHeaders = (accessToken: string) => ({ Authorization: `Bearer ${accessToken}` });
const jsonHeaders = (accessToken: string) => ({ ...authHeaders(accessToken), "Content-Type": "application/json" });

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  try {
    const body: unknown = await response.json();
    if (body && typeof body === "object") {
      const message = (body as { message?: unknown }).message;
      if (typeof message === "string" && message.trim()) return message;
      const errors = (body as { errors?: Record<string, unknown> }).errors;
      if (errors && typeof errors === "object") {
        const first = Object.values(errors).flat().find(value => typeof value === "string" && value.trim());
        if (typeof first === "string") return first;
      }
    }
  } catch {
    // el cuerpo no era JSON (o venía vacío); nos quedamos con el mensaje por defecto.
  }
  return fallback;
}

/** Inicia la evaluación de un caso (RF-12). Si ya existe una instancia, el backend la devuelve tal cual. */
export async function startEvaluation(accessToken: string, caseId: number): Promise<EvaluationInstance> {
  const response = await fetch(`${baseUrl}/api/cases/${caseId}/evaluations`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    if (response.status === 403) throw new Error("Solo el técnico asignado vigente puede iniciar esta evaluación.");
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "El expediente no admite iniciar una evaluación en su estado actual."));
    }
    if (response.status === 400) {
      throw new Error(await readErrorMessage(response, "El caso debe tener un técnico asignado antes de iniciar la evaluación."));
    }
    if (response.status === 404) throw new Error("El expediente no existe.");
    throw new Error(await readErrorMessage(response, "No fue posible iniciar la evaluación."));
  }
  return response.json() as Promise<EvaluationInstance>;
}

/** Progreso de captura (RF-13): total de preguntas, respondidas y porcentaje. */
export async function getEvaluation(accessToken: string, evaluationId: number): Promise<EvaluationProgress> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}`, { headers: authHeaders(accessToken) });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La evaluación no existe.");
    if (response.status === 403) throw new Error("No tienes la asignación vigente de este expediente.");
    throw new Error(await readErrorMessage(response, "No fue posible consultar el progreso de la evaluación."));
  }
  return response.json() as Promise<EvaluationProgress>;
}

export interface SaveResponseInput {
  optionCode: string;
  observations?: string;
  comments?: string;
}

/** Autosave de una respuesta (RF-13). Idempotente: repetir la misma pregunta actualiza en vez de duplicar. */
export async function saveEvaluationResponse(
  accessToken: string,
  evaluationId: number,
  itemId: number,
  input: SaveResponseInput,
): Promise<EvaluationResponse> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/responses/${itemId}`, {
    method: "PUT",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify({
      optionCode: input.optionCode,
      observations: input.observations ?? "",
      comments: input.comments ?? "",
    }),
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "La evaluación ya fue enviada y sus respuestas están bloqueadas."));
    }
    if (response.status === 403) throw new Error("No tienes la asignación vigente de este expediente.");
    if (response.status === 400) throw new Error(await readErrorMessage(response, "La respuesta no es válida."));
    throw new Error(await readErrorMessage(response, "No fue posible guardar la respuesta."));
  }
  return response.json() as Promise<EvaluationResponse>;
}

/** Envía (bloquea) la evaluación y calcula el resultado (irreversible). */
export async function submitEvaluation(accessToken: string, evaluationId: number): Promise<EvaluationInstance> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/submit`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "La evaluación ya fue enviada o no admite envío en el estado actual del caso."));
    }
    if (response.status === 403) throw new Error("No tienes la asignación vigente de este expediente.");
    throw new Error(await readErrorMessage(response, "No fue posible enviar la evaluación."));
  }
  return response.json() as Promise<EvaluationInstance>;
}

/** Resultado calculado (RF-14): BPM, calificación, riesgo y no conformidades. */
export async function getEvaluationResult(accessToken: string, evaluationId: number): Promise<EvaluationResult> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/result`, { headers: authHeaders(accessToken) });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La evaluación todavía no tiene un resultado calculado.");
    if (response.status === 403) throw new Error("No tienes acceso al resultado de este expediente.");
    throw new Error(await readErrorMessage(response, "No fue posible consultar el resultado."));
  }
  return response.json() as Promise<EvaluationResult>;
}

export interface UploadEvidenceInput {
  file: File;
  templateItemId?: number;
  description?: string;
}

/** Sube una evidencia (RF-15, `multipart/form-data`). */
export async function uploadEvidence(
  accessToken: string,
  evaluationId: number,
  input: UploadEvidenceInput,
): Promise<EvaluationEvidence> {
  const form = new FormData();
  form.append("file", input.file);
  if (input.templateItemId != null) form.append("templateItemId", String(input.templateItemId));
  if (input.description?.trim()) form.append("description", input.description.trim());

  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/evidence`, {
    method: "POST",
    // Sin Content-Type explícito: el navegador arma el boundary multipart correcto.
    headers: authHeaders(accessToken),
    body: form,
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "La evaluación ya fue enviada y no admite nuevas evidencias."));
    }
    if (response.status === 400) {
      throw new Error(await readErrorMessage(response, "El archivo no cumple los requisitos de evidencia (tipo permitido: JPEG/PNG/WEBP/PDF, máx. 15 MB)."));
    }
    if (response.status === 403) throw new Error("No tienes la asignación vigente de este expediente.");
    throw new Error(await readErrorMessage(response, "No fue posible subir la evidencia."));
  }
  return response.json() as Promise<EvaluationEvidence>;
}

export async function listEvidence(accessToken: string, evaluationId: number): Promise<EvaluationEvidence[]> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/evidence`, { headers: authHeaders(accessToken) });
  if (!response.ok) throw new Error(await readErrorMessage(response, "No fue posible consultar las evidencias."));
  return response.json() as Promise<EvaluationEvidence[]>;
}

/** Descarga el binario de una evidencia (requiere el header Authorization, no es un link directo). */
export async function downloadEvidence(accessToken: string, evaluationId: number, evidenceId: number): Promise<Blob> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/evidence/${evidenceId}/content`, {
    headers: authHeaders(accessToken),
  });
  if (!response.ok) throw new Error(response.status === 404 ? "La evidencia no existe." : "No fue posible descargar la evidencia.");
  return response.blob();
}

export interface IssueReportInput {
  executiveSummary: string;
  findings: string;
  recommendations: string;
}

/** Emite una versión nueva del informe (RF-16); si el caso ya está en revisión, no cambia de estado. */
export async function issueReport(
  accessToken: string,
  evaluationId: number,
  input: IssueReportInput,
): Promise<EvaluationReport> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/report`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify({
      executiveSummary: input.executiveSummary.trim(),
      findings: input.findings.trim(),
      recommendations: input.recommendations.trim(),
    }),
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "El expediente no admite una nueva versión del informe en su estado actual."));
    }
    if (response.status === 403) throw new Error("Solo el técnico asignado vigente puede emitir el informe.");
    if (response.status === 404) throw new Error("La evaluación no existe.");
    throw new Error(await readErrorMessage(response, "No fue posible emitir el informe."));
  }
  return response.json() as Promise<EvaluationReport>;
}

/** Versión vigente del informe (la más reciente), o `null` si aún no se ha emitido ninguna. */
export async function getCurrentReport(accessToken: string, evaluationId: number): Promise<EvaluationReport | null> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/report`, { headers: authHeaders(accessToken) });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(await readErrorMessage(response, "No fue posible consultar el informe."));
  return response.json() as Promise<EvaluationReport>;
}

export async function listReportVersions(accessToken: string, evaluationId: number): Promise<EvaluationReport[]> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/report/versions`, { headers: authHeaders(accessToken) });
  if (!response.ok) throw new Error(await readErrorMessage(response, "No fue posible consultar las versiones del informe."));
  return response.json() as Promise<EvaluationReport[]>;
}

/** Observaciones de revisión (RF-17/RF-18), de la más antigua a la más reciente. */
export async function listReportReviews(accessToken: string, evaluationId: number): Promise<EvaluationReportReview[]> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/report/reviews`, { headers: authHeaders(accessToken) });
  if (!response.ok) throw new Error(await readErrorMessage(response, "No fue posible consultar las revisiones del informe."));
  return response.json() as Promise<EvaluationReportReview[]>;
}

export const REPORT_REVIEW_DECISIONS = ["APPROVED", "RETURNED", "CORRECTION_REQUESTED"] as const;
export type ReportReviewDecision = (typeof REPORT_REVIEW_DECISIONS)[number];

/**
 * Revisión del coordinador sobre la versión vigente del informe (RF-17/RF-18). Devolver o pedir
 * corrección exige observaciones no vacías; el backend rechaza con 400 si faltan.
 */
export async function reviewReport(
  accessToken: string,
  evaluationId: number,
  input: { decision: ReportReviewDecision; observations?: string },
): Promise<EvaluationReportReview> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/report/review`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify({ decision: input.decision, observations: input.observations?.trim() ?? "" }),
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "El expediente no está en revisión."));
    }
    if (response.status === 400) {
      throw new Error(await readErrorMessage(response, "Devolver el informe exige observaciones que indiquen qué corregir."));
    }
    if (response.status === 404) throw new Error("El informe no existe.");
    throw new Error(await readErrorMessage(response, "No fue posible registrar la revisión del informe."));
  }
  return response.json() as Promise<EvaluationReportReview>;
}

export interface OfficialReport {
  id: number;
  evaluationInstanceId: number;
  reportId: number;
  reportVersion: number;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
  sha256: string;
  generatedAt: string;
  generatedBy: string;
}

/**
 * Genera el PDF oficial (RF-19); exige que la última versión del informe esté aprobada. Es
 * idempotente: si ya existe, el backend devuelve el mismo metadato sin regenerar el archivo.
 */
export async function generateOfficialReport(accessToken: string, evaluationId: number): Promise<OfficialReport> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/report/official`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "La última versión del informe debe estar aprobada antes de generar el PDF oficial."));
    }
    if (response.status === 404) throw new Error("El informe no existe.");
    throw new Error(await readErrorMessage(response, "No fue posible generar el PDF oficial."));
  }
  return response.json() as Promise<OfficialReport>;
}

/**
 * Descarga el PDF oficial (RF-19). El endpoint exige el header Authorization, así que no es un link
 * directo de navegador: se trae el blob por fetch y el nombre de archivo sugerido para guardarlo.
 */
export async function downloadOfficialReport(
  accessToken: string,
  evaluationId: number,
): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetch(`${baseUrl}/api/evaluations/${evaluationId}/report/official/content`, {
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    throw new Error(response.status === 404 ? "El PDF oficial todavía no ha sido generado." : "No fue posible descargar el PDF oficial.");
  }
  const disposition = response.headers.get("content-disposition") ?? "";
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const fileName = match?.[1] ? decodeURIComponent(match[1]) : `informe-evaluacion-${evaluationId}.pdf`;
  return { blob: await response.blob(), fileName };
}

export interface CaseClosure {
  id: number;
  caseId: number;
  reportId: number;
  officialReportId: number;
  status: string;
  result: string;
  closedAt: string;
  closedBy: string;
}

/**
 * Cierra el expediente (RF-19, irreversible). El backend exige informe aprobado y PDF oficial
 * emitido; es idempotente si ya está cerrado (devuelve el cierre existente).
 */
export async function closeCase(accessToken: string, caseId: number, input: { result: string }): Promise<CaseClosure> {
  const response = await fetch(`${baseUrl}/api/cases/${caseId}/close`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify({ result: input.result.trim() }),
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "El expediente exige su última versión aprobada y el PDF oficial antes del cierre."));
    }
    if (response.status === 400) throw new Error("El resultado del cierre es obligatorio.");
    if (response.status === 404) throw new Error("El expediente no existe.");
    throw new Error(await readErrorMessage(response, "No fue posible cerrar el expediente."));
  }
  return response.json() as Promise<CaseClosure>;
}

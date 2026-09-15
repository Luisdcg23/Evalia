/**
 * Cliente de `GET/POST /api/evaluation-templates` y sus sub-rutas (rol Administrador).
 * Ver `backend/src/EBR.Api/Endpoints/EvaluationTemplateEndpoints.cs`.
 */
export interface EvaluationTemplate {
  id: number;
  familyId: string;
  name: string;
  version: number;
  status: string;
  createdAt: string;
  publishedAt?: string | null;
}

/** Respuesta de `GET/POST .../active` y `.../activate`: cuál plantilla usa hoy `StartAsync`. */
export interface ActiveEvaluationTemplate {
  templateId: number | null;
  activatedAt: string | null;
}

export interface EvaluationTemplateItem {
  id: number;
  templateId: number;
  parentId: number | null;
  code: string;
  description: string;
  itemType: string;
  order: number;
  weight: number | null;
  isRequired: boolean;
  isCritical: boolean;
  allowsNotApplicable: boolean;
  responseType: string | null;
  rulesJson: string;
  scoreConfigurationJson: string;
  isActive: boolean;
  versionToken: string;
}

/** Cuerpo de `POST/PUT .../items`: mismo contrato que `UpsertItemRequest` en el backend. */
export interface UpsertTemplateItemInput {
  code: string;
  description: string;
  itemType: string;
  order: number;
  parentId?: number | null;
  weight?: number | null;
  isRequired?: boolean;
  isCritical?: boolean;
  allowsNotApplicable?: boolean;
  responseType?: string | null;
  rulesJson?: string | null;
  scoreConfigurationJson?: string | null;
  versionToken?: string | null;
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

function buildItemPayload(input: UpsertTemplateItemInput) {
  return {
    code: input.code.trim(),
    description: input.description.trim(),
    itemType: input.itemType.trim().toUpperCase(),
    order: input.order,
    parentId: input.parentId ?? null,
    weight: input.weight ?? null,
    isRequired: input.isRequired ?? false,
    isCritical: input.isCritical ?? false,
    allowsNotApplicable: input.allowsNotApplicable ?? true,
    responseType: input.responseType?.trim() ? input.responseType.trim().toUpperCase() : null,
    rulesJson: input.rulesJson?.trim() ? input.rulesJson : undefined,
    scoreConfigurationJson: input.scoreConfigurationJson?.trim() ? input.scoreConfigurationJson : undefined,
    versionToken: input.versionToken ?? undefined,
  };
}

export async function listTemplates(accessToken: string): Promise<EvaluationTemplate[]> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates`, { headers: authHeaders(accessToken) });
  if (!response.ok) throw new Error(await readErrorMessage(response, "No fue posible consultar las plantillas."));
  return response.json() as Promise<EvaluationTemplate[]>;
}

export async function createTemplate(accessToken: string, name: string): Promise<EvaluationTemplate> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify({ name: name.trim() }),
  });
  if (!response.ok) {
    throw new Error(response.status === 400
      ? await readErrorMessage(response, "El nombre de la plantilla es obligatorio.")
      : await readErrorMessage(response, "No fue posible crear la plantilla."));
  }
  return response.json() as Promise<EvaluationTemplate>;
}

export async function listTemplateItems(accessToken: string, templateId: number): Promise<EvaluationTemplateItem[]> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/items`, { headers: authHeaders(accessToken) });
  if (!response.ok) {
    throw new Error(response.status === 404
      ? "La plantilla no existe."
      : await readErrorMessage(response, "No fue posible consultar la estructura de la plantilla."));
  }
  return response.json() as Promise<EvaluationTemplateItem[]>;
}

export async function createTemplateItem(
  accessToken: string, templateId: number, input: UpsertTemplateItemInput,
): Promise<EvaluationTemplateItem> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/items`, {
    method: "POST",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify(buildItemPayload(input)),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La plantilla no existe.");
    if (response.status === 409) {
      throw new Error(await readErrorMessage(
        response, "No fue posible crear el ítem: la plantilla ya no está en borrador o el código ya existe.",
      ));
    }
    throw new Error(await readErrorMessage(response, "No fue posible crear el ítem."));
  }
  return response.json() as Promise<EvaluationTemplateItem>;
}

export async function updateTemplateItem(
  accessToken: string, templateId: number, itemId: number, input: UpsertTemplateItemInput,
): Promise<EvaluationTemplateItem> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/items/${itemId}`, {
    method: "PUT",
    headers: jsonHeaders(accessToken),
    body: JSON.stringify(buildItemPayload(input)),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("El ítem no existe.");
    if (response.status === 409) {
      throw new Error(await readErrorMessage(
        response, "No fue posible guardar el ítem: la plantilla ya no está en borrador o fue modificado por otra persona.",
      ));
    }
    throw new Error(await readErrorMessage(response, "No fue posible guardar el ítem."));
  }
  return response.json() as Promise<EvaluationTemplateItem>;
}

export async function deleteTemplateItem(accessToken: string, templateId: number, itemId: number): Promise<void> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/items/${itemId}`, {
    method: "DELETE",
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("El ítem no existe.");
    if (response.status === 409) {
      throw new Error(await readErrorMessage(
        response, "No fue posible borrar el ítem: la plantilla ya no está en borrador o tiene ítems dependientes.",
      ));
    }
    throw new Error(await readErrorMessage(response, "No fue posible borrar el ítem."));
  }
}

export async function publishTemplate(accessToken: string, templateId: number): Promise<EvaluationTemplate> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/publish`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La plantilla no existe.");
    throw new Error(await readErrorMessage(response, "No fue posible publicar la plantilla."));
  }
  return response.json() as Promise<EvaluationTemplate>;
}

export async function getActiveTemplate(accessToken: string): Promise<ActiveEvaluationTemplate> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/active`, { headers: authHeaders(accessToken) });
  if (!response.ok) throw new Error(await readErrorMessage(response, "No fue posible consultar la plantilla activa."));
  return response.json() as Promise<ActiveEvaluationTemplate>;
}

export async function activateTemplate(accessToken: string, templateId: number): Promise<ActiveEvaluationTemplate> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/activate`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La plantilla no existe.");
    if (response.status === 409) {
      throw new Error(await readErrorMessage(
        response, "No fue posible activar la plantilla: debe estar publicada y cumplir el contrato de bandas de calificación.",
      ));
    }
    throw new Error(await readErrorMessage(response, "No fue posible activar la plantilla."));
  }
  return response.json() as Promise<ActiveEvaluationTemplate>;
}

export async function createTemplateVersion(accessToken: string, templateId: number): Promise<EvaluationTemplate> {
  const response = await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/versions`, {
    method: "POST",
    headers: authHeaders(accessToken),
  });
  if (!response.ok) {
    if (response.status === 404) throw new Error("La plantilla no existe.");
    if (response.status === 409) {
      throw new Error(await readErrorMessage(response, "Solo una versión publicada puede clonarse."));
    }
    throw new Error(await readErrorMessage(response, "No fue posible crear la nueva versión."));
  }
  return response.json() as Promise<EvaluationTemplate>;
}

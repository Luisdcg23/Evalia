export interface EvaluationTemplate {
  id: number;
  familyId: string;
  name: string;
  version: number;
  status: "DRAFT" | "PUBLISHED" | "RETIRED";
}

export interface EvaluationTemplateItem {
  id: number;
  templateId: number;
  parentId: number | null;
  code: string;
  description: string;
  itemType: "CHAPTER" | "SECTION" | "SUBSECTION" | "QUESTION" | "INSTRUCTION" | "OPTION";
  order: number;
  weight: number | null;
  isRequired: boolean;
  isCritical: boolean;
  allowsNotApplicable: boolean;
  responseType: string | null;
  versionToken: string;
}

export type EvaluationTemplateItemInput = Omit<EvaluationTemplateItem, "id" | "templateId" | "versionToken">;

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");
const headers = (accessToken: string) => ({ Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" });

async function parse<T>(response: Response, message: string): Promise<T> {
  if (!response.ok) throw new Error(message);
  return response.json() as Promise<T>;
}

export async function listTemplates(accessToken: string): Promise<EvaluationTemplate[]> {
  return parse(await fetch(`${baseUrl}/api/evaluation-templates`, { headers: headers(accessToken) }), "No fue posible consultar las plantillas.");
}

export async function createTemplate(accessToken: string, name: string): Promise<EvaluationTemplate> {
  return parse(await fetch(`${baseUrl}/api/evaluation-templates`, {
    method: "POST", headers: headers(accessToken), body: JSON.stringify({ name: name.trim() }),
  }), "No fue posible crear la plantilla.");
}

export async function listTemplateItems(accessToken: string, templateId: number): Promise<EvaluationTemplateItem[]> {
  return parse(await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/items`, { headers: headers(accessToken) }), "No fue posible consultar la estructura.");
}

export async function createTemplateItem(accessToken: string, templateId: number, input: EvaluationTemplateItemInput): Promise<EvaluationTemplateItem> {
  return parse(await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/items`, {
    method: "POST", headers: headers(accessToken), body: JSON.stringify({ ...input, description: input.description.trim(), code: input.code.trim() }),
  }), "No fue posible crear el ítem.");
}

export async function publishTemplate(accessToken: string, templateId: number): Promise<EvaluationTemplate> {
  return parse(await fetch(`${baseUrl}/api/evaluation-templates/${templateId}/publish`, {
    method: "POST", headers: headers(accessToken), body: "{}",
  }), "No fue posible publicar la plantilla.");
}

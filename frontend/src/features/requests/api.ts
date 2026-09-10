export interface BpmRequest {
  id: number;
  companyId: number;
  establishmentType: string;
  reason: string;
  observations: string;
  status: "DRAFT" | "SUBMITTED";
  createdAt: string;
  submittedAt: string | null;
}

export interface InspectionCase { id: number; companyId: number; status: string; sourceReferenceId: number; }
export interface BpmRequestDocumentInput {
  documentType: string; fileName: string; mimeType: string; sizeBytes: number;
  hash: string; storageReference: string;
}
const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");
const options = (accessToken: string, method = "GET", body?: object): RequestInit => ({
  method, headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
  ...(body ? { body: JSON.stringify(body) } : {}),
});

export async function listBpmRequests(accessToken: string): Promise<BpmRequest[]> {
  const response = await fetch(`${baseUrl}/api/bpm-requests`, options(accessToken));
  if (!response.ok) throw new Error("No fue posible consultar tus solicitudes.");
  return response.json() as Promise<BpmRequest[]>;
}

export async function createAndSubmitRequest(accessToken: string, input: {
  companyId: number; establishmentType: string; reason: string; observations: string;
  document: BpmRequestDocumentInput;
}): Promise<{ request: BpmRequest; inspectionCase: InspectionCase }> {
  const { document, ...requestInput } = input;
  const createdResponse = await fetch(`${baseUrl}/api/bpm-requests`, options(accessToken, "POST", requestInput));
  if (!createdResponse.ok) throw new Error("No fue posible guardar la solicitud.");
  const request = await createdResponse.json() as BpmRequest;
  const documentResponse = await fetch(`${baseUrl}/api/bpm-requests/${request.id}/documents`, options(accessToken, "POST", document));
  if (!documentResponse.ok) throw new Error("La solicitud se guardó, pero no pudo adjuntarse su documentación.");
  const submitResponse = await fetch(`${baseUrl}/api/bpm-requests/${request.id}/submit`, options(accessToken, "POST", {}));
  if (!submitResponse.ok) throw new Error("La solicitud se guardó, pero no pudo enviarse.");
  return { request: { ...request, status: "SUBMITTED" }, inspectionCase: await submitResponse.json() as InspectionCase };
}

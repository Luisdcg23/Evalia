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

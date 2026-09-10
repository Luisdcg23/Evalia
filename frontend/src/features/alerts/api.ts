export interface HealthAlert {
  id: number;
  alertNumber: string;
  receivedAt: string;
  product: string;
  companyId: number;
  description: string;
  status: string;
  decisionReason: string | null;
  decidedAt: string | null;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export async function listAlerts(accessToken: string): Promise<HealthAlert[]> {
  const response = await fetch(`${baseUrl}/api/alerts`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar las alertas LAPCH.");
  return response.json() as Promise<HealthAlert[]>;
}

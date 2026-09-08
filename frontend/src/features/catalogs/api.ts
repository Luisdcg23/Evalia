export interface RiskLevel {
  id: number;
  name: string;
  points: number;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export async function listRiskLevels(accessToken: string): Promise<RiskLevel[]> {
  const response = await fetch(`${baseUrl}/api/catalogs/risk-levels`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar los niveles de riesgo.");
  return response.json() as Promise<RiskLevel[]>;
}

export async function createRiskLevel(
  accessToken: string,
  input: { name: string; points: number },
): Promise<RiskLevel> {
  const response = await fetch(`${baseUrl}/api/catalogs/risk-levels`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify({ name: input.name.trim(), points: input.points }),
  });
  if (!response.ok) throw new Error("No fue posible crear el nivel de riesgo.");
  return response.json() as Promise<RiskLevel>;
}

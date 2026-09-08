export interface Company {
  id: number;
  legalName: string;
  rnc: string;
  tradeName: string;
  isActive: boolean;
}

export interface CompanyInput {
  legalName: string;
  rnc: string;
  tradeName: string;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export async function listCompanies(accessToken: string): Promise<Company[]> {
  const response = await fetch(`${baseUrl}/api/companies`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar las empresas.");
  return response.json() as Promise<Company[]>;
}

export async function createCompany(accessToken: string, input: CompanyInput): Promise<Company> {
  const payload = {
    legalName: input.legalName.trim(),
    rnc: input.rnc.trim(),
    tradeName: input.tradeName.trim(),
  };
  const response = await fetch(`${baseUrl}/api/companies`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!response.ok) {
    throw new Error(response.status === 409 ? "El RNC ya está registrado." : "No fue posible crear la empresa.");
  }
  return response.json() as Promise<Company>;
}

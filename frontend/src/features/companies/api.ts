export interface Company {
  id: number;
  legalName: string;
  rnc: string;
  tradeName: string;
  isActive: boolean;
  address?: string;
  municipality?: string;
  province?: string;
  phoneNumber?: string;
  email?: string;
  economicActivity?: string;
  versionToken?: string;
}

export interface CompanyInput {
  legalName: string;
  rnc: string;
  tradeName: string;
}

/** Datos completos del perfil de empresa (RF-03) que exige `PUT /api/companies/{id}`. */
export interface CompanyProfileInput {
  legalName: string;
  tradeName: string;
  address: string;
  municipality: string;
  province: string;
  phoneNumber: string;
  email: string;
  economicActivity: string;
  versionToken: string;
}

/** Representante tipado de una empresa (`GET /api/companies/{id}/representatives`). */
export interface CompanyRepresentative {
  id: number;
  companyId: number;
  fullName: string;
  documentNumber: string;
  email: string;
  phoneNumber: string;
  representativeType: string;
  isActive: boolean;
}

const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export async function listCompanies(accessToken: string): Promise<Company[]> {
  const response = await fetch(`${baseUrl}/api/companies`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar las empresas.");
  return response.json() as Promise<Company[]>;
}

export async function getCompany(accessToken: string, id: number): Promise<Company> {
  const response = await fetch(`${baseUrl}/api/companies/${id}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) {
    throw new Error(response.status === 404
      ? "No fue posible encontrar la empresa."
      : "No fue posible consultar la empresa.");
  }
  return response.json() as Promise<Company>;
}

export async function listRepresentatives(accessToken: string, id: number): Promise<CompanyRepresentative[]> {
  const response = await fetch(`${baseUrl}/api/companies/${id}/representatives`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar los representantes de la empresa.");
  return response.json() as Promise<CompanyRepresentative[]>;
}

export async function updateCompany(
  accessToken: string,
  id: number,
  input: CompanyProfileInput,
): Promise<Company> {
  const payload = {
    legalName: input.legalName.trim(),
    tradeName: input.tradeName.trim(),
    address: input.address.trim(),
    municipality: input.municipality.trim(),
    province: input.province.trim(),
    phoneNumber: input.phoneNumber.trim(),
    email: input.email.trim(),
    economicActivity: input.economicActivity.trim(),
    versionToken: input.versionToken,
  };
  const response = await fetch(`${baseUrl}/api/companies/${id}`, {
    method: "PUT",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!response.ok) {
    if (response.status === 409) {
      throw new Error("La empresa fue modificada por otra persona. Recarga la información e intenta de nuevo.");
    }
    if (response.status === 400) {
      throw new Error("Revisa los datos del perfil: todos los campos son obligatorios y el correo debe ser válido.");
    }
    throw new Error("No fue posible guardar el perfil de la empresa.");
  }
  return response.json() as Promise<Company>;
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

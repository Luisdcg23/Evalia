export type ApiRole =
  | "ADMINISTRADOR"
  | "ADMINISTRADOR_EMPRESA"
  | "USUARIO_DELEGADO"
  | "COORDINADOR"
  | "TECNICO_EVALUADOR";

export interface AuthenticatedUser {
  name: string;
  role: string;
  email: string;
}

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthenticatedUser;
}

export interface RegistrationRequest {
  fullName: string;
  documentNumber: string;
  phoneNumber: string;
  email: string;
  password: string;
  requestedRole: "ADMINISTRADOR_EMPRESA" | "USUARIO_DELEGADO";
}

export interface PendingUser {
  id: string;
  email: string;
  fullName: string;
  documentNumber: string | null;
  phoneNumber: string | null;
  requestedRole: "ADMINISTRADOR_EMPRESA" | "USUARIO_DELEGADO";
}

interface LoginApiResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  role: ApiRole;
  fullName: string;
  email: string;
}

const roleLabels: Record<ApiRole, string> = {
  ADMINISTRADOR: "Administrador del Sistema",
  ADMINISTRADOR_EMPRESA: "Admin de Empresa",
  USUARIO_DELEGADO: "Usuario Delegado",
  COORDINADOR: "Coordinador",
  TECNICO_EVALUADOR: "Técnico Evaluador",
};

const apiUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export async function login(email: string, password: string): Promise<AuthSession> {
  const normalizedEmail = email.trim().toLowerCase();
  const response = await fetch(`${apiUrl}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: normalizedEmail, password }),
  });

  if (!response.ok) {
    throw new Error(response.status === 401
      ? "Correo electrónico o contraseña incorrectos."
      : "No fue posible iniciar sesión. Verifica que la API esté disponible.");
  }

  const result = await response.json() as LoginApiResponse;
  return {
    accessToken: result.accessToken,
    refreshToken: result.refreshToken,
    expiresAt: result.expiresAt,
    user: {
      name: result.fullName,
      role: roleLabels[result.role],
      email: result.email,
    },
  };
}

export async function register(request: RegistrationRequest): Promise<void> {
  const payload = {
    ...request,
    fullName: request.fullName.trim(),
    documentNumber: request.documentNumber.trim(),
    phoneNumber: request.phoneNumber.trim(),
    email: request.email.trim().toLowerCase(),
  };
  const response = await fetch(`${apiUrl}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!response.ok) {
    throw new Error(response.status === 400
      ? "La solicitud contiene datos inválidos o el correo ya está registrado."
      : "No fue posible enviar la solicitud. Verifica que la API esté disponible.");
  }
}

export async function requestPasswordRecovery(email: string): Promise<string | null> {
  const response = await fetch(`${apiUrl}/api/auth/forgot-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: email.trim().toLowerCase() }),
  });
  if (!response.ok) throw new Error("No fue posible generar el código de recuperación.");
  const result = await response.json() as { recoveryCode?: string | null };
  return result.recoveryCode ?? null;
}

export async function changePassword(
  email: string,
  recoveryCode: string,
  newPassword: string,
): Promise<void> {
  const response = await fetch(`${apiUrl}/api/auth/change-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: email.trim().toLowerCase(), recoveryCode, newPassword }),
  });
  if (!response.ok) {
    throw new Error(response.status === 400
      ? "El código expiró o la nueva contraseña no cumple los requisitos."
      : "No fue posible cambiar la contraseña.");
  }
}

export async function getPendingUsers(accessToken: string): Promise<PendingUser[]> {
  const response = await fetch(`${apiUrl}/api/users/pending`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar las solicitudes pendientes.");
  return response.json() as Promise<PendingUser[]>;
}

export async function decidePendingUser(
  accessToken: string,
  userId: string,
  decision: "approve" | "reject",
  reason?: string,
): Promise<void> {
  const response = await fetch(`${apiUrl}/api/users/${userId}/${decision}`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
    },
    body: decision === "reject" ? JSON.stringify({ reason }) : undefined,
  });
  if (!response.ok) throw new Error("No fue posible registrar la decisión administrativa.");
}

export async function logout(accessToken: string, refreshToken: string): Promise<void> {
  await fetch(`${apiUrl}/api/auth/logout`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ refreshToken }),
  });
}

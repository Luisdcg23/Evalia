import { afterEach, describe, expect, it, vi } from "vitest";
import { login } from "./api";
import { register } from "./api";

describe("login", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("calls the API and maps the administrator role for the existing dashboard", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      accessToken: "access-token",
      refreshToken: "refresh-token",
      expiresAt: "2026-09-08T12:00:00Z",
      role: "ADMINISTRADOR",
      fullName: "Administrador EBR",
      email: "admin@ebr.local",
    }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    }));
    vi.stubGlobal("fetch", fetchMock);

    const session = await login("ADMIN@EBR.LOCAL ", "EbrLocal2026!");

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5080/api/auth/login",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ email: "admin@ebr.local", password: "EbrLocal2026!" }),
      }),
    );
    expect(session.user.role).toBe("Administrador del Sistema");
    expect(session.refreshToken).toBe("refresh-token");
  });
});

it("submits an access request with the canonical company role", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
    ok: true,
    status: 202,
    json: async () => ({ id: "2d5f98bd-c31c-4473-87ea-f8985c796a52", status: "PENDIENTE_VALIDACION" }),
  }));

  await register({
    fullName: " Ana Pérez ",
    documentNumber: "001-0000000-1",
    phoneNumber: "8095550101",
    email: "ANA@EXAMPLE.LOCAL ",
    password: "Registro2026!",
    requestedRole: "USUARIO_DELEGADO",
  });

  expect(fetch).toHaveBeenCalledWith(
    "http://localhost:5080/api/auth/register",
    expect.objectContaining({
      body: JSON.stringify({
        fullName: "Ana Pérez",
        documentNumber: "001-0000000-1",
        phoneNumber: "8095550101",
        email: "ana@example.local",
        password: "Registro2026!",
        requestedRole: "USUARIO_DELEGADO",
      }),
    }),
  );
});

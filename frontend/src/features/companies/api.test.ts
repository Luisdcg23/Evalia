import { afterEach, describe, expect, it, vi } from "vitest";
import { createCompany, getCompany, listCompanies, listRepresentatives, updateCompany } from "./api";

describe("companies API", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("lists only the companies returned for the authenticated token", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [{ id: 7, legalName: "Empresa A", rnc: "130000001", tradeName: "A", isActive: true }],
    }));

    const companies = await listCompanies("token-local");

    expect(companies).toHaveLength(1);
    expect(companies[0].rnc).toBe("130000001");
    expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/companies", {
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("normalizes company fields before creation", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: 8, legalName: "Empresa B", rnc: "131000002", tradeName: "B", isActive: true }),
    }));

    await createCompany("token-local", { legalName: " Empresa B ", rnc: " 131000002 ", tradeName: " B " });

    expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/companies", expect.objectContaining({
      body: JSON.stringify({ legalName: "Empresa B", rnc: "131000002", tradeName: "B" }),
    }));
  });

  it("reads the full company profile from GET /api/companies/{id}", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id: 5, legalName: "Empresa C", rnc: "130000005", tradeName: "C", isActive: true,
        address: "Calle 1", municipality: "DN", province: "SD", phoneNumber: "809",
        email: "c@e.do", economicActivity: "Alimentos", versionToken: "tok-1",
      }),
    }));

    const company = await getCompany("token-local", 5);

    expect(company.economicActivity).toBe("Alimentos");
    expect(company.versionToken).toBe("tok-1");
    expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/companies/5", {
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("lists the typed representatives of a company", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [{ id: 1, companyId: 5, fullName: "Ana", documentNumber: "001", email: "a@e.do", phoneNumber: "809", representativeType: "LEGAL", isActive: true }],
    }));

    const reps = await listRepresentatives("token-local", 5);

    expect(reps[0].representativeType).toBe("LEGAL");
    expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/companies/5/representatives", {
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("sends the version token on update and maps the 409 conflict", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) });
    vi.stubGlobal("fetch", fetchMock);

    await expect(updateCompany("token-local", 5, {
      legalName: "Empresa C", tradeName: "C", address: "Calle 1", municipality: "DN",
      province: "SD", phoneNumber: "809", email: "c@e.do", economicActivity: "Alimentos",
      versionToken: "tok-1",
    })).rejects.toThrow(/modificada por otra persona/i);

    const call = fetchMock.mock.calls[0];
    expect(String(call[0])).toBe("http://localhost:5080/api/companies/5");
    expect(call[1]).toMatchObject({ method: "PUT" });
    expect(JSON.parse(call[1].body as string)).toMatchObject({ versionToken: "tok-1", address: "Calle 1" });
  });
});

import { afterEach, describe, expect, it, vi } from "vitest";
import { createCompany, listCompanies } from "./api";

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
});

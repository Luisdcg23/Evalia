import { afterEach, expect, it, vi } from "vitest";
import { createAlert, decideAlert, listAlerts } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("lists alerts with bearer authentication", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 1, status: "PENDING" }] });
  vi.stubGlobal("fetch", fetchMock);
  expect(await listAlerts("token")).toHaveLength(1);
  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/alerts", { headers: { Authorization: "Bearer token" } });
});

it("creates an alert with the exact request body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 9, status: "PENDING" }) });
  vi.stubGlobal("fetch", fetchMock);

  await createAlert("token", {
    alertNumber: "  LAPCH-1  ", receivedAt: "2026-09-10T00:00:00Z",
    product: "  Leche en polvo  ", companyId: 2, description: "  Riesgo detectado  ",
  });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/alerts");
  expect(call[1]).toMatchObject({ method: "POST" });
  expect(JSON.parse(call[1].body as string)).toEqual({
    alertNumber: "LAPCH-1", receivedAt: "2026-09-10T00:00:00Z", product: "Leche en polvo",
    companyId: 2, description: "Riesgo detectado",
  });
});

it("maps a 404 on creating an alert to a company-not-found message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404, json: async () => ({}) }));
  await expect(createAlert("token", {
    alertNumber: "A", receivedAt: "2026-09-10T00:00:00Z", product: "P", companyId: 999, description: "D",
  })).rejects.toThrow(/no existe o está inactiva/i);
});

it("decides an alert with the exact result/reason body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ status: "PROCEED", caseId: 5 }) });
  vi.stubGlobal("fetch", fetchMock);

  const result = await decideAlert("token", 3, { result: "PROCEED", reason: "  procede  " });

  expect(result.caseId).toBe(5);
  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/alerts/3/decision");
  expect(JSON.parse(call[1].body as string)).toEqual({ result: "PROCEED", reason: "procede" });
});

it("maps a 400 on deciding an alert to a validation message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 400, json: async () => ({}) }));
  await expect(decideAlert("token", 3, { result: "PROCEED", reason: "x" })).rejects.toThrow(/no son válidos/i);
});

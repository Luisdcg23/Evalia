import { afterEach, expect, it, vi } from "vitest";
import { createComplaint, decideComplaint, listComplaints } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("lists complaints with bearer authentication", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 1, status: "PENDING" }] });
  vi.stubGlobal("fetch", fetchMock);
  expect(await listComplaints("token")).toHaveLength(1);
  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/complaints", { headers: { Authorization: "Bearer token" } });
});

it("creates a complaint with the exact request body, companyId null when omitted", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 9, status: "PENDING" }) });
  vi.stubGlobal("fetch", fetchMock);

  await createComplaint("token", {
    complaintType: "  Higiene  ", receivedAt: "2026-09-10T00:00:00Z",
    complainant: "  Anónimo  ", description: "  Descripción  ",
  });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/complaints");
  expect(JSON.parse(call[1].body as string)).toEqual({
    complaintType: "Higiene", receivedAt: "2026-09-10T00:00:00Z",
    complainant: "Anónimo", description: "Descripción", companyId: null,
  });
});

it("includes the companyId when provided", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 9 }) });
  vi.stubGlobal("fetch", fetchMock);
  await createComplaint("token", { complaintType: "T", receivedAt: "2026-09-10T00:00:00Z", complainant: "C", description: "D", companyId: 4 });
  expect(JSON.parse(fetchMock.mock.calls[0][1].body as string).companyId).toBe(4);
});

it("decides a complaint with the exact result/reason body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ status: "REFERRED", caseId: null }) });
  vi.stubGlobal("fetch", fetchMock);

  const result = await decideComplaint("token", 3, { result: "REFERRED", reason: "  remitida  " });

  expect(result.status).toBe("REFERRED");
  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/complaints/3/decision");
  expect(JSON.parse(call[1].body as string)).toEqual({ result: "REFERRED", reason: "remitida" });
});

it("maps a 409 on deciding a complaint to the missing-company message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(decideComplaint("token", 3, { result: "PROCEED", reason: "x" })).rejects.toThrow(/asociar una empresa/i);
});

import { afterEach, expect, it, vi } from "vitest";
import { assignTechnician, listCases, listSchedule } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("loads coordinator cases with bearer authentication", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 7, companyId: 2, status: "PENDING_ASSIGNMENT" }] });
  vi.stubGlobal("fetch", fetchMock);
  expect(await listCases("token")).toHaveLength(1);
  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/cases", expect.objectContaining({ headers: { Authorization: "Bearer token" } }));
});

it("requests the real schedule for an explicit interval", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });
  vi.stubGlobal("fetch", fetchMock);
  await listSchedule("token", "2026-09-01T00:00:00Z", "2026-10-01T00:00:00Z");
  expect(String(fetchMock.mock.calls[0][0])).toContain("/api/cases/schedule?from=");
});

it("posts the real assignment with technician and reason", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 3, caseId: 7, technicianId: "t-1", isCurrent: true }) });
  vi.stubGlobal("fetch", fetchMock);

  const result = await assignTechnician("token", 7, { technicianId: "t-1", reason: "  motivo  " });

  expect(result.caseId).toBe(7);
  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/cases/7/assign");
  expect(call[1]).toMatchObject({ method: "POST" });
  expect(JSON.parse(call[1].body as string)).toEqual({ technicianId: "t-1", reason: "motivo" });
});

it("maps a 409 on assignment to a state-conflict message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(assignTechnician("token", 7, { technicianId: "t-1", reason: "x" }))
    .rejects.toThrow(/estado actual/i);
});

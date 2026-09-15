import { afterEach, expect, it, vi } from "vitest";
import {
  assignTechnician, cancelSchedule, createInstitutionalCase, listCases, listCaseSchedules,
  listSchedule, rescheduleCase, scheduleCase,
} from "./api";

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

it("creates an institutional case with the exact request body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 40, companyId: 2, sourceType: "INSTITUTIONAL" }) });
  vi.stubGlobal("fetch", fetchMock);

  await createInstitutionalCase("token", { companyId: 2, reason: "  Programa anual  " });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/cases/institutional");
  expect(JSON.parse(call[1].body as string)).toEqual({ companyId: 2, reason: "Programa anual", observations: undefined });
});

it("maps a 404 on institutional case creation to a company message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404, json: async () => ({}) }));
  await expect(createInstitutionalCase("token", { companyId: 999, reason: "x" })).rejects.toThrow(/no existe o está inactiva/i);
});

it("schedules a case with the exact request body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, caseId: 7, isCurrent: true }) });
  vi.stubGlobal("fetch", fetchMock);

  await scheduleCase("token", 7, { scheduledFor: "2026-09-15T14:00:00Z", reason: "  primera visita  " });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/cases/7/schedule");
  expect(call[1]).toMatchObject({ method: "POST" });
  expect(JSON.parse(call[1].body as string)).toEqual({
    scheduledFor: "2026-09-15T14:00:00Z", reason: "primera visita", priority: undefined, observations: undefined,
  });
});

it("maps a 409 on scheduling to an overlap/state message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(scheduleCase("token", 7, { scheduledFor: "2026-09-15T14:00:00Z", reason: "x" })).rejects.toThrow(/se solapa/i);
});

it("reschedules a case against the reschedule endpoint", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 2, caseId: 7, isCurrent: true }) });
  vi.stubGlobal("fetch", fetchMock);
  await rescheduleCase("token", 7, { scheduledFor: "2026-09-16T14:00:00Z", reason: "reprogramación" });
  expect(String(fetchMock.mock.calls[0][0])).toBe("http://localhost:5080/api/cases/7/reschedule");
});

it("cancels a schedule with the exact reason body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 2, isCurrent: false }) });
  vi.stubGlobal("fetch", fetchMock);
  await cancelSchedule("token", 7, { reason: "  cliente canceló  " });
  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/cases/7/cancel-schedule");
  expect(JSON.parse(call[1].body as string)).toEqual({ reason: "cliente canceló" });
});

it("maps a 409 on cancel-schedule to the no-current-schedule message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(cancelSchedule("token", 7, { reason: "x" })).rejects.toThrow(/no tiene una programación vigente/i);
});

it("lists the schedule history of a case", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 1, caseId: 7 }] });
  vi.stubGlobal("fetch", fetchMock);
  expect(await listCaseSchedules("token", 7)).toHaveLength(1);
  expect(String(fetchMock.mock.calls[0][0])).toBe("http://localhost:5080/api/cases/7/schedules");
});

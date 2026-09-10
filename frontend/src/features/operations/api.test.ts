import { afterEach, expect, it, vi } from "vitest";
import { getDashboard, listMyCases, listMySchedule, listTechnicians, searchCaseHistory } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("reads the role-scoped dashboard metrics with bearer authentication", async () => {
  const payload = { totalCases: 4, byStatus: [{ status: "PENDING_ASSIGNMENT", count: 2 }], unreadNotifications: 1 };
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => payload });
  vi.stubGlobal("fetch", fetchMock);

  const result = await getDashboard("token");

  expect(result).toEqual(payload);
  expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5080/api/dashboard",
    expect.objectContaining({ headers: { Authorization: "Bearer token" } }),
  );
});

it("loads the authenticated technician cases", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 7 }] });
  vi.stubGlobal("fetch", fetchMock);

  expect(await listMyCases("token")).toHaveLength(1);
  expect(String(fetchMock.mock.calls[0][0])).toBe("http://localhost:5080/api/me/cases");
});

it("passes the explicit interval to the personal schedule query", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });
  vi.stubGlobal("fetch", fetchMock);

  await listMySchedule("token", "2026-09-01T00:00:00Z", "2026-10-01T00:00:00Z");

  const url = String(fetchMock.mock.calls[0][0]);
  expect(url).toContain("/api/me/schedule?from=2026-09-01");
  expect(url).toContain("to=2026-10-01");
});

it("requests the technician roster", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: "abc", fullName: "Ana", email: "a@e.do", activeCaseCount: 2 }] });
  vi.stubGlobal("fetch", fetchMock);

  const roster = await listTechnicians("token");

  expect(roster[0].activeCaseCount).toBe(2);
  expect(String(fetchMock.mock.calls[0][0])).toBe("http://localhost:5080/api/technicians");
});

it("builds the historical search query from the provided filters", async () => {
  const page = { page: 1, pageSize: 50, total: 0, items: [] };
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => page });
  vi.stubGlobal("fetch", fetchMock);

  await searchCaseHistory("token", { companyId: 3, status: "approved", from: "2026-01-01T00:00:00Z", page: 2 });

  const url = String(fetchMock.mock.calls[0][0]);
  expect(url).toContain("/api/cases/history/search?");
  expect(url).toContain("companyId=3");
  expect(url).toContain("status=approved");
  expect(url).toContain("from=2026-01-01");
  expect(url).toContain("page=2");
});

it("surfaces a role message when the history search is forbidden", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 403, json: async () => ({}) });
  vi.stubGlobal("fetch", fetchMock);

  await expect(searchCaseHistory("token")).rejects.toThrow(/rol/i);
});

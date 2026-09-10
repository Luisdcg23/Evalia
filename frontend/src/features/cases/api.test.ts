import { afterEach, expect, it, vi } from "vitest";
import { listCases, listSchedule } from "./api";

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

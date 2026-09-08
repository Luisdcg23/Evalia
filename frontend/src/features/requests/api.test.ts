import { afterEach, expect, it, vi } from "vitest";
import { createAndSubmitRequest } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("persists a draft before submitting it", async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 12, status: "DRAFT" }) })
    .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 20, status: "PENDING_ASSIGNMENT" }) });
  vi.stubGlobal("fetch", fetchMock);

  const result = await createAndSubmitRequest("token", { companyId: 2, establishmentType: "Almacén", reason: "Renovación", observations: "" });

  expect(result.request.id).toBe(12);
  expect(fetchMock.mock.calls[1][0]).toContain("/api/bpm-requests/12/submit");
});

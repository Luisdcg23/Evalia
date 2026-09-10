import { afterEach, expect, it, vi } from "vitest";
import { createAndSubmitRequest } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("persists a draft before submitting it", async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 12, status: "DRAFT" }) })
    .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 3 }) })
    .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 20, status: "PENDING_ASSIGNMENT" }) });
  vi.stubGlobal("fetch", fetchMock);

  const result = await createAndSubmitRequest("token", {
    companyId: 2, establishmentType: "Almacén", reason: "Renovación", observations: "",
    document: { documentType: "SOLICITUD_BPM", fileName: "solicitud.pdf", mimeType: "application/pdf", sizeBytes: 42, hash: "abc", storageReference: "client-sha256:abc" },
  });

  expect(result.request.id).toBe(12);
  expect(fetchMock.mock.calls[1][0]).toContain("/api/bpm-requests/12/documents");
  expect(fetchMock.mock.calls[2][0]).toContain("/api/bpm-requests/12/submit");
});

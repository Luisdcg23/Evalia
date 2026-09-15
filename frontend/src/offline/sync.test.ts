import "fake-indexeddb/auto";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { captureResponse, syncPendingResponses } from "./sync";

beforeEach(async () => {
  await new Promise<void>((resolve, reject) => {
    const request = indexedDB.deleteDatabase("evalia-outbox");
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });
});

afterEach(() => vi.unstubAllGlobals());

it("queues the answer captured without network and sends it on the next sync", async () => {
  vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

  const capture = await captureResponse("token", {
    evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "",
  });

  expect(capture).toBe("queued");

  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
  expect(await syncPendingResponses("token")).toEqual({ sent: 1, failed: 0 });
  expect(fetch).toHaveBeenCalledWith(
    "http://localhost:5080/api/evaluations/4/responses/12",
    expect.objectContaining({ method: "PUT" }),
  );
});

it("reports the answer as sent when the network is available", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));

  const capture = await captureResponse("token", {
    evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "",
  });

  expect(capture).toBe("sent");
  expect(await syncPendingResponses("token")).toEqual({ sent: 0, failed: 0 });
});

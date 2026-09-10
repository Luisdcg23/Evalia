import "fake-indexeddb/auto";
import { beforeEach, expect, it, vi } from "vitest";
import { countPendingResponses, enqueueResponse, flushOutbox } from "./outbox";

beforeEach(async () => {
  await new Promise<void>((resolve, reject) => {
    const request = indexedDB.deleteDatabase("evalia-outbox");
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });
});

it("sends only the current user's response and includes a stable operation id", async () => {
  await enqueueResponse({ ownerId: "tech-a", evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "" });
  await enqueueResponse({ ownerId: "tech-b", evaluationId: 5, itemId: 12, optionCode: "C", observations: "", comments: "" });

  const send = vi.fn().mockResolvedValue(undefined);
  const result = await flushOutbox("tech-a", send);

  expect(send).toHaveBeenCalledWith(expect.objectContaining({ ownerId: "tech-a", evaluationId: 4, itemId: 12, operationId: expect.any(String) }));
  expect(result).toEqual({ sent: 1, failed: 0 });
  expect(await countPendingResponses("tech-b")).toBe(1);
});

it("keeps only the last capture when the same question is answered again offline", async () => {
  await enqueueResponse({ evaluationId: 4, itemId: 12, optionCode: "CP", observations: "", comments: "" });
  await enqueueResponse({ evaluationId: 4, itemId: 12, optionCode: "IT", observations: "Sin registro", comments: "" });

  const send = vi.fn().mockResolvedValue(undefined);
  const result = await flushOutbox(send);

  expect(result).toEqual({ sent: 1, failed: 0 });
  expect(send).toHaveBeenCalledTimes(1);
  expect(send).toHaveBeenCalledWith(expect.objectContaining({ optionCode: "IT", observations: "Sin registro" }));
});

it("keeps the response queued when the server cannot be reached and sends it on the next sync", async () => {
  await enqueueResponse({ evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "" });

  const unreachable = vi.fn().mockRejectedValue(new Error("Failed to fetch"));
  expect(await flushOutbox(unreachable)).toEqual({ sent: 0, failed: 1 });

  const reachable = vi.fn().mockResolvedValue(undefined);
  expect(await flushOutbox(reachable)).toEqual({ sent: 1, failed: 0 });
  expect(reachable).toHaveBeenCalledWith(expect.objectContaining({ itemId: 12, optionCode: "C" }));

  expect(await flushOutbox(reachable)).toEqual({ sent: 0, failed: 0 });
  expect(reachable).toHaveBeenCalledTimes(1);
});

it("counts the answers still waiting to be synchronized", async () => {
  expect(await countPendingResponses()).toBe(0);

  await enqueueResponse({ evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "" });
  await enqueueResponse({ evaluationId: 4, itemId: 13, optionCode: "NA", observations: "", comments: "" });
  await enqueueResponse({ evaluationId: 4, itemId: 13, optionCode: "C", observations: "", comments: "" });

  expect(await countPendingResponses()).toBe(2);

  await flushOutbox(vi.fn().mockResolvedValue(undefined));
  expect(await countPendingResponses()).toBe(0);
});

it("does not send the same response twice when two syncs overlap", async () => {
  await enqueueResponse({ evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "" });

  const send = vi.fn().mockImplementation(() => new Promise<void>(resolve => setTimeout(resolve, 10)));
  const [first, second] = await Promise.all([flushOutbox(send), flushOutbox(send)]);

  expect(send).toHaveBeenCalledTimes(1);
  expect(first.sent + second.sent).toBe(1);
});

it("pauses synchronization on 401 without exposing another user's queued data", async () => {
  await enqueueResponse({ ownerId: "tech-a", evaluationId: 4, itemId: 1, optionCode: "C", observations: "", comments: "" });
  await enqueueResponse({ ownerId: "tech-a", evaluationId: 4, itemId: 2, optionCode: "C", observations: "", comments: "" });
  const unauthorized = Object.assign(new Error("expired"), { status: 401 });
  const send = vi.fn().mockRejectedValue(unauthorized);

  expect(await flushOutbox("tech-a", send)).toEqual({ sent: 0, failed: 2 });
  expect(send).toHaveBeenCalledTimes(1);
  expect(await countPendingResponses("tech-a")).toBe(2);
});

it("retains a 409 conflict for explicit resolution", async () => {
  await enqueueResponse({ ownerId: "tech-a", evaluationId: 4, itemId: 1, optionCode: "CP", observations: "", comments: "" });
  const conflict = Object.assign(new Error("conflict"), { status: 409 });
  expect(await flushOutbox("tech-a", vi.fn().mockRejectedValue(conflict))).toEqual({ sent: 0, failed: 1 });
  expect(await countPendingResponses("tech-a")).toBe(1);
});

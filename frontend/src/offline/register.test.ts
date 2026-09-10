import "fake-indexeddb/auto";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { enqueueResponse } from "./outbox";
import { registerServiceWorker, startAutoSync } from "./register";

beforeEach(async () => {
  await new Promise<void>((resolve, reject) => {
    const request = indexedDB.deleteDatabase("evalia-outbox");
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });
});

afterEach(() => vi.unstubAllGlobals());

it("registers the service worker that keeps the application available offline", async () => {
  const container = { register: vi.fn().mockResolvedValue({}) };

  expect(await registerServiceWorker(container as unknown as ServiceWorkerContainer)).toBe(true);
  expect(container.register).toHaveBeenCalledWith("/sw.js", { scope: "/" });
});

it("skips the registration in a browser without service workers", async () => {
  expect(await registerServiceWorker(undefined)).toBe(false);
});

it("synchronizes the queued answers as soon as the connection returns", async () => {
  await enqueueResponse({ evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "" });
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));

  const target = new EventTarget();
  startAutoSync(() => "token", target);
  target.dispatchEvent(new Event("online"));

  await vi.waitFor(() => expect(fetch).toHaveBeenCalledWith(
    "http://localhost:5080/api/evaluations/4/responses/12",
    expect.objectContaining({ method: "PUT" }),
  ));
});

it("does not synchronize while there is no session", async () => {
  await enqueueResponse({ evaluationId: 4, itemId: 12, optionCode: "C", observations: "", comments: "" });
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));

  const target = new EventTarget();
  startAutoSync(() => null, target);
  target.dispatchEvent(new Event("online"));

  await new Promise(resolve => setTimeout(resolve, 20));
  expect(fetch).not.toHaveBeenCalled();
});

import { afterEach, expect, it, vi } from "vitest";
import { listNotifications, markNotificationRead } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("lists notifications without query parameters by default", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });
  vi.stubGlobal("fetch", fetchMock);

  await listNotifications("token");

  expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5080/api/notifications",
    expect.objectContaining({ headers: { Authorization: "Bearer token" } }),
  );
});

it("requests only unread notifications with an explicit page size", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [] });
  vi.stubGlobal("fetch", fetchMock);

  await listNotifications("token", { unreadOnly: true, take: 10 });

  const url = String(fetchMock.mock.calls[0][0]);
  expect(url).toContain("/api/notifications?");
  expect(url).toContain("unreadOnly=true");
  expect(url).toContain("take=10");
});

it("marks a notification as read with a PATCH request", async () => {
  const updated = { id: 5, readAt: "2026-09-10T12:00:00Z" };
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => updated });
  vi.stubGlobal("fetch", fetchMock);

  const result = await markNotificationRead("token", 5);

  expect(result).toEqual(updated);
  expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5080/api/notifications/5/read",
    expect.objectContaining({ method: "PATCH", headers: expect.objectContaining({ Authorization: "Bearer token" }) }),
  );
});

it("reports when the notification no longer exists", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 404, json: async () => ({}) });
  vi.stubGlobal("fetch", fetchMock);

  await expect(markNotificationRead("token", 9)).rejects.toThrow(/ya no está disponible/i);
});

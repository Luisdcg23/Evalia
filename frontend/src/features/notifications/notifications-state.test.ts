import { expect, it } from "vitest";
import type { Notification } from "./api";
import { initialNotificationsState, notificationsReducer } from "./notifications-state";

const build = (id: number, readAt: string | null, createdAt = `2026-09-1${id}T09:00:00Z`): Notification => ({
  id,
  recipientId: "user-1",
  type: "CASE_ASSIGNED",
  title: `Caso ${id}`,
  message: "Detalle",
  referenceType: "CASE",
  referenceId: id,
  operationId: null,
  createdAt,
  readAt,
});

it("derives the unread count from the loaded notifications", () => {
  const state = notificationsReducer(initialNotificationsState, {
    type: "set",
    items: [build(1, null), build(2, "2026-09-12T10:00:00Z"), build(3, null)],
  });

  expect(state.items).toHaveLength(3);
  expect(state.unreadCount).toBe(2);
});

it("orders notifications with the most recent first", () => {
  const state = notificationsReducer(initialNotificationsState, {
    type: "set",
    items: [build(1, null, "2026-09-01T09:00:00Z"), build(2, null, "2026-09-09T09:00:00Z")],
  });

  expect(state.items.map(item => item.id)).toEqual([2, 1]);
});

it("decrements the unread count when a notification is marked read", () => {
  const loaded = notificationsReducer(initialNotificationsState, {
    type: "set",
    items: [build(1, null), build(2, null)],
  });

  const afterRead = notificationsReducer(loaded, { type: "markRead", id: 1, readAt: "2026-09-15T08:00:00Z" });

  expect(afterRead.unreadCount).toBe(1);
  expect(afterRead.items.find(item => item.id === 1)?.readAt).toBe("2026-09-15T08:00:00Z");
});

it("ignores marking an already-read notification", () => {
  const loaded = notificationsReducer(initialNotificationsState, {
    type: "set",
    items: [build(1, "2026-09-11T08:00:00Z"), build(2, null)],
  });

  const afterRead = notificationsReducer(loaded, { type: "markRead", id: 1, readAt: "2026-09-20T08:00:00Z" });

  expect(afterRead.unreadCount).toBe(1);
  expect(afterRead.items.find(item => item.id === 1)?.readAt).toBe("2026-09-11T08:00:00Z");
});

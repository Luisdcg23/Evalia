import type { Notification } from "./api";

/**
 * Estado de la campana de notificaciones. El contador de no leídas se deriva
 * siempre de `items` y del backend (fecha `readAt`), nunca de un contador
 * mantenido a mano en el cliente, para que no diverja de la fuente real.
 */
export interface NotificationsState {
  items: Notification[];
  unreadCount: number;
}

export type NotificationsAction =
  | { type: "set"; items: Notification[] }
  | { type: "markRead"; id: number; readAt: string }
  | { type: "reset" };

export const initialNotificationsState: NotificationsState = { items: [], unreadCount: 0 };

const countUnread = (items: Notification[]): number =>
  items.reduce((total, item) => total + (item.readAt == null ? 1 : 0), 0);

export function notificationsReducer(
  state: NotificationsState,
  action: NotificationsAction,
): NotificationsState {
  switch (action.type) {
    case "set": {
      const items = [...action.items].sort(
        (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
      );
      return { items, unreadCount: countUnread(items) };
    }
    case "markRead": {
      const items = state.items.map(item =>
        item.id === action.id && item.readAt == null
          ? { ...item, readAt: action.readAt }
          : item,
      );
      return { items, unreadCount: countUnread(items) };
    }
    case "reset":
      return initialNotificationsState;
    default:
      return state;
  }
}

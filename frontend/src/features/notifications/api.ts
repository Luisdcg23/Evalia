const baseUrl = (import.meta.env.VITE_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

/** Notificación persistida y privada del usuario autenticado. */
export interface Notification {
  id: number;
  recipientId: string;
  type: string;
  title: string;
  message: string;
  referenceType: string | null;
  referenceId: number | null;
  operationId: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface ListNotificationsOptions {
  unreadOnly?: boolean;
  take?: number;
}

export async function listNotifications(
  accessToken: string,
  { unreadOnly, take }: ListNotificationsOptions = {},
): Promise<Notification[]> {
  const query = new URLSearchParams();
  if (unreadOnly) query.set("unreadOnly", "true");
  if (take != null) query.set("take", String(take));
  const suffix = query.toString() ? `?${query}` : "";
  const response = await fetch(`${baseUrl}/api/notifications${suffix}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!response.ok) throw new Error("No fue posible consultar tus notificaciones.");
  return response.json() as Promise<Notification[]>;
}

/** Marca una notificación como leída en el backend (`PATCH /api/notifications/{id}/read`). */
export async function markNotificationRead(accessToken: string, id: number): Promise<Notification> {
  const response = await fetch(`${baseUrl}/api/notifications/${id}/read`, {
    method: "PATCH",
    headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": "application/json" },
  });
  if (!response.ok) {
    throw new Error(response.status === 404
      ? "La notificación ya no está disponible."
      : "No fue posible marcar la notificación como leída.");
  }
  return response.json() as Promise<Notification>;
}

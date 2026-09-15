import { useCallback, useEffect, useReducer, useRef, useState } from "react";
import { listNotifications, markNotificationRead } from "./api";
import { initialNotificationsState, notificationsReducer } from "./notifications-state";

function formatWhen(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString("es-DO", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });
}

/**
 * Campana de notificaciones: contador de no leídas, lista desplegable y marcado
 * como leída persistido en el backend (`PATCH /api/notifications/{id}/read`).
 * El estado de lectura vive en el servidor; el cliente solo lo refleja.
 */
export default function NotificationsBell({
  accessToken,
  accent = "#3BF6E5",
}: {
  accessToken: string;
  accent?: string;
}) {
  const [state, dispatch] = useReducer(notificationsReducer, initialNotificationsState);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const containerRef = useRef<HTMLDivElement | null>(null);

  const load = useCallback(async () => {
    setError("");
    try {
      const items = await listNotifications(accessToken, { take: 50 });
      dispatch({ type: "set", items });
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar las notificaciones.");
    } finally {
      setLoading(false);
    }
  }, [accessToken]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => { void load(); }, 60_000);
    return () => window.clearInterval(timer);
  }, [load]);

  useEffect(() => {
    if (!open) return;
    const onClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) setOpen(false);
    };
    window.addEventListener("mousedown", onClickOutside);
    return () => window.removeEventListener("mousedown", onClickOutside);
  }, [open]);

  const onMarkRead = async (id: number) => {
    try {
      const updated = await markNotificationRead(accessToken, id);
      dispatch({ type: "markRead", id, readAt: updated.readAt ?? new Date().toISOString() });
    } catch (markError) {
      setError(markError instanceof Error ? markError.message : "No fue posible marcar la notificación.");
    }
  };

  const markAllRead = async () => {
    const unread = state.items.filter(item => item.readAt == null);
    for (const item of unread) await onMarkRead(item.id);
  };

  return (
    <div ref={containerRef} style={{ position: "relative" }}>
      <button
        type="button"
        aria-label="Notificaciones"
        onClick={() => setOpen(current => !current)}
        style={{
          position: "relative", width: 36, height: 36, borderRadius: 10, cursor: "pointer",
          background: "rgba(255,255,255,0.04)", border: `1px solid ${accent}40`,
          display: "flex", alignItems: "center", justifyContent: "center",
        }}
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
          <path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0" stroke={accent} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        {state.unreadCount > 0 && (
          <span style={{
            position: "absolute", top: -4, right: -4, minWidth: 16, height: 16, padding: "0 3px",
            borderRadius: 99, background: "#E53BF6", color: "white", fontSize: "0.55rem", fontWeight: 900,
            display: "flex", alignItems: "center", justifyContent: "center", fontFamily: "Poppins, sans-serif",
          }}>
            {state.unreadCount > 99 ? "99+" : state.unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div style={{
          position: "absolute", right: 0, top: 44, width: 320, maxHeight: 420, overflowY: "auto", zIndex: 120,
          borderRadius: 14, background: "rgba(8,14,28,0.98)", backdropFilter: "blur(28px)",
          border: "1px solid rgba(255,255,255,0.1)", boxShadow: "0 16px 48px rgba(0,0,0,0.6)",
          fontFamily: "Poppins, sans-serif",
        }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "12px 14px", borderBottom: "1px solid rgba(255,255,255,0.07)" }}>
            <span style={{ fontSize: "0.72rem", fontWeight: 700, color: "#f1f5f9", textTransform: "uppercase", letterSpacing: "0.08em" }}>
              Notificaciones{state.unreadCount > 0 ? ` · ${state.unreadCount}` : ""}
            </span>
            {state.unreadCount > 0 && (
              <button type="button" onClick={() => void markAllRead()} style={{ background: "none", border: "none", cursor: "pointer", color: accent, fontSize: "0.62rem", fontWeight: 700, fontFamily: "Poppins, sans-serif" }}>
                Marcar todo
              </button>
            )}
          </div>

          {error && <p role="alert" style={{ padding: "12px 14px", color: "#fda4af", fontSize: "0.72rem" }}>{error}</p>}
          {loading && !error && <p style={{ padding: "16px 14px", color: "#94a3b8", fontSize: "0.72rem" }}>Cargando…</p>}
          {!loading && !error && state.items.length === 0 && (
            <p style={{ padding: "20px 14px", color: "#94a3b8", fontSize: "0.72rem", textAlign: "center" }}>No tienes notificaciones.</p>
          )}

          {state.items.map(item => (
            <button
              key={item.id}
              type="button"
              onClick={() => item.readAt == null && void onMarkRead(item.id)}
              style={{
                width: "100%", textAlign: "left", cursor: item.readAt == null ? "pointer" : "default",
                display: "block", padding: "11px 14px", border: "none",
                borderBottom: "1px solid rgba(255,255,255,0.05)",
                background: item.readAt == null ? "rgba(59,246,229,0.06)" : "transparent",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                {item.readAt == null && <span style={{ width: 6, height: 6, borderRadius: "50%", background: accent, flexShrink: 0 }} />}
                <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "#f1f5f9" }}>{item.title}</span>
              </div>
              <p style={{ fontSize: "0.68rem", color: "rgba(148,163,184,0.75)", margin: "3px 0 0", lineHeight: 1.4 }}>{item.message}</p>
              <p style={{ fontSize: "0.58rem", color: "rgba(148,163,184,0.4)", marginTop: 4 }}>{formatWhen(item.createdAt)}</p>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

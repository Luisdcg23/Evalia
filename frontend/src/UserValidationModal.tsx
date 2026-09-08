import { useCallback, useEffect, useState } from "react";
import { decidePendingUser, getPendingUsers, type PendingUser } from "./auth/api";

const roleLabels = {
  ADMINISTRADOR_EMPRESA: "Administrador de Empresa",
  USUARIO_DELEGADO: "Usuario Delegado",
};

export default function UserValidationModal({
  accessToken,
  onClose,
}: {
  accessToken: string;
  onClose: () => void;
}) {
  const [users, setUsers] = useState<PendingUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [workingId, setWorkingId] = useState<string | null>(null);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      setUsers(await getPendingUsers(accessToken));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar las solicitudes.");
    } finally {
      setLoading(false);
    }
  }, [accessToken]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === "Escape") onClose(); };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [onClose]);

  const decide = async (user: PendingUser, decision: "approve" | "reject") => {
    const reason = decision === "reject"
      ? window.prompt("Motivo del rechazo:")?.trim()
      : undefined;
    if (decision === "reject" && !reason) return;

    setWorkingId(user.id);
    setError("");
    try {
      await decidePendingUser(accessToken, user.id, decision, reason);
      setUsers(current => current.filter(item => item.id !== user.id));
    } catch (decisionError) {
      setError(decisionError instanceof Error ? decisionError.message : "No fue posible guardar la decisión.");
    } finally {
      setWorkingId(null);
    }
  };

  return (
    <main className="mesh-bg" style={{ minHeight: "100svh", padding: "32px 20px", color: "#f1f5f9" }}>
      <section className="glass-card" style={{ maxWidth: 880, margin: "0 auto", padding: 28, borderRadius: 24 }}>
        <header style={{ display: "flex", justifyContent: "space-between", gap: 16, alignItems: "center", marginBottom: 24 }}>
          <div>
            <p style={{ color: "#3BF6E5", fontSize: 11, letterSpacing: ".12em", fontWeight: 700 }}>ADMINISTRACIÓN</p>
            <h1 style={{ fontSize: 24, marginTop: 5 }}>Solicitudes de acceso pendientes</h1>
          </div>
          <button type="button" onClick={onClose} style={secondaryButton}>Volver</button>
        </header>

        {error && <p role="alert" style={{ color: "#fda4af", marginBottom: 16 }}>{error}</p>}
        {loading && <p style={{ color: "#94a3b8" }}>Cargando solicitudes…</p>}
        {!loading && users.length === 0 && (
          <div style={{ padding: 28, textAlign: "center", border: "1px solid rgba(255,255,255,.08)", borderRadius: 16, color: "#94a3b8" }}>
            No hay solicitudes pendientes.
          </div>
        )}

        <div style={{ display: "grid", gap: 12 }}>
          {users.map(user => (
            <article key={user.id} style={{ padding: 18, borderRadius: 16, background: "rgba(255,255,255,.035)", border: "1px solid rgba(255,255,255,.09)" }}>
              <div style={{ display: "flex", flexWrap: "wrap", justifyContent: "space-between", gap: 16 }}>
                <div>
                  <h2 style={{ fontSize: 16 }}>{user.fullName}</h2>
                  <p style={{ color: "#94a3b8", fontSize: 13, marginTop: 4 }}>{user.email}</p>
                  <p style={{ color: "#64748b", fontSize: 12, marginTop: 6 }}>
                    Documento: {user.documentNumber ?? "No indicado"} · Teléfono: {user.phoneNumber ?? "No indicado"}
                  </p>
                  <span style={{ display: "inline-block", marginTop: 10, color: "#3BF6E5", fontSize: 12 }}>
                    {roleLabels[user.requestedRole]}
                  </span>
                </div>
                <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
                  <button type="button" disabled={workingId !== null} onClick={() => void decide(user, "reject")} style={dangerButton}>Rechazar</button>
                  <button type="button" disabled={workingId !== null} onClick={() => void decide(user, "approve")} style={primaryButton}>
                    {workingId === user.id ? "Guardando…" : "Aprobar"}
                  </button>
                </div>
              </div>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}

const buttonBase = {
  padding: "10px 16px",
  borderRadius: 10,
  fontFamily: "Poppins, sans-serif",
  fontWeight: 700,
  cursor: "pointer",
} as const;
const primaryButton = { ...buttonBase, border: 0, background: "linear-gradient(135deg,#E53BF6,#3BF6E5)", color: "#fff" };
const dangerButton = { ...buttonBase, border: "1px solid rgba(251,113,133,.45)", background: "rgba(251,113,133,.08)", color: "#fda4af" };
const secondaryButton = { ...buttonBase, border: "1px solid rgba(255,255,255,.14)", background: "rgba(255,255,255,.04)", color: "#cbd5e1" };

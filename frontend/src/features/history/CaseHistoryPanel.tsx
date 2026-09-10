import { useEffect, useState } from "react";
import { listCompanies, type Company } from "@/features/companies/api";
import {
  searchCaseHistory,
  type CaseHistoryEntry,
  type CaseHistoryFilters,
} from "@/features/operations/api";

/** Estados de expediente que el backend acepta como filtro de la consulta histórica (RF-20). */
const CASE_STATUSES = [
  "PENDING_ASSIGNMENT", "ASSIGNED", "SCHEDULED", "IN_EVALUATION", "PENDING_REPORT",
  "IN_REVIEW", "CORRECTION_REQUIRED", "APPROVED", "CLOSED", "CANCELLED",
];
const SOURCE_TYPES = ["BPM_REQUEST", "HEALTH_ALERT", "COMPLAINT", "INSTITUTIONAL"];

function formatWhen(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString("es-DO", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

const field: React.CSSProperties = {
  width: "100%", padding: "9px 10px", borderRadius: 10, outline: "none",
  background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.1)",
  color: "#f1f5f9", fontFamily: "Poppins, sans-serif", fontSize: "0.78rem", boxSizing: "border-box",
  colorScheme: "dark",
};

/**
 * Consulta histórica de expedientes (RF-20). Filtra por empresa, origen, estado
 * y rango de fechas contra `GET /api/cases/history/search` y muestra el historial
 * de estados con su motivo y responsable. Solo coordinador/administrador.
 */
export default function CaseHistoryPanel({ accessToken }: { accessToken: string }) {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [filters, setFilters] = useState<CaseHistoryFilters>({ page: 1, pageSize: 50 });
  const [rows, setRows] = useState<CaseHistoryEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [status, setStatus] = useState<"idle" | "loading" | "loaded" | "error">("idle");
  const [error, setError] = useState("");

  useEffect(() => {
    void listCompanies(accessToken).then(setCompanies).catch(() => setCompanies([]));
  }, [accessToken]);

  const runSearch = async (nextFilters: CaseHistoryFilters) => {
    setStatus("loading");
    setError("");
    try {
      const result = await searchCaseHistory(accessToken, nextFilters);
      setRows(result.items);
      setTotal(result.total);
      setStatus("loaded");
    } catch (searchError) {
      setError(searchError instanceof Error ? searchError.message : "No fue posible ejecutar la consulta.");
      setStatus("error");
    }
  };

  const update = (patch: Partial<CaseHistoryFilters>) =>
    setFilters(current => ({ ...current, ...patch, page: 1 }));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 960, fontFamily: "Poppins, sans-serif" }}>
      <div>
        <p style={{ fontSize: "0.56rem", fontWeight: 700, color: "rgba(229,59,246,0.5)", letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>EVALIA · CONSULTA HISTÓRICA</p>
        <h2 style={{ fontSize: "1.1rem", fontWeight: 800, color: "#f1f5f9", letterSpacing: "-0.02em" }}>Historial de expedientes</h2>
        <p style={{ fontSize: "0.72rem", color: "rgba(148,163,184,0.45)", marginTop: 2 }}>Consulta el historial de estados, informes y calificaciones por empresa, origen, estado y fecha.</p>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))", gap: 10, padding: 16, borderRadius: 14, background: "rgba(10,18,36,0.7)", border: "1px solid rgba(255,255,255,0.07)" }}>
        <label style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.6)", display: "flex", flexDirection: "column", gap: 4 }}>
          Empresa
          <select style={field} value={filters.companyId ?? ""} onChange={e => update({ companyId: e.target.value ? Number(e.target.value) : undefined })}>
            <option value="">Todas</option>
            {companies.map(company => <option key={company.id} value={company.id}>{company.tradeName}</option>)}
          </select>
        </label>
        <label style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.6)", display: "flex", flexDirection: "column", gap: 4 }}>
          Origen
          <select style={field} value={filters.sourceType ?? ""} onChange={e => update({ sourceType: e.target.value || undefined })}>
            <option value="">Todos</option>
            {SOURCE_TYPES.map(source => <option key={source} value={source}>{source}</option>)}
          </select>
        </label>
        <label style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.6)", display: "flex", flexDirection: "column", gap: 4 }}>
          Estado
          <select style={field} value={filters.status ?? ""} onChange={e => update({ status: e.target.value || undefined })}>
            <option value="">Todos</option>
            {CASE_STATUSES.map(state => <option key={state} value={state}>{state}</option>)}
          </select>
        </label>
        <label style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.6)", display: "flex", flexDirection: "column", gap: 4 }}>
          Desde
          <input type="date" style={field} onChange={e => update({ from: e.target.value ? new Date(e.target.value).toISOString() : undefined })} />
        </label>
        <label style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.6)", display: "flex", flexDirection: "column", gap: 4 }}>
          Hasta
          <input type="date" style={field} onChange={e => update({ to: e.target.value ? new Date(e.target.value).toISOString() : undefined })} />
        </label>
        <button
          type="button"
          onClick={() => void runSearch(filters)}
          style={{ alignSelf: "flex-end", padding: "9px 0", borderRadius: 10, border: "none", cursor: "pointer", background: "linear-gradient(135deg,#E53BF6,#8b5cf6)", color: "white", fontWeight: 700, fontSize: "0.75rem", fontFamily: "Poppins, sans-serif" }}
        >
          Consultar
        </button>
      </div>

      {status === "loading" && <p style={{ color: "#94a3b8", fontSize: "0.78rem" }}>Ejecutando la consulta…</p>}
      {status === "error" && <p role="alert" style={{ color: "#fda4af", fontSize: "0.78rem" }}>{error}</p>}
      {status === "loaded" && rows.length === 0 && (
        <p style={{ color: "rgba(148,163,184,0.55)", fontSize: "0.78rem" }}>La consulta no devolvió expedientes con esos filtros.</p>
      )}

      {status === "loaded" && rows.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <p style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.5)", textTransform: "uppercase", letterSpacing: "0.08em" }}>{total} movimientos de estado</p>
          <div style={{ overflowX: "auto", borderRadius: 12, border: "1px solid rgba(255,255,255,0.07)" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.72rem", color: "#cbd5e1" }}>
              <thead>
                <tr style={{ background: "rgba(255,255,255,0.03)", textAlign: "left" }}>
                  {["Expediente", "Origen", "Cambio de estado", "Motivo", "Fecha", "Responsable"].map(head => (
                    <th key={head} style={{ padding: "9px 12px", fontWeight: 700, color: "rgba(148,163,184,0.7)", whiteSpace: "nowrap" }}>{head}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map(entry => (
                  <tr key={entry.id} style={{ borderTop: "1px solid rgba(255,255,255,0.05)" }}>
                    <td style={{ padding: "9px 12px", fontFamily: "'Courier New', monospace" }}>CAS-{entry.caseId}</td>
                    <td style={{ padding: "9px 12px" }}>{entry.sourceType}</td>
                    <td style={{ padding: "9px 12px", whiteSpace: "nowrap" }}>{(entry.previousStatus ?? "—")} → <strong style={{ color: "#f1f5f9" }}>{entry.newStatus}</strong></td>
                    <td style={{ padding: "9px 12px" }}>{entry.reason ?? "—"}</td>
                    <td style={{ padding: "9px 12px", whiteSpace: "nowrap" }}>{formatWhen(entry.changedAt)}</td>
                    <td style={{ padding: "9px 12px" }}>{entry.changedBy ?? "Sistema"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {total > (filters.pageSize ?? 50) && (
            <div style={{ display: "flex", gap: 8, alignItems: "center", justifyContent: "flex-end" }}>
              <button
                type="button"
                disabled={(filters.page ?? 1) <= 1}
                onClick={() => { const page = (filters.page ?? 1) - 1; setFilters(c => ({ ...c, page })); void runSearch({ ...filters, page }); }}
                style={{ padding: "6px 12px", borderRadius: 8, border: "1px solid rgba(255,255,255,0.12)", background: "rgba(255,255,255,0.04)", color: "#cbd5e1", cursor: "pointer", fontSize: "0.7rem" }}
              >
                Anterior
              </button>
              <span style={{ fontSize: "0.7rem", color: "rgba(148,163,184,0.6)" }}>Página {filters.page ?? 1}</span>
              <button
                type="button"
                disabled={(filters.page ?? 1) * (filters.pageSize ?? 50) >= total}
                onClick={() => { const page = (filters.page ?? 1) + 1; setFilters(c => ({ ...c, page })); void runSearch({ ...filters, page }); }}
                style={{ padding: "6px 12px", borderRadius: 8, border: "1px solid rgba(255,255,255,0.12)", background: "rgba(255,255,255,0.04)", color: "#cbd5e1", cursor: "pointer", fontSize: "0.7rem" }}
              >
                Siguiente
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

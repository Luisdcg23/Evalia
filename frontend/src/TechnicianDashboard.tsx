import { useCallback, useEffect, useState } from "react";
import NotificationsBell from "./features/notifications/NotificationsBell";
import {
  listMyCases, listMySchedule,
  type MyCaseRow, type MyScheduleRow,
} from "./features/operations/api";

function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

/* ── Types ──────────────────────────────────────────────── */
type Section    = "inicio" | "evaluaciones" | "calendario" | "reportes" | "configuracion";
type EvalStatus = "Pendiente" | "En Curso" | "Completado";
type LoadState  = "loading" | "loaded" | "error";

/* ── Backend → UI mappers ──────────────────────────────────
   Los datos operativos provienen de `GET /api/me/cases` y
   `GET /api/me/schedule`, filtrados por la identidad autenticada.
   Ningún dato de esta pantalla es simulado. */
const SOURCE_LABEL: Record<string, string> = {
  BPM_REQUEST: "Solicitud BPM",
  HEALTH_ALERT: "Alerta LAPCH",
  COMPLAINT: "Denuncia",
  INSTITUTIONAL: "Programación institucional",
};
const sourceLabel = (source: string) => SOURCE_LABEL[source] ?? source;

function evalStatus(caseStatus: string): EvalStatus {
  if (caseStatus === "IN_EVALUATION") return "En Curso";
  if (["PENDING_REPORT", "IN_REVIEW", "CORRECTION_REQUIRED", "APPROVED", "CLOSED"].includes(caseStatus)) return "Completado";
  return "Pendiente";
}

const STATUS_COLOR: Record<EvalStatus, { color: string; bg: string; border: string }> = {
  Pendiente:  { color: "rgba(148,163,184,0.7)", bg: "rgba(148,163,184,0.07)", border: "rgba(148,163,184,0.2)" },
  "En Curso": { color: "#F6E53B", bg: "rgba(246,229,59,0.1)", border: "rgba(246,229,59,0.35)" },
  Completado: { color: "#22c55e", bg: "rgba(34,197,94,0.1)", border: "rgba(34,197,94,0.35)" },
};

function formatDay(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleDateString("es-DO", { day: "2-digit", month: "short" });
}
function formatTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleTimeString("es-DO", { hour: "2-digit", minute: "2-digit" });
}

/* ── Nav items ──────────────────────────────────────────── */
const NAV: { id: Section; label: string; icon: (c: string) => React.ReactNode }[] = [
  { id: "inicio",       label: "Inicio",             icon: c => <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" stroke={c} strokeWidth="1.75" /><polyline points="9,22 9,12 15,12 15,22" stroke={c} strokeWidth="1.75" /></svg> },
  { id: "evaluaciones", label: "Evaluaciones",       icon: c => <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M9 11l3 3L22 4" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" /><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" /></svg> },
  { id: "calendario",   label: "Calendario",         icon: c => <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><rect x="3" y="4" width="18" height="18" rx="2" stroke={c} strokeWidth="1.75" /><path d="M16 2v4M8 2v4M3 10h18" stroke={c} strokeWidth="1.75" strokeLinecap="round" /></svg> },
  { id: "reportes",     label: "Pendientes informe", icon: c => <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" stroke={c} strokeWidth="1.75" /><polyline points="14,2 14,8 20,8" stroke={c} strokeWidth="1.75" /><line x1="16" y1="13" x2="8" y2="13" stroke={c} strokeWidth="1.75" strokeLinecap="round" /></svg> },
  { id: "configuracion", label: "Configuración",     icon: c => <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="3" stroke={c} strokeWidth="1.75" /><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" stroke={c} strokeWidth="1.75" /></svg> },
];

/* ── Shared UI ──────────────────────────────────────────── */
function Chip({ label, color, bg, border }: { label: string; color: string; bg: string; border: string }) {
  return <span style={{ display: "inline-flex", alignItems: "center", gap: 5, padding: "2px 9px", borderRadius: 99, background: bg, border: `1px solid ${border}`, fontSize: "0.58rem", fontWeight: 700, color, fontFamily: "Poppins, sans-serif", whiteSpace: "nowrap" }}><span style={{ width: 5, height: 5, borderRadius: "50%", background: color, flexShrink: 0 }} />{label}</span>;
}
function GlassCard({ children, accent, style }: { children: React.ReactNode; accent?: string; style?: React.CSSProperties }) {
  return <div style={{ borderRadius: 16, background: "rgba(10,18,36,0.82)", backdropFilter: "blur(24px) saturate(160%)", WebkitBackdropFilter: "blur(24px) saturate(160%)", borderTop: `1px solid ${accent ? accent + "22" : "rgba(255,255,255,0.07)"}`, borderRight: "1px solid rgba(255,255,255,0.05)", borderBottom: "1px solid rgba(255,255,255,0.05)", borderLeft: `2px solid ${accent ? accent + "55" : "rgba(255,255,255,0.07)"}`, overflow: "hidden", ...style }}>{children}</div>;
}
function SectionHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div style={{ display: "flex", alignItems: "flex-end", justifyContent: "space-between", marginBottom: 20, gap: 12 }}>
      <div>
        <p style={{ fontSize: "0.56rem", fontWeight: 700, color: "rgba(59,246,229,0.5)", letterSpacing: "0.14em", textTransform: "uppercase", fontFamily: "Poppins, sans-serif", marginBottom: 4 }}>EVALIA · TÉCNICO</p>
        <h2 style={{ fontSize: "1.1rem", fontWeight: 800, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", letterSpacing: "-0.02em" }}>{title}</h2>
        {subtitle && <p style={{ fontSize: "0.72rem", color: "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif", marginTop: 2 }}>{subtitle}</p>}
      </div>
    </div>
  );
}
function StateNote({ state, error, empty }: { state: LoadState; error: string; empty: string }) {
  if (state === "loading") return <p style={{ color: "#94a3b8", fontSize: "0.75rem", fontFamily: "Poppins, sans-serif" }}>Cargando…</p>;
  if (state === "error") return <p role="alert" style={{ color: "#fda4af", fontSize: "0.75rem", fontFamily: "Poppins, sans-serif" }}>{error}</p>;
  return <p style={{ color: "rgba(148,163,184,0.55)", fontSize: "0.75rem", fontFamily: "Poppins, sans-serif" }}>{empty}</p>;
}

/* ── Evaluation card ───────────────────────────────────── */
function EvalCard({ row }: { row: MyCaseRow }) {
  const status = evalStatus(row.status);
  const st = STATUS_COLOR[status];
  return (
    <GlassCard accent={st.color}>
      <div style={{ padding: "16px 18px", display: "flex", flexDirection: "column", gap: 10 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
          <div>
            <span style={{ fontSize: "0.52rem", fontFamily: "'Courier New',monospace", color: "rgba(148,163,184,0.25)" }}>CAS-{row.id}</span>
            <p style={{ fontSize: "0.92rem", fontWeight: 700, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", marginBottom: 2 }}>{row.companyName}</p>
            <p style={{ fontSize: "0.65rem", color: "rgba(148,163,184,0.4)", fontFamily: "Poppins, sans-serif" }}>{sourceLabel(row.sourceType)}</p>
          </div>
          <Chip label={status} color={st.color} bg={st.bg} border={st.border} />
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
          <span style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.5)", fontFamily: "Poppins, sans-serif" }}>Recibido {formatDay(row.createdAt)}</span>
          {row.bpmPercentage != null && <Chip label={`BPM ${Number(row.bpmPercentage).toFixed(1)}%`} color="#3BF6E5" bg="rgba(59,246,229,0.1)" border="rgba(59,246,229,0.35)" />}
          {row.classification && <Chip label={row.classification} color="#8b5cf6" bg="rgba(139,92,246,0.1)" border="rgba(139,92,246,0.35)" />}
          {row.frequencyMonths != null && <span style={{ fontSize: "0.6rem", color: "rgba(148,163,184,0.4)", fontFamily: "Poppins, sans-serif" }}>Frecuencia {row.frequencyMonths} meses</span>}
        </div>
      </div>
    </GlassCard>
  );
}

/* ── Section: Inicio ────────────────────────────────────── */
function InicioSection({ cases, schedule, state, error }: {
  cases: MyCaseRow[]; schedule: MyScheduleRow[]; state: LoadState; error: string;
}) {
  const enCurso = cases.filter(c => evalStatus(c.status) === "En Curso").length;
  const pendientes = cases.filter(c => evalStatus(c.status) === "Pendiente").length;
  const completadas = cases.filter(c => evalStatus(c.status) === "Completado").length;
  const informes = cases.filter(c => ["PENDING_REPORT", "CORRECTION_REQUIRED"].includes(c.status)).length;

  const today = new Date().toDateString();
  const todaySchedule = schedule.filter(s => new Date(s.scheduledFor).toDateString() === today);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))", gap: 10 }}>
        {[
          { label: "Expedientes activos", value: cases.length, color: "#3BF6E5" },
          { label: "En curso", value: enCurso, color: "#F6E53B" },
          { label: "Completadas", value: completadas, color: "#22c55e" },
          { label: "Pendientes informe", value: informes, color: "#E53BF6" },
        ].map(s => (
          <GlassCard key={s.label} accent={s.color}>
            <div style={{ padding: "14px 16px" }}>
              <p style={{ fontSize: "1.8rem", fontWeight: 900, color: s.color, fontFamily: "Poppins, sans-serif", lineHeight: 1, textShadow: `0 0 16px ${s.color}50` }}>{state === "loading" ? "…" : s.value}</p>
              <p style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif", marginTop: 4 }}>{s.label}</p>
            </div>
          </GlassCard>
        ))}
      </div>

      <div>
        <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(148,163,184,0.3)", textTransform: "uppercase", letterSpacing: "0.12em", fontFamily: "Poppins, sans-serif", marginBottom: 12 }}>AGENDA DE HOY</p>
        {state !== "loaded" || todaySchedule.length === 0
          ? <StateNote state={state} error={error} empty="No tienes evaluaciones programadas para hoy." />
          : (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {todaySchedule.map(item => (
                <GlassCard key={item.caseId} accent="#3BF6E5">
                  <div style={{ padding: "12px 16px", display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10 }}>
                    <div>
                      <p style={{ fontSize: "0.82rem", fontWeight: 700, color: "#f1f5f9", fontFamily: "Poppins, sans-serif" }}>{item.companyName}</p>
                      <p style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.4)", fontFamily: "Poppins, sans-serif" }}>{item.companyAddress ?? "Sin dirección registrada"}</p>
                    </div>
                    <span style={{ fontSize: "0.72rem", fontWeight: 700, color: "#3BF6E5", fontFamily: "Poppins, sans-serif" }}>{formatTime(item.scheduledFor)}</span>
                  </div>
                </GlassCard>
              ))}
            </div>
          )}
      </div>

      <div>
        <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(148,163,184,0.3)", textTransform: "uppercase", letterSpacing: "0.12em", fontFamily: "Poppins, sans-serif", marginBottom: 12 }}>EXPEDIENTES ASIGNADOS</p>
        {state !== "loaded" || cases.length === 0
          ? <StateNote state={state} error={error} empty="Todavía no tienes expedientes asignados." />
          : <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>{cases.filter(c => evalStatus(c.status) !== "Completado").map(row => <EvalCard key={row.id} row={row} />)}</div>}
        {pendientes > 0 && <p style={{ fontSize: "0.6rem", color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif", marginTop: 6 }}>{pendientes} sin iniciar</p>}
      </div>
    </div>
  );
}

/* ── Section: Evaluaciones ─────────────────────────────── */
function EvaluacionesSection({ cases, state, error }: { cases: MyCaseRow[]; state: LoadState; error: string }) {
  const [filter, setFilter] = useState<EvalStatus | "Todas">("Todas");
  const filtered = filter === "Todas" ? cases : cases.filter(c => evalStatus(c.status) === filter);
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <SectionHeader title="Evaluaciones" subtitle={`${cases.length} expediente${cases.length !== 1 ? "s" : ""} asignado${cases.length !== 1 ? "s" : ""}`} />
      <div className="hide-scroll" style={{ display: "flex", gap: 7, overflowX: "auto" }}>
        {(["Todas", "Pendiente", "En Curso", "Completado"] as const).map(f => {
          const active = filter === f; const c = f === "Todas" ? "rgba(148,163,184,0.7)" : STATUS_COLOR[f].color;
          return <button key={f} onClick={() => setFilter(f)} style={{ flexShrink: 0, padding: "5px 14px", borderRadius: 99, fontSize: "0.65rem", fontWeight: 600, fontFamily: "Poppins, sans-serif", cursor: "pointer", background: active ? `${c}18` : "rgba(255,255,255,0.04)", border: active ? `1px solid ${c}50` : "1px solid rgba(255,255,255,0.09)", color: active ? c : "rgba(148,163,184,0.45)" }}>{f}</button>;
        })}
      </div>
      {state !== "loaded" || filtered.length === 0
        ? <StateNote state={state} error={error} empty="No hay evaluaciones para este filtro." />
        : <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>{filtered.map(row => <EvalCard key={row.id} row={row} />)}</div>}
    </div>
  );
}

/* ── Section: Calendario ────────────────────────────────── */
function CalendarioSection({ schedule, state, error }: { schedule: MyScheduleRow[]; state: LoadState; error: string }) {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const firstWeekday = (new Date(year, month, 1).getDay() + 6) % 7; // lunes = 0
  const cells: (number | null)[] = [...Array(firstWeekday).fill(null), ...Array.from({ length: daysInMonth }, (_, i) => i + 1)];
  const byDay = new Map<number, MyScheduleRow[]>();
  for (const item of schedule) {
    const date = new Date(item.scheduledFor);
    if (date.getFullYear() === year && date.getMonth() === month) {
      const list = byDay.get(date.getDate()) ?? [];
      list.push(item);
      byDay.set(date.getDate(), list);
    }
  }
  const monthLabel = now.toLocaleDateString("es-DO", { month: "long", year: "numeric" });
  const upcoming = [...schedule].filter(s => new Date(s.scheduledFor) >= new Date(now.toDateString()))
    .sort((a, b) => new Date(a.scheduledFor).getTime() - new Date(b.scheduledFor).getTime());

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <SectionHeader title="Calendario" subtitle={monthLabel} />
      <GlassCard accent="#3BF6E5">
        <div style={{ padding: "20px" }}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 4, marginBottom: 8 }}>
            {["L", "M", "X", "J", "V", "S", "D"].map(d => <div key={d} style={{ textAlign: "center", fontSize: "0.58rem", fontWeight: 700, color: "rgba(148,163,184,0.3)", fontFamily: "Poppins, sans-serif", textTransform: "uppercase", padding: "4px 0" }}>{d}</div>)}
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 4 }}>
            {cells.map((day, i) => {
              const events = day ? byDay.get(day) : null;
              const isToday = day === now.getDate();
              return (
                <div key={i} style={{ aspectRatio: "1", borderRadius: 10, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 3, background: isToday ? "rgba(246,229,59,0.15)" : day ? "rgba(255,255,255,0.03)" : "transparent", border: isToday ? "1px solid rgba(246,229,59,0.5)" : "1px solid transparent" }}>
                  {day && <>
                    <span style={{ fontSize: "0.7rem", fontWeight: isToday ? 800 : 400, color: isToday ? "#F6E53B" : "rgba(148,163,184,0.65)", fontFamily: "Poppins, sans-serif" }}>{day}</span>
                    {events && <div style={{ display: "flex", gap: 2 }}>{events.slice(0, 3).map((_, ei) => <span key={ei} style={{ width: 5, height: 5, borderRadius: "50%", background: "#3BF6E5", boxShadow: "0 0 4px #3BF6E5" }} />)}</div>}
                  </>}
                </div>
              );
            })}
          </div>
        </div>
      </GlassCard>
      <GlassCard>
        <div style={{ padding: "16px 20px" }}>
          <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(148,163,184,0.3)", letterSpacing: "0.12em", textTransform: "uppercase", fontFamily: "Poppins, sans-serif", marginBottom: 12 }}>PRÓXIMAS EVALUACIONES</p>
          {state !== "loaded" || upcoming.length === 0
            ? <StateNote state={state} error={error} empty="No tienes evaluaciones programadas." />
            : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {upcoming.map(item => (
                  <div key={item.caseId} style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    <span style={{ fontSize: "0.58rem", fontFamily: "'Courier New',monospace", color: "rgba(148,163,184,0.3)", flexShrink: 0, width: 60 }}>{formatDay(item.scheduledFor)}</span>
                    <span style={{ width: 7, height: 7, borderRadius: "50%", background: "#3BF6E5", flexShrink: 0, boxShadow: "0 0 6px #3BF6E5" }} />
                    <span style={{ fontSize: "0.7rem", color: "rgba(148,163,184,0.6)", fontFamily: "Poppins, sans-serif" }}>{item.companyName} · {formatTime(item.scheduledFor)}</span>
                  </div>
                ))}
              </div>
            )}
        </div>
      </GlassCard>
    </div>
  );
}

/* ── Section: Reportes ──────────────────────────────────── */
function ReportesSection({ cases, state, error }: { cases: MyCaseRow[]; state: LoadState; error: string }) {
  const pending = cases.filter(c => c.status === "PENDING_REPORT");
  const correction = cases.filter(c => c.status === "CORRECTION_REQUIRED");
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <SectionHeader title="Pendientes de Informe" subtitle={`${pending.length + correction.length} expediente(s)`} />
      {state !== "loaded" || (pending.length + correction.length) === 0
        ? <StateNote state={state} error={error} empty="No tienes informes pendientes." />
        : (
          <>
            {correction.length > 0 && <>
              <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(229,59,246,0.5)", textTransform: "uppercase", letterSpacing: "0.1em", fontFamily: "Poppins, sans-serif" }}>CORRECCIÓN SOLICITADA</p>
              {correction.map(row => <EvalCard key={row.id} row={row} />)}
            </>}
            {pending.length > 0 && <>
              <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(148,163,184,0.3)", textTransform: "uppercase", letterSpacing: "0.1em", fontFamily: "Poppins, sans-serif", marginTop: 8 }}>PENDIENTE DE INFORME</p>
              {pending.map(row => <EvalCard key={row.id} row={row} />)}
            </>}
          </>
        )}
    </div>
  );
}

/* ── Section: Configuración ─────────────────────────────── */
function ConfiguracionSection({ userName, onToast }: { userName: string; onToast: (m: string) => void }) {
  const [nombre, setNombre] = useState(userName);
  const [notifNew, setNotifNew] = useState(true);
  const [notifOv, setNotifOv] = useState(true);
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 680 }}>
      <SectionHeader title="Configuración" subtitle="Preferencias de la cuenta" />
      <GlassCard accent="#3BF6E5">
        <div style={{ padding: "20px" }}>
          <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(59,246,229,0.5)", textTransform: "uppercase", letterSpacing: "0.12em", fontFamily: "Poppins, sans-serif", marginBottom: 14 }}>PERFIL DE TÉCNICO</p>
          <div style={{ position: "relative", marginBottom: 11 }}>
            <input value={nombre} onChange={e => setNombre(e.target.value)} style={{ width: "100%", padding: "1.2rem 1rem 0.5rem", borderRadius: 11, outline: "none", background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.09)", color: "#f1f5f9", fontFamily: "Poppins, sans-serif", fontSize: "0.875rem", boxSizing: "border-box" }} />
            <label style={{ position: "absolute", top: "0.5rem", left: "1rem", fontSize: "0.55rem", fontWeight: 700, color: "rgba(59,246,229,0.6)", textTransform: "uppercase", letterSpacing: "0.08em", fontFamily: "Poppins, sans-serif", pointerEvents: "none" }}>Nombre completo</label>
          </div>
          <button onClick={() => onToast("Perfil actualizado")} style={{ marginTop: 4, width: "100%", padding: "11px 0", borderRadius: 12, cursor: "pointer", background: "linear-gradient(135deg,#3BF6E5,#06b6d4)", border: "none", color: "#0F172A", fontFamily: "Poppins, sans-serif", fontWeight: 800, fontSize: "0.85rem" }}>Guardar cambios</button>
        </div>
      </GlassCard>
      <GlassCard accent="#E53BF6">
        <div style={{ padding: "20px" }}>
          <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(229,59,246,0.5)", textTransform: "uppercase", letterSpacing: "0.12em", fontFamily: "Poppins, sans-serif", marginBottom: 14 }}>NOTIFICACIONES</p>
          {[{ label: "Nuevas evaluaciones asignadas", v: notifNew, s: setNotifNew }, { label: "Informes por corregir", v: notifOv, s: setNotifOv }].map(n => (
            <div key={n.label} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "10px 0", borderBottom: "1px solid rgba(255,255,255,0.05)" }}>
              <span style={{ fontSize: "0.78rem", color: "rgba(148,163,184,0.65)", fontFamily: "Poppins, sans-serif" }}>{n.label}</span>
              <button onClick={() => n.s(!n.v)} style={{ width: 44, height: 24, borderRadius: 99, cursor: "pointer", border: "none", background: n.v ? "linear-gradient(135deg,#E53BF6,#8b5cf6)" : "rgba(255,255,255,0.08)", position: "relative", transition: "all 0.2s ease" }}>
                <span style={{ position: "absolute", top: 3, left: n.v ? 22 : 3, width: 18, height: 18, borderRadius: "50%", background: "white", transition: "left 0.2s ease" }} />
              </button>
            </div>
          ))}
        </div>
      </GlassCard>
      <button onClick={() => onToast("Contraseña: revisa tu correo")} style={{ padding: "12px 0", borderRadius: 12, cursor: "pointer", background: "rgba(255,255,255,0.04)", border: "1px solid rgba(255,255,255,0.09)", color: "rgba(148,163,184,0.5)", fontFamily: "Poppins, sans-serif", fontSize: "0.8rem", fontWeight: 600 }}>Cambiar contraseña</button>
    </div>
  );
}

/* ══ Main Component ═════════════════════════════════════════ */
export default function TechnicianDashboard({
  onBack, userName = "Técnico Evaluador", accessToken = "",
}: {
  onBack?: () => void;
  userName?: string;
  accessToken?: string;
}) {
  const width = useWidth();
  const isMobile = width < 768;
  const isTablet = width >= 768 && width < 1100;
  const expanded = !isMobile && !isTablet;

  const [section, setSection] = useState<Section>("inicio");
  const [drawerOpen, setDrawer] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const [cases, setCases] = useState<MyCaseRow[]>([]);
  const [schedule, setSchedule] = useState<MyScheduleRow[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [error, setError] = useState("");

  const showToast = (msg: string) => { setToast(msg); setTimeout(() => setToast(null), 2800); };

  const load = useCallback(async () => {
    if (!accessToken) { setState("error"); setError("Sesión no disponible."); return; }
    setState("loading");
    setError("");
    try {
      const [caseRows, scheduleRows] = await Promise.all([
        listMyCases(accessToken),
        listMySchedule(accessToken),
      ]);
      setCases(caseRows);
      setSchedule(scheduleRows);
      setState("loaded");
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar tus expedientes.");
      setState("error");
    }
  }, [accessToken]);

  useEffect(() => { void load(); }, [load]);

  const currentNav = NAV.find(n => n.id === section)!;

  return (
    <>
      <style>{`
        @keyframes tdPulse { 0%,100%{transform:scale(1);opacity:1} 50%{transform:scale(1.5);opacity:0.5} }
        @keyframes tdToast { from{opacity:0;transform:translateY(10px) scale(0.96)} to{opacity:1;transform:none} }
        .hide-scroll::-webkit-scrollbar{display:none}
        .hide-scroll{-ms-overflow-style:none;scrollbar-width:none}
      `}</style>

      <div style={{ display: "flex", height: "100svh", background: "#0F172A", fontFamily: "Poppins, sans-serif", overflow: "hidden", position: "relative" }}>
        <div className="mesh-orb" style={{ width: 450, height: 450, background: "rgba(59,246,229,0.14)", top: "-100px", left: "-60px", zIndex: 0 }} />
        <div className="mesh-orb mesh-orb-2" style={{ width: 380, height: 380, background: "rgba(246,229,59,0.1)", bottom: "-80px", right: "-60px", zIndex: 0 }} />
        <div className="mesh-orb mesh-orb-3" style={{ width: 220, height: 220, background: "rgba(229,59,246,0.1)", top: "40%", left: "45%", zIndex: 0 }} />

        {isMobile && drawerOpen && <div onClick={() => setDrawer(false)} style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.65)", zIndex: 40, backdropFilter: "blur(3px)" }} />}

        <aside style={{ position: "relative", top: 0, left: 0, height: "100%", width: isTablet ? 64 : 220, flexShrink: 0, zIndex: 10, transition: "width 0.25s ease", background: "rgba(7,12,24,0.96)", backdropFilter: "blur(36px)", WebkitBackdropFilter: "blur(36px)", borderRight: "1px solid rgba(255,255,255,0.07)", display: isMobile ? "none" : "flex", flexDirection: "column" }}>
          <div style={{ padding: expanded ? "20px 18px 16px" : "18px 0 14px", display: "flex", alignItems: "center", gap: 11, justifyContent: expanded ? "flex-start" : "center", borderBottom: "1px solid rgba(255,255,255,0.06)", flexShrink: 0 }}>
            <div style={{ width: 36, height: 36, borderRadius: 10, background: "linear-gradient(135deg,rgba(59,246,229,0.35),rgba(246,229,59,0.25))", border: "1.5px solid rgba(59,246,229,0.5)", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
              <span style={{ fontSize: "0.75rem", fontWeight: 900, background: "linear-gradient(135deg,#3BF6E5,#F6E53B)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text" }}>TE</span>
            </div>
            {expanded && <div><p style={{ fontSize: "0.8rem", fontWeight: 700, color: "#f1f5f9" }}>Técnico Evaluador</p><p style={{ fontSize: "0.52rem", color: "rgba(148,163,184,0.3)", textTransform: "uppercase", letterSpacing: "0.07em" }}>Evalia BPM</p></div>}
          </div>

          <nav style={{ flex: 1, padding: expanded ? "14px 10px" : "14px 0", display: "flex", flexDirection: "column", gap: 2, overflowY: "auto" }} className="hide-scroll">
            {expanded && <p style={{ fontSize: "0.52rem", fontWeight: 700, color: "rgba(148,163,184,0.22)", letterSpacing: "0.14em", textTransform: "uppercase", padding: "0 6px 8px" }}>NAVEGACIÓN</p>}
            {NAV.map(item => {
              const active = section === item.id;
              const color = active ? "#3BF6E5" : "rgba(148,163,184,0.4)";
              return (
                <button key={item.id} onClick={() => { setSection(item.id); if (isMobile) setDrawer(false); }} title={!expanded ? item.label : undefined}
                  style={{ display: "flex", alignItems: "center", gap: 10, padding: expanded ? "9px 12px" : "10px", justifyContent: expanded ? "flex-start" : "center", borderRadius: 10, border: "none", cursor: "pointer", background: active ? "rgba(59,246,229,0.1)" : "transparent", color, fontFamily: "Poppins, sans-serif", fontWeight: active ? 600 : 400, fontSize: "0.78rem", transition: "all 0.18s ease", position: "relative", width: "100%" }}>
                  {active && <div style={{ position: "absolute", left: -10, top: "50%", transform: "translateY(-50%)", width: 3, height: 16, borderRadius: 99, background: "#3BF6E5", boxShadow: "0 0 10px #3BF6E5" }} />}
                  <span>{item.icon(color)}</span>
                  {expanded && <span style={{ flex: 1, textAlign: "left" }}>{item.label}</span>}
                </button>
              );
            })}
          </nav>

          <div style={{ padding: expanded ? "12px 10px" : "12px 0", borderTop: "1px solid rgba(255,255,255,0.06)", flexShrink: 0 }}>
            {expanded && <div style={{ padding: "8px 10px", borderRadius: 10, background: "rgba(255,255,255,0.03)", display: "flex", alignItems: "center", gap: 9, marginBottom: 8 }}>
              <div style={{ width: 28, height: 28, borderRadius: 8, background: "linear-gradient(135deg,#3BF6E5,#F6E53B)", padding: 1.5, flexShrink: 0 }}>
                <div style={{ width: "100%", height: "100%", borderRadius: 6, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "0.65rem", fontWeight: 800, color: "#f1f5f9" }}>{userName[0]}</div>
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <p style={{ fontSize: "0.72rem", fontWeight: 700, color: "#f1f5f9", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{userName}</p>
                <p style={{ fontSize: "0.52rem", color: "rgba(148,163,184,0.35)" }}>Técnico Evaluador</p>
              </div>
            </div>}
            <button onClick={onBack} title={!expanded ? "Salir" : undefined} style={{ display: "flex", alignItems: "center", gap: 9, padding: expanded ? "9px 10px" : "10px", borderRadius: 10, border: "1px solid rgba(59,246,229,0.18)", cursor: "pointer", background: "rgba(59,246,229,0.06)", color: "#3BF6E5", fontFamily: "Poppins, sans-serif", fontSize: "0.72rem", fontWeight: 600, justifyContent: expanded ? "flex-start" : "center", width: "100%" }}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#3BF6E5" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" /></svg>
              {expanded && "Cerrar sesión"}
            </button>
          </div>
        </aside>

        <main style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", position: "relative", zIndex: 1, minWidth: 0 }}>
          <header style={{ flexShrink: 0, display: "flex", alignItems: "center", justifyContent: "space-between", padding: isMobile ? "13px 16px" : "14px 28px", background: "rgba(7,12,24,0.85)", backdropFilter: "blur(24px)", WebkitBackdropFilter: "blur(24px)", borderBottom: "1px solid rgba(255,255,255,0.06)" }}>
            <div>
              <p style={{ fontSize: "0.56rem", fontWeight: 700, color: "rgba(59,246,229,0.5)", letterSpacing: "0.12em", textTransform: "uppercase", fontFamily: "Poppins, sans-serif" }}>EVALIA · TÉCNICO</p>
              <h1 style={{ fontSize: isMobile ? "1rem" : "1.1rem", fontWeight: 800, color: "#f1f5f9", letterSpacing: "-0.02em", fontFamily: "Poppins, sans-serif" }}>{currentNav.label}</h1>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              {accessToken && <NotificationsBell accessToken={accessToken} accent="#3BF6E5" />}
              {isMobile && (
                <button onClick={onBack} title="Cerrar sesión" style={{ width: 36, height: 36, borderRadius: 10, background: "rgba(59,246,229,0.08)", border: "1px solid rgba(59,246,229,0.25)", display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#3BF6E5" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" /></svg>
                </button>
              )}
            </div>
          </header>

          <div key={section} className="hide-scroll" style={{ flex: 1, overflowY: "auto", padding: isMobile ? "16px 14px 84px" : "24px 28px 36px" }}>
            {section === "inicio" && <InicioSection cases={cases} schedule={schedule} state={state} error={error} />}
            {section === "evaluaciones" && <EvaluacionesSection cases={cases} state={state} error={error} />}
            {section === "calendario" && <CalendarioSection schedule={schedule} state={state} error={error} />}
            {section === "reportes" && <ReportesSection cases={cases} state={state} error={error} />}
            {section === "configuracion" && <ConfiguracionSection userName={userName} onToast={showToast} />}
          </div>
        </main>

        {isMobile && (
          <div style={{ position: "fixed", bottom: 0, left: 0, right: 0, zIndex: 60, background: "rgba(7,12,24,0.97)", backdropFilter: "blur(28px)", WebkitBackdropFilter: "blur(28px)", borderTop: "1px solid rgba(255,255,255,0.09)", display: "flex", alignItems: "stretch", height: 66, paddingBottom: "env(safe-area-inset-bottom,0px)" }}>
            {NAV.map(item => {
              const active = section === item.id;
              const accent = "#3BF6E5";
              return (
                <button key={item.id} onClick={() => setSection(item.id)} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 3, border: "none", cursor: "pointer", background: "transparent", color: active ? accent : "rgba(148,163,184,0.4)", position: "relative", paddingTop: 4 }}>
                  {active && <div style={{ position: "absolute", top: 0, left: "50%", transform: "translateX(-50%)", width: 30, height: 2.5, borderRadius: "0 0 3px 3px", background: accent, boxShadow: `0 0 8px ${accent}99` }} />}
                  {item.icon(active ? accent : "rgba(148,163,184,0.4)")}
                  <span style={{ fontSize: "0.5rem", fontWeight: active ? 700 : 400, fontFamily: "Poppins, sans-serif", letterSpacing: "0.02em" }}>{item.label.split(" ")[0]}</span>
                </button>
              );
            })}
          </div>
        )}

        {toast && <div style={{ position: "fixed", bottom: isMobile ? 74 : 24, left: "50%", transform: "translateX(-50%)", zIndex: 90, display: "flex", alignItems: "center", gap: 10, padding: "11px 18px", borderRadius: 13, whiteSpace: "nowrap", background: "rgba(8,14,28,0.97)", backdropFilter: "blur(24px)", border: "1px solid rgba(59,246,229,0.4)", boxShadow: "0 16px 48px rgba(0,0,0,0.6)", animation: "tdToast 0.3s cubic-bezier(.22,1,.36,1) both" }}>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#3BF6E5" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" /></svg>
          <span style={{ fontSize: "0.78rem", fontWeight: 600, color: "#f1f5f9", fontFamily: "Poppins, sans-serif" }}>{toast}</span>
        </div>}
      </div>
    </>
  );
}

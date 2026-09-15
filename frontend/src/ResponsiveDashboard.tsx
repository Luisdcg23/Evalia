import { useState, useEffect } from "react";
import CompanyProfile from "./CompanyProfile";
import { getDashboard, type DashboardMetrics } from "./features/operations/api";
import { listCases, listSchedule, type InspectionCase, type ScheduleEntry } from "./features/cases/api";
import { listCompanies, type Company } from "./features/companies/api";
import { listAlerts, type HealthAlert } from "./features/alerts/api";
import { listComplaints, type Complaint } from "./features/complaints/api";
import { getPendingUsers, type PendingUser } from "./auth/api";
import NotificationsBell from "./features/notifications/NotificationsBell";
import TemplateAdmin from "./features/templates/TemplateAdmin";

/* ── Breakpoint hook ─────────────────────────────────────── */
function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

/* ── Design tokens ───────────────────────────────────────── */
const T = {
  bg:        "#0F172A",
  surface:   "rgba(255,255,255,0.04)",
  surfaceHi: "rgba(255,255,255,0.07)",
  border:    "rgba(255,255,255,0.08)",
  cyan:      "#3BF6E5",
  magenta:   "#E53BF6",
  yellow:    "#F6E53B",
  txt:       "#f1f5f9",
  txtMuted:  "rgba(148,163,184,0.65)",
  txtFaint:  "rgba(148,163,184,0.38)",
};

/* ── Types ───────────────────────────────────────────────── */
interface StatItem { label: string; value: number | string; pct: number; accent: string; trend: string }

/* ── Case status → Spanish label + colour ────────────────── */
const CASE_STATUS: Record<string, { label: string; color: string }> = {
  PENDING_ASSIGNMENT:  { label: "Pendiente de asignación", color: "rgba(148,163,184,0.7)" },
  ASSIGNED:            { label: "Asignado", color: T.cyan },
  SCHEDULED:           { label: "Programado", color: T.cyan },
  IN_EVALUATION:       { label: "En evaluación", color: T.yellow },
  PENDING_REPORT:      { label: "Pendiente de informe", color: T.yellow },
  IN_REVIEW:           { label: "En revisión", color: "#8b5cf6" },
  CORRECTION_REQUIRED: { label: "Corrección solicitada", color: T.magenta },
  APPROVED:            { label: "Aprobado", color: "#4ade80" },
  CLOSED:              { label: "Cerrado", color: "#4ade80" },
  CANCELLED:           { label: "Cancelado", color: T.magenta },
};
const caseStatusMeta = (status: string) => CASE_STATUS[status] ?? { label: status, color: T.txtMuted };

const SOURCE_LABEL: Record<string, string> = {
  BPM_REQUEST: "Solicitud BPM", HEALTH_ALERT: "Alerta LAPCH",
  COMPLAINT: "Denuncia", INSTITUTIONAL: "Programación institucional",
};

const ROLE_LABEL: Record<string, string> = {
  ADMINISTRADOR: "Administrador del Sistema",
  COORDINADOR: "Coordinador",
  TECNICO_EVALUADOR: "Técnico Evaluador",
  ADMINISTRADOR_EMPRESA: "Admin de Empresa",
  USUARIO_DELEGADO: "Usuario Delegado",
};

function formatDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleDateString("es-DO", { day: "2-digit", month: "short", year: "numeric" });
}

/** El backend guarda la decisión de una alerta/denuncia en `status`. Lo traducimos a una etiqueta. */
function decisionLabel(status: string): { label: string; color: string } {
  switch (status) {
    case "PROCEED": return { label: "Procede", color: T.magenta };
    case "NOT_PROCEED": return { label: "No procede", color: "#4ade80" };
    case "REFERRED": return { label: "Remitida", color: T.cyan };
    default: return { label: "En revisión", color: T.yellow };
  }
}

/* ── Nav items ───────────────────────────────────────────── */
const navItems = [
  { id: "dash",    label: "Dashboard",     icon: gridIcon     },
  { id: "empresa", label: "Mi Empresa",    icon: buildingIcon },
  { id: "eval",    label: "Evaluaciones",  icon: clipIcon     },
  { id: "cal",     label: "Calendario",    icon: calIcon      },
  { id: "alert",   label: "Alertas",       icon: warnIcon     },
  { id: "rep",     label: "Reportes",      icon: chartIcon    },
  { id: "plant",   label: "Plantillas",    icon: clipIcon     },
  { id: "cfg",     label: "Configuración", icon: gearIcon     },
];

/* ── SVG icons ───────────────────────────────────────────── */
function gridIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="3" y="3" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/><rect x="14" y="3" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/><rect x="3" y="14" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/><rect x="14" y="14" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/></svg>;
}
function clipIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="8" y="2" width="8" height="4" rx="1" stroke={c} strokeWidth="1.5"/><path d="M8 3H6a2 2 0 00-2 2v14a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2h-2" stroke={c} strokeWidth="1.5"/><path d="M9 12h6M9 16h4" stroke={c} strokeWidth="1.5" strokeLinecap="round"/></svg>;
}
function calIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="3" y="5" width="18" height="16" rx="3" stroke={c} strokeWidth="1.75"/><path d="M8 3v4M16 3v4M3 10h18" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg>;
}
function warnIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg>;
}
function chartIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M18 20V10M12 20V4M6 20v-6" stroke={c} strokeWidth="2" strokeLinecap="round"/></svg>;
}
function gearIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="3" stroke={c} strokeWidth="1.75"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" stroke={c} strokeWidth="1.75"/></svg>;
}
function collapseIcon(c: string, flipped: boolean) {
  return <svg width="16" height="16" viewBox="0 0 24 24" fill="none" style={{ transform: flipped ? "rotate(180deg)" : "none", transition: "transform 0.3s ease" }}><path d="M15 18l-6-6 6-6" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}
function bellIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg>;
}
function searchIcon(c: string) {
  return <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><circle cx="11" cy="11" r="8" stroke={c} strokeWidth="1.75"/><path d="M21 21l-4.35-4.35" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg>;
}
function buildingIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M3 21h18M3 7l9-4 9 4M4 7v14M20 7v14M9 21v-4a3 3 0 016 0v4" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}
function shieldIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}

/* ── Shared sub-components ───────────────────────────────── */
function StatCard({ s }: { s: StatItem }) {
  return (
    <div style={{ flex: 1, minWidth: 0, padding: "20px", borderRadius: 18, background: T.surface, borderTop: `2px solid ${s.accent}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, boxShadow: `0 4px 30px ${s.accent}12`, position: "relative", overflow: "hidden" }}>
      <div style={{ position: "absolute", top: -20, right: -20, width: 80, height: 80, borderRadius: "50%", background: `radial-gradient(ellipse, ${s.accent}1a 0%, transparent 70%)`, pointerEvents: "none" }}/>
      <p style={{ fontSize: "2rem", fontWeight: 900, color: s.accent, lineHeight: 1, marginBottom: 4, textShadow: `0 0 18px ${s.accent}55` }}>{s.value}</p>
      <p style={{ fontSize: "0.78rem", fontWeight: 600, color: T.txt, marginBottom: 2 }}>{s.label}</p>
      <p style={{ fontSize: "0.66rem", color: T.txtMuted, marginBottom: 14 }}>{s.trend}</p>
      <div style={{ height: 5, borderRadius: 99, background: "rgba(255,255,255,0.06)" }}>
        <div style={{ height: "100%", width: `${s.pct}%`, borderRadius: 99, background: `linear-gradient(90deg, ${s.accent}80, ${s.accent})`, boxShadow: `0 0 8px ${s.accent}70` }}/>
      </div>
    </div>
  );
}

/** Anillos derivados de `GET /api/dashboard` (byStatus). Sin datos simulados. */
function ProgressRings({ size = 240, metrics }: { size?: number; metrics: DashboardMetrics | null }) {
  const cx = size / 2, cy = size / 2;
  const scale = size / 220;
  const total = metrics?.totalCases ?? 0;
  const count = (status: string) => metrics?.byStatus.find(s => s.status === status)?.count ?? 0;
  const pctOf = (n: number) => (total > 0 ? Math.round((n / total) * 100) : 0);
  const completed = count("APPROVED") + count("CLOSED");
  const inReport = count("PENDING_REPORT") + count("IN_REVIEW") + count("CORRECTION_REQUIRED");
  const inField = count("IN_EVALUATION") + count("SCHEDULED") + count("ASSIGNED");
  const definedRings = [
    { label: "Cerrados / aprobados", pct: pctOf(completed), color: T.cyan, r: 88 },
    { label: "En informe / revisión", pct: pctOf(inReport), color: T.magenta, r: 66 },
    { label: "En campo", pct: pctOf(inField), color: T.yellow, r: 44 },
  ];
  const scaledRings = definedRings.map((r) => ({ ...r, r: r.r * scale, sw: 13 * scale }));
  const overall = pctOf(completed);
  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 20 }}>
      <div style={{ position: "relative", width: size, height: size }}>
        <svg viewBox={`0 0 ${size} ${size}`} width={size} height={size}>
          {scaledRings.map((r) => {
            const C = 2 * Math.PI * r.r;
            const dash = (r.pct / 100) * C;
            return (
              <g key={r.label}>
                <circle cx={cx} cy={cy} r={r.r} fill="none" stroke="rgba(255,255,255,0.05)" strokeWidth={r.sw}/>
                <circle cx={cx} cy={cy} r={r.r} fill="none" stroke={r.color} strokeWidth={r.sw}
                  strokeDasharray={`${dash} ${C}`} strokeLinecap="round"
                  transform={`rotate(-90 ${cx} ${cy})`}/>
              </g>
            );
          })}
          <text x={cx} y={cy - 8} textAnchor="middle" fill={T.txt} fontSize={size * 0.145} fontWeight="900" fontFamily="Poppins,sans-serif">{overall}%</text>
          <text x={cx} y={cy + 14} textAnchor="middle" fill={T.txtMuted} fontSize={size * 0.053} fontFamily="Poppins,sans-serif">Expedientes cerrados</text>
        </svg>
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "8px 20px", justifyContent: "center" }}>
        {definedRings.map((r) => (
          <div key={r.label} style={{ display: "flex", alignItems: "center", gap: 7 }}>
            <div style={{ width: 8, height: 8, borderRadius: "50%", background: r.color, boxShadow: `0 0 6px ${r.color}` }}/>
            <span style={{ fontSize: "0.72rem", color: T.txtMuted }}>{r.label}</span>
            <span style={{ fontSize: "0.72rem", fontWeight: 700, color: r.color }}>{r.pct}%</span>
          </div>
        ))}
      </div>
    </div>
  );
}

/* ── Section: Evaluaciones (expedientes reales) ─────────────── */
function EvaluacionesSection({ isMobile, cases, companies, loading, error }: {
  isMobile: boolean; cases: InspectionCase[]; companies: Company[]; loading: boolean; error: string;
}) {
  const [filter, setFilter] = useState<"Todas" | string>("Todas");
  const companyName = (id: number) => companies.find(c => c.id === id)?.tradeName ?? `Empresa #${id}`;
  const groups = [
    { key: "Todas", statuses: [] as string[] },
    { key: "En campo", statuses: ["ASSIGNED", "SCHEDULED", "IN_EVALUATION"] },
    { key: "En informe", statuses: ["PENDING_REPORT", "IN_REVIEW", "CORRECTION_REQUIRED"] },
    { key: "Cerradas", statuses: ["APPROVED", "CLOSED"] },
    { key: "Sin asignar", statuses: ["PENDING_ASSIGNMENT"] },
  ];
  const active = groups.find(g => g.key === filter) ?? groups[0];
  const visible = filter === "Todas" ? cases : cases.filter(c => active.statuses.includes(c.status));

  const stats = [
    { label: "Total", value: cases.length, accent: T.cyan },
    { label: "En campo", value: cases.filter(c => ["ASSIGNED", "SCHEDULED", "IN_EVALUATION"].includes(c.status)).length, accent: T.yellow },
    { label: "Cerradas", value: cases.filter(c => ["APPROVED", "CLOSED"].includes(c.status)).length, accent: "#4ade80" },
    { label: "Sin asignar", value: cases.filter(c => c.status === "PENDING_ASSIGNMENT").length, accent: T.txtMuted as string },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Evaluaciones</h2>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(120px, 1fr))", gap: 12 }}>
        {stats.map(s => (
          <div key={s.label} style={{ padding: "18px 16px", borderRadius: 16, background: T.surface, borderTop: `2px solid ${s.accent}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, textAlign: "center" }}>
            <p style={{ fontSize: "1.7rem", fontWeight: 900, color: s.accent, lineHeight: 1, textShadow: `0 0 14px ${s.accent}44` }}>{loading ? "…" : s.value}</p>
            <p style={{ fontSize: "0.68rem", color: T.txtMuted, marginTop: 4 }}>{s.label}</p>
          </div>
        ))}
      </div>

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        {groups.map(g => {
          const isActive = filter === g.key;
          return (
            <button key={g.key} onClick={() => setFilter(g.key)} style={{ padding: "6px 14px", borderRadius: 99, border: `1px solid ${isActive ? T.cyan : T.border}`, background: isActive ? `${T.cyan}18` : "transparent", color: isActive ? T.cyan : T.txtMuted, fontSize: "0.72rem", fontWeight: isActive ? 700 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif" }}>{g.key}</button>
          );
        })}
      </div>

      {loading && <p style={{ color: T.txtMuted, fontSize: "0.78rem" }}>Cargando expedientes…</p>}
      {error && !loading && <p role="alert" style={{ color: "#fda4af", fontSize: "0.78rem" }}>{error}</p>}
      {!loading && !error && visible.length === 0 && <p style={{ color: T.txtFaint, fontSize: "0.78rem" }}>No hay expedientes para este filtro.</p>}

      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {visible.map(ev => {
          const meta = caseStatusMeta(ev.status);
          return (
            <div key={ev.id} style={{ padding: "16px 18px", borderRadius: 16, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${meta.color}`, display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
              <div style={{ flex: 1, minWidth: 140 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 2, flexWrap: "wrap" }}>
                  <span style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt }}>{companyName(ev.companyId)}</span>
                  <span style={{ fontSize: "0.62rem", color: T.txtFaint, padding: "2px 7px", borderRadius: 99, background: T.surface, border: `1px solid ${T.border}` }}>CAS-{ev.id}</span>
                </div>
                <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                  <span style={{ fontSize: "0.7rem", color: T.txtMuted }}>{SOURCE_LABEL[ev.sourceType] ?? ev.sourceType}</span>
                  <span style={{ fontSize: "0.7rem", color: T.txtFaint }}>·</span>
                  <span style={{ fontSize: "0.7rem", color: T.txtFaint }}>{formatDate(ev.createdAt)}</span>
                </div>
              </div>
              <span style={{ padding: "3px 9px", borderRadius: 99, background: `${meta.color}18`, border: `1px solid ${meta.color}40`, fontSize: "0.62rem", fontWeight: 700, color: meta.color }}>{meta.label}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ── Section: Calendario (agenda real) ─────────────────────── */
function CalendarioSection({ isMobile, schedule, loading, error }: {
  isMobile: boolean; schedule: ScheduleEntry[]; loading: boolean; error: string;
}) {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth();
  const daysOfWeek = ["L", "M", "X", "J", "V", "S", "D"];
  const totalDays = new Date(year, month + 1, 0).getDate();
  const startOffset = (new Date(year, month, 1).getDay() + 6) % 7;
  const cells: (number | null)[] = [...Array(startOffset).fill(null), ...Array.from({ length: totalDays }, (_, i) => i + 1)];
  const [selectedDay, setSelectedDay] = useState(now.getDate());

  const byDay = new Map<number, ScheduleEntry[]>();
  for (const entry of schedule) {
    const d = new Date(entry.scheduledFor);
    if (d.getFullYear() === year && d.getMonth() === month) {
      const list = byDay.get(d.getDate()) ?? [];
      list.push(entry); byDay.set(d.getDate(), list);
    }
  }
  const dayEvents = byDay.get(selectedDay) ?? [];
  const upcoming = [...schedule]
    .filter(e => new Date(e.scheduledFor) >= new Date(now.toDateString()))
    .sort((a, b) => new Date(a.scheduledFor).getTime() - new Date(b.scheduledFor).getTime())
    .slice(0, 6);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Calendario de Evaluaciones</h2>
        <p style={{ fontSize: "0.7rem", color: T.txtFaint, marginTop: 2, textTransform: "capitalize" }}>{now.toLocaleDateString("es-DO", { month: "long", year: "numeric" })}</p>
      </div>

      {loading && <p style={{ color: T.txtMuted, fontSize: "0.78rem" }}>Cargando la agenda…</p>}
      {error && !loading && <p role="alert" style={{ color: "#fda4af", fontSize: "0.78rem" }}>{error}</p>}

      <div style={{ display: "grid", gridTemplateColumns: isMobile ? "1fr" : "1fr 1fr", gap: 20, alignItems: "start" }}>
        <div style={{ padding: "22px", borderRadius: 20, background: T.surface, border: `1px solid ${T.border}` }}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 4, marginBottom: 8 }}>
            {daysOfWeek.map(d => <div key={d} style={{ textAlign: "center", fontSize: "0.62rem", fontWeight: 700, color: T.txtFaint, padding: "4px 0" }}>{d}</div>)}
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 4 }}>
            {cells.map((day, i) => {
              if (!day) return <div key={`e-${i}`}/>;
              const hasEvent = byDay.has(day);
              const isSelected = day === selectedDay;
              const isToday = day === now.getDate();
              return (
                <button key={day} onClick={() => setSelectedDay(day)}
                  style={{ position: "relative", aspectRatio: "1", borderRadius: 10, border: isSelected ? `1px solid ${T.cyan}` : isToday ? `1px solid rgba(59,246,229,0.3)` : "1px solid transparent", background: isSelected ? "rgba(59,246,229,0.15)" : isToday ? "rgba(59,246,229,0.07)" : "transparent", color: isSelected ? T.cyan : T.txt, fontSize: "0.78rem", fontWeight: isSelected ? 700 : isToday ? 600 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif", display: "flex", alignItems: "center", justifyContent: "center" }}>
                  {day}
                  {hasEvent && <span style={{ position: "absolute", bottom: 3, left: "50%", transform: "translateX(-50%)", width: 4, height: 4, borderRadius: "50%", background: isSelected ? T.cyan : T.magenta }}/>}
                </button>
              );
            })}
          </div>
        </div>

        <div style={{ padding: "22px", borderRadius: 20, background: T.surface, border: `1px solid ${T.border}` }}>
          <p style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt, marginBottom: 4 }}>{selectedDay} de {now.toLocaleDateString("es-DO", { month: "long" })}</p>
          <p style={{ fontSize: "0.68rem", color: T.txtFaint, marginBottom: 18 }}>
            {dayEvents.length === 0 ? "Sin evaluaciones programadas" : `${dayEvents.length} evaluación${dayEvents.length > 1 ? "es" : ""} programada${dayEvents.length > 1 ? "s" : ""}`}
          </p>
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {dayEvents.length === 0 ? (
              <div style={{ padding: "32px 20px", textAlign: "center", borderRadius: 14, border: `1px dashed ${T.border}` }}>
                <p style={{ fontSize: "0.78rem", color: T.txtFaint }}>Día sin actividad programada</p>
              </div>
            ) : dayEvents.map((ev, i) => (
              <div key={i} style={{ padding: "14px 16px", borderRadius: 14, background: T.surface, borderLeft: `3px solid ${T.cyan}`, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}` }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6 }}>
                  <span style={{ fontSize: "0.8rem", fontWeight: 700, color: T.txt }}>{ev.companyName}</span>
                  <span style={{ fontSize: "0.68rem", fontWeight: 700, color: T.cyan }}>{new Date(ev.scheduledFor).toLocaleTimeString("es-DO", { hour: "2-digit", minute: "2-digit" })}</span>
                </div>
                <p style={{ fontSize: "0.7rem", color: T.txtMuted, marginBottom: 2 }}>CAS-{ev.caseId} · {caseStatusMeta(ev.caseStatus).label}</p>
                <p style={{ fontSize: "0.65rem", color: T.txtFaint }}>{ev.address || "Sin dirección registrada"}</p>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div style={{ padding: "22px", borderRadius: 20, background: T.surface, border: `1px solid ${T.border}` }}>
        <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt, marginBottom: 16 }}>Próximas evaluaciones</p>
        {upcoming.length === 0
          ? <p style={{ fontSize: "0.72rem", color: T.txtFaint }}>No hay evaluaciones programadas en la agenda.</p>
          : (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 10 }}>
              {upcoming.map((ev, i) => {
                const d = new Date(ev.scheduledFor);
                return (
                  <div key={i} style={{ padding: "12px 14px", borderRadius: 12, background: T.surface, borderLeft: `3px solid ${T.cyan}`, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, display: "flex", alignItems: "center", gap: 12 }}>
                    <div style={{ width: 36, height: 36, borderRadius: 10, background: `${T.cyan}18`, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                      <span style={{ fontSize: "0.9rem", fontWeight: 800, color: T.cyan, lineHeight: 1 }}>{d.getDate()}</span>
                      <span style={{ fontSize: "0.52rem", color: T.cyan, opacity: 0.7 }}>{d.toLocaleDateString("es-DO", { month: "short" })}</span>
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <p style={{ fontSize: "0.78rem", fontWeight: 600, color: T.txt, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{ev.companyName}</p>
                      <p style={{ fontSize: "0.65rem", color: T.txtMuted }}>CAS-{ev.caseId} · {d.toLocaleTimeString("es-DO", { hour: "2-digit", minute: "2-digit" })}</p>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
      </div>
    </div>
  );
}

/* ── Section: Alertas (LAPCH + denuncias reales) ───────────── */
function AlertasSection({ isMobile, alerts, complaints, companies, loading, error }: {
  isMobile: boolean; alerts: HealthAlert[]; complaints: Complaint[]; companies: Company[]; loading: boolean; error: string;
}) {
  const [filter, setFilter] = useState<"Todas" | "Alertas LAPCH" | "Denuncias" | "Pendientes">("Todas");
  const companyName = (id: number | null) => id == null ? "Sin empresa" : companies.find(c => c.id === id)?.tradeName ?? `Empresa #${id}`;

  type Row = { key: string; kind: "Alerta LAPCH" | "Denuncia"; title: string; company: string; description: string; when: string; status: string; pending: boolean };
  const rows: Row[] = [
    ...alerts.map(a => ({
      key: `a-${a.id}`, kind: "Alerta LAPCH" as const, title: `LAPCH #${a.alertNumber} · ${a.product}`,
      company: companyName(a.companyId), description: a.description, when: formatDate(a.receivedAt),
      status: a.status, pending: a.status === "PENDING",
    })),
    ...complaints.map(c => ({
      key: `c-${c.id}`, kind: "Denuncia" as const, title: `Denuncia · ${c.complaintType}`,
      company: companyName(c.companyId), description: c.description, when: formatDate(c.receivedAt),
      status: c.status, pending: c.status === "PENDING",
    })),
  ];

  const visible = rows.filter(r =>
    filter === "Todas" ? true
    : filter === "Alertas LAPCH" ? r.kind === "Alerta LAPCH"
    : filter === "Denuncias" ? r.kind === "Denuncia"
    : r.pending);

  const stats = [
    { label: "Alertas LAPCH", value: alerts.length, accent: T.magenta },
    { label: "Denuncias", value: complaints.length, accent: T.yellow },
    { label: "Sin decidir", value: rows.filter(r => r.pending).length, accent: T.cyan },
    { label: "Decididas", value: rows.filter(r => !r.pending).length, accent: "#4ade80" },
  ];
  const chips: (typeof filter)[] = ["Todas", "Alertas LAPCH", "Denuncias", "Pendientes"];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Alertas LAPCH y Denuncias</h2>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(120px, 1fr))", gap: 12 }}>
        {stats.map(s => (
          <div key={s.label} style={{ padding: "18px 16px", borderRadius: 16, background: T.surface, borderTop: `2px solid ${s.accent}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, textAlign: "center" }}>
            <p style={{ fontSize: "1.7rem", fontWeight: 900, color: s.accent, lineHeight: 1, textShadow: `0 0 14px ${s.accent}44` }}>{loading ? "…" : s.value}</p>
            <p style={{ fontSize: "0.68rem", color: T.txtMuted, marginTop: 4 }}>{s.label}</p>
          </div>
        ))}
      </div>

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        {chips.map(c => {
          const isActive = filter === c;
          return <button key={c} onClick={() => setFilter(c)} style={{ padding: "6px 14px", borderRadius: 99, border: `1px solid ${isActive ? T.cyan : T.border}`, background: isActive ? `${T.cyan}18` : "transparent", color: isActive ? T.cyan : T.txtMuted, fontSize: "0.72rem", fontWeight: isActive ? 700 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif" }}>{c}</button>;
        })}
      </div>

      {loading && <p style={{ color: T.txtMuted, fontSize: "0.78rem" }}>Cargando alertas y denuncias…</p>}
      {error && !loading && <p role="alert" style={{ color: "#fda4af", fontSize: "0.78rem" }}>{error}</p>}
      {!loading && !error && visible.length === 0 && (
        <div style={{ padding: "40px 20px", textAlign: "center", borderRadius: 16, border: `1px dashed ${T.border}` }}>
          <p style={{ fontSize: "0.82rem", color: T.txtFaint }}>No hay registros en esta categoría</p>
        </div>
      )}

      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {visible.map(row => {
          const decision = decisionLabel(row.status);
          const accent = row.pending ? T.yellow : decision.color;
          return (
            <div key={row.key} style={{ padding: "16px 18px", borderRadius: 16, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `4px solid ${accent}`, display: "flex", gap: 14, flexWrap: "wrap", alignItems: "flex-start" }}>
              <div style={{ width: 36, height: 36, borderRadius: 10, background: `${accent}18`, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0, marginTop: 2 }}>
                {warnIcon(accent)}
              </div>
              <div style={{ flex: 1, minWidth: 200 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4, flexWrap: "wrap" }}>
                  <span style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt }}>{row.title}</span>
                  <span style={{ padding: "2px 8px", borderRadius: 99, background: `${T.txtFaint}20`, border: `1px solid ${T.border}`, fontSize: "0.6rem", fontWeight: 700, color: T.txtMuted }}>{row.kind}</span>
                  <span style={{ padding: "2px 8px", borderRadius: 99, background: `${decision.color}20`, border: `1px solid ${decision.color}40`, fontSize: "0.6rem", fontWeight: 700, color: decision.color }}>{decision.label}</span>
                </div>
                <p style={{ fontSize: "0.7rem", color: T.txtMuted, marginBottom: 4 }}><span style={{ color: T.cyan, fontWeight: 600 }}>{row.company}</span> · {row.when}</p>
                <p style={{ fontSize: "0.72rem", color: T.txtMuted, lineHeight: 1.5 }}>{row.description}</p>
              </div>
            </div>
          );
        })}
      </div>
      <p style={{ fontSize: "0.66rem", color: T.txtFaint }}>
        La decisión de cada alerta o denuncia (procede / no procede / remitir) se registra desde el panel del coordinador.
      </p>
    </div>
  );
}

/* ── Section: Reportes (métricas derivadas de /api/dashboard) ── */
function ReportesSection({ isMobile, metrics, cases, loading }: {
  isMobile: boolean; metrics: DashboardMetrics | null; cases: InspectionCase[]; loading: boolean;
}) {
  const count = (status: string) => metrics?.byStatus.find(s => s.status === status)?.count ?? 0;
  const total = metrics?.totalCases ?? cases.length;
  const closed = count("APPROVED") + count("CLOSED");
  const kpis = [
    { label: "Expedientes registrados", value: total, sub: "Total histórico", accent: T.cyan },
    { label: "Cerrados / aprobados", value: closed, sub: total > 0 ? `${Math.round((closed / total) * 100)}% del total` : "Sin datos", accent: "#4ade80" },
    { label: "En evaluación", value: count("IN_EVALUATION") + count("SCHEDULED") + count("ASSIGNED"), sub: "En campo", accent: T.yellow },
    { label: "En informe / revisión", value: count("PENDING_REPORT") + count("IN_REVIEW") + count("CORRECTION_REQUIRED"), sub: "Pendiente de cierre", accent: T.magenta },
  ];

  const bySource = Object.entries(
    cases.reduce<Record<string, number>>((acc, c) => {
      acc[c.sourceType] = (acc[c.sourceType] ?? 0) + 1;
      return acc;
    }, {}),
  ).sort((a, b) => b[1] - a[1]);
  const maxSource = Math.max(1, ...bySource.map(([, n]) => n));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Reportes y Analítica</h2>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))", gap: 12 }}>
        {kpis.map(k => (
          <div key={k.label} style={{ padding: "20px 16px", borderRadius: 16, background: T.surface, borderTop: `2px solid ${k.accent}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
            <p style={{ fontSize: "1.9rem", fontWeight: 900, color: k.accent, lineHeight: 1, marginBottom: 4, textShadow: `0 0 14px ${k.accent}44` }}>{loading ? "…" : k.value}</p>
            <p style={{ fontSize: "0.75rem", fontWeight: 600, color: T.txt, marginBottom: 3 }}>{k.label}</p>
            <p style={{ fontSize: "0.64rem", color: T.txtMuted }}>{k.sub}</p>
          </div>
        ))}
      </div>

      <div style={{ padding: "22px", borderRadius: 20, background: T.surface, border: `1px solid ${T.border}` }}>
        <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt, marginBottom: 4 }}>Expedientes por origen</p>
        <p style={{ fontSize: "0.68rem", color: T.txtFaint, marginBottom: 20 }}>Distribución sobre {cases.length} expedientes</p>
        {bySource.length === 0
          ? <p style={{ fontSize: "0.72rem", color: T.txtFaint }}>Sin expedientes registrados.</p>
          : (
            <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
              {bySource.map(([source, n]) => (
                <div key={source}>
                  <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
                    <span style={{ fontSize: "0.75rem", color: T.txt }}>{SOURCE_LABEL[source] ?? source}</span>
                    <span style={{ fontSize: "0.75rem", fontWeight: 700, color: T.cyan }}>{n}</span>
                  </div>
                  <div style={{ height: 6, borderRadius: 99, background: "rgba(255,255,255,0.06)" }}>
                    <div style={{ height: "100%", width: `${(n / maxSource) * 100}%`, borderRadius: 99, background: `linear-gradient(90deg, ${T.cyan}80, ${T.cyan})` }}/>
                  </div>
                </div>
              ))}
            </div>
          )}
      </div>

      <div style={{ padding: "18px 20px", borderRadius: 16, background: T.surface, border: `1px dashed ${T.border}` }}>
        <p style={{ fontSize: "0.75rem", fontWeight: 600, color: T.txt, marginBottom: 4 }}>Series históricas y exportación de informes</p>
        <p style={{ fontSize: "0.68rem", color: T.txtMuted, lineHeight: 1.5 }}>
          El backend aún no expone un endpoint de agregación histórica por mes ni un repositorio de informes
          descargables a nivel de administración. Las métricas anteriores se derivan de <code>GET /api/dashboard</code> y
          <code> GET /api/cases</code>. El informe oficial en PDF de cada expediente se descarga desde su evaluación
          (<code>GET /api/evaluations/&#123;id&#125;/report/official/content</code>).
        </p>
      </div>
    </div>
  );
}

/* ── Section: Configuración ──────────────────────────────── */
function ConfiguracionSection({ isMobile, onLogout, onUserValidation, pendingUsers, pendingLoading, pendingError }: {
  isMobile: boolean; onLogout: () => void; onUserValidation?: () => void;
  pendingUsers: PendingUser[]; pendingLoading: boolean; pendingError: string;
}) {
  const [cfgTab, setCfgTab] = useState<"usuarios" | "roles">("usuarios");
  const tabs: { id: typeof cfgTab; label: string }[] = [
    { id: "usuarios", label: "Solicitudes de registro" },
    { id: "roles",    label: "Roles y Permisos" },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Configuración</h2>
      </div>

      <div style={{ display: "flex", gap: 6, flexWrap: "wrap", padding: "6px", borderRadius: 14, background: T.surface, border: `1px solid ${T.border}`, width: "fit-content" }}>
        {tabs.map(t => (
          <button key={t.id} onClick={() => setCfgTab(t.id)} style={{ padding: "8px 16px", borderRadius: 10, border: cfgTab === t.id ? `1px solid rgba(59,246,229,0.3)` : "1px solid transparent", background: cfgTab === t.id ? "rgba(59,246,229,0.12)" : "transparent", color: cfgTab === t.id ? T.cyan : T.txtMuted, fontSize: "0.78rem", fontWeight: cfgTab === t.id ? 700 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif", whiteSpace: "nowrap" }}>{t.label}</button>
        ))}
      </div>

      {cfgTab === "usuarios" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 10 }}>
            <p style={{ fontSize: "0.82rem", color: T.txtMuted }}>
              {pendingLoading ? "Cargando…" : `${pendingUsers.length} solicitud${pendingUsers.length === 1 ? "" : "es"} de registro pendiente${pendingUsers.length === 1 ? "" : "s"} de validación`}
            </p>
            <button onClick={onUserValidation} style={{ padding: "8px 16px", borderRadius: 10, border: "none", background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, color: "#fff", fontSize: "0.75rem", fontWeight: 700, cursor: "pointer", fontFamily: "Poppins, sans-serif" }}>
              Revisar solicitudes
            </button>
          </div>
          {pendingError && <p role="alert" style={{ fontSize: "0.75rem", color: "#fda4af" }}>{pendingError}</p>}
          {!pendingLoading && !pendingError && pendingUsers.length === 0 && (
            <div style={{ padding: "32px 20px", textAlign: "center", borderRadius: 16, border: `1px dashed ${T.border}` }}>
              <p style={{ fontSize: "0.8rem", color: T.txtFaint }}>No hay solicitudes de registro pendientes.</p>
            </div>
          )}
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {pendingUsers.map(u => (
              <div key={u.id} style={{ padding: "16px 18px", borderRadius: 16, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${T.yellow}`, display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
                <div style={{ width: 40, height: 40, borderRadius: 11, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5, flexShrink: 0 }}>
                  <div style={{ width: "100%", height: "100%", borderRadius: 9, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 800, fontSize: "0.82rem", color: T.txt }}>{(u.fullName?.[0] ?? "?").toUpperCase()}</div>
                </div>
                <div style={{ flex: 1, minWidth: 160 }}>
                  <p style={{ fontSize: "0.84rem", fontWeight: 700, color: T.txt, marginBottom: 2 }}>{u.fullName}</p>
                  <p style={{ fontSize: "0.68rem", color: T.txtMuted }}>{u.email}</p>
                </div>
                <span style={{ padding: "3px 9px", borderRadius: 99, background: `${T.cyan}15`, border: `1px solid ${T.cyan}40`, fontSize: "0.62rem", fontWeight: 700, color: T.cyan }}>
                  {ROLE_LABEL[u.requestedRole] ?? u.requestedRole}
                </span>
                <div style={{ textAlign: "right", flexShrink: 0 }}>
                  <p style={{ fontSize: "0.62rem", color: T.txtFaint }}>Contacto</p>
                  <p style={{ fontSize: "0.68rem", color: T.txtMuted }}>{u.phoneNumber || u.documentNumber || "—"}</p>
                </div>
              </div>
            ))}
          </div>
          <p style={{ fontSize: "0.66rem", color: T.txtFaint }}>
            El backend expone las solicitudes pendientes de validación (<code>GET /api/users/pending</code>); todavía
            no hay un endpoint que liste todas las cuentas ya activas del sistema.
          </p>
        </div>
      )}

      {cfgTab === "roles" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 14 }}>
          {[
            { rol: "Administrador del Sistema", permisos: ["Gestión de usuarios y validación de registros", "Acceso a todos los módulos", "Consulta histórica de expedientes"], color: T.magenta },
            { rol: "Coordinador",               permisos: ["Asignar y reasignar técnicos", "Programar y reprogramar evaluaciones", "Decidir alertas LAPCH y denuncias", "Revisar y aprobar informes"], color: T.cyan },
            { rol: "Técnico Evaluador",         permisos: ["Ejecutar la ficha BPM en campo", "Subir evidencias", "Emitir el informe de la evaluación"], color: T.yellow },
            { rol: "Admin de Empresa",          permisos: ["Enviar solicitudes BPM", "Editar el perfil de la empresa", "Ver sus evaluaciones y resultados"], color: "#4ade80" },
            { rol: "Usuario Delegado",          permisos: ["Ver solicitudes y evaluaciones de la empresa", "Acceso de solo lectura"], color: "rgba(148,163,184,0.7)" },
          ].map(r => (
            <div key={r.rol} style={{ padding: "20px", borderRadius: 18, background: T.surface, borderTop: `2px solid ${r.color}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14 }}>
                {shieldIcon(r.color)}
                <p style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt }}>{r.rol}</p>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {r.permisos.map(p => (
                  <div key={p} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <div style={{ width: 5, height: 5, borderRadius: "50%", background: r.color, flexShrink: 0 }}/>
                    <span style={{ fontSize: "0.7rem", color: T.txtMuted }}>{p}</span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      <div style={{ marginTop: 8, padding: "20px", borderRadius: 18, background: "rgba(229,59,246,0.05)", borderTop: "1px solid rgba(229,59,246,0.15)", borderRight: "1px solid rgba(229,59,246,0.15)", borderBottom: "1px solid rgba(229,59,246,0.15)", borderLeft: "3px solid rgba(229,59,246,0.4)", display: "flex", alignItems: "center", justifyContent: "space-between", gap: 16, flexWrap: "wrap" }}>
        <div>
          <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt, marginBottom: 3 }}>Cerrar sesión</p>
          <p style={{ fontSize: "0.7rem", color: T.txtMuted }}>Salir del panel de administración del sistema</p>
        </div>
        <button onClick={onLogout} style={{ padding: "10px 22px", borderRadius: 12, border: "1px solid rgba(229,59,246,0.4)", background: "rgba(229,59,246,0.1)", color: T.magenta, fontSize: "0.78rem", fontWeight: 700, cursor: "pointer", fontFamily: "Poppins, sans-serif", display: "flex", alignItems: "center", gap: 8, whiteSpace: "nowrap" }}>
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke={T.magenta} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
          Cerrar sesión
        </button>
      </div>
    </div>
  );
}

/* ── Main dashboard ───────────────────────────────────────── */
export interface UserInfo { name: string; role: string; email: string }

export default function ResponsiveDashboard({ onBack, user, accessToken = "", onRisk, onCompanyProfile, onCompanyForm, onCompanyDashboard, onCoordinatorDashboard, onUserValidation }: { onBack: () => void; user?: UserInfo; accessToken?: string; onRisk?: () => void; onCompanyProfile?: () => void; onCompanyForm?: () => void; onCompanyDashboard?: () => void; onCoordinatorDashboard?: () => void; onUserValidation?: () => void }) {
  const width    = useWidth();
  const isMobile = width < 768;
  const isTablet = width >= 768 && width < 1100;

  const [sidebarExpanded, setSidebarExpanded] = useState(true);
  const [activeNav, setActiveNav] = useState("dash");

  const [metrics, setMetrics] = useState<DashboardMetrics | null>(null);
  const [cases, setCases] = useState<InspectionCase[]>([]);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [schedule, setSchedule] = useState<ScheduleEntry[]>([]);
  const [alerts, setAlerts] = useState<HealthAlert[]>([]);
  const [complaints, setComplaints] = useState<Complaint[]>([]);
  const [pendingUsers, setPendingUsers] = useState<PendingUser[]>([]);
  const [opsLoading, setOpsLoading] = useState(true);
  const [opsError, setOpsError] = useState("");
  const [pendingLoading, setPendingLoading] = useState(true);
  const [pendingError, setPendingError] = useState("");

  useEffect(() => {
    if (!accessToken) return;
    let active = true;
    setOpsLoading(true); setOpsError("");
    const from = new Date(Date.now() - 30 * 864e5).toISOString();
    const to = new Date(Date.now() + 90 * 864e5).toISOString();
    void Promise.all([
      getDashboard(accessToken).catch(() => null),
      listCases(accessToken).catch(() => [] as InspectionCase[]),
      listCompanies(accessToken).catch(() => [] as Company[]),
      listSchedule(accessToken, from, to).catch(() => [] as ScheduleEntry[]),
      listAlerts(accessToken).catch(() => [] as HealthAlert[]),
      listComplaints(accessToken).catch(() => [] as Complaint[]),
    ]).then(([dashboardData, caseData, companyData, scheduleData, alertData, complaintData]) => {
      if (!active) return;
      setMetrics(dashboardData);
      setCases(caseData);
      setCompanies(companyData);
      setSchedule(scheduleData);
      setAlerts(alertData);
      setComplaints(complaintData);
      setOpsLoading(false);
      if (!dashboardData && caseData.length === 0) setOpsError("No fue posible cargar la información operativa.");
    });
    return () => { active = false; };
  }, [accessToken]);

  useEffect(() => {
    if (!accessToken) return;
    let active = true;
    setPendingLoading(true); setPendingError("");
    void getPendingUsers(accessToken)
      .then(rows => { if (active) { setPendingUsers(rows); setPendingLoading(false); } })
      .catch(err => { if (active) { setPendingError(err instanceof Error ? err.message : "No fue posible consultar las solicitudes."); setPendingLoading(false); } });
    return () => { active = false; };
  }, [accessToken]);

  if (!user) return null;

  const SIDEBAR_W = sidebarExpanded ? 232 : 68;
  const glass = { background: "rgba(255,255,255,0.03)", backdropFilter: "blur(28px)", WebkitBackdropFilter: "blur(28px)" };
  const GB = `1px solid ${T.border}`;

  const SECTION_TITLE: Record<string, string> = {
    dash: "Dashboard Admin", empresa: "Mi Empresa", eval: "Evaluaciones",
    cal: "Calendario", alert: "Alertas", rep: "Reportes", plant: "Plantillas de evaluación", cfg: "Configuración",
  };

  const count = (status: string) => metrics?.byStatus.find(s => s.status === status)?.count ?? 0;
  const totalCases = metrics?.totalCases ?? cases.length;
  const inEval = count("IN_EVALUATION") + count("PENDING_REPORT") + count("IN_REVIEW");
  const pendingAssign = count("PENDING_ASSIGNMENT");
  const stats: StatItem[] = [
    { label: "Expedientes totales", value: opsLoading ? "…" : totalCases, pct: 100, accent: T.cyan, trend: `${count("APPROVED") + count("CLOSED")} cerrados` },
    { label: "En evaluación", value: opsLoading ? "…" : inEval, pct: totalCases ? Math.round((inEval / totalCases) * 100) : 0, accent: T.magenta, trend: `${count("IN_EVALUATION")} en curso` },
    { label: "Pendientes de asignación", value: opsLoading ? "…" : pendingAssign, pct: totalCases ? Math.round((pendingAssign / totalCases) * 100) : 0, accent: T.yellow, trend: `${metrics?.unreadNotifications ?? 0} notificación(es)` },
  ];

  const activeAlertCount = alerts.filter(a => a.status === "PENDING").length + complaints.filter(c => c.status === "PENDING").length;

  const NavBtn = ({ item }: { item: typeof navItems[number] }) => {
    const active = activeNav === item.id;
    const badge = item.id === "alert" && activeAlertCount > 0 ? activeAlertCount
      : item.id === "cfg" && pendingUsers.length > 0 ? pendingUsers.length : undefined;
    return (
      <button onClick={() => setActiveNav(item.id)} title={!sidebarExpanded ? item.label : undefined}
        style={{ display: "flex", alignItems: "center", gap: 11, padding: sidebarExpanded ? "10px 12px" : "10px", borderRadius: 12, border: "none", cursor: "pointer", width: "100%", justifyContent: sidebarExpanded ? "flex-start" : "center", background: active ? "rgba(59,246,229,0.11)" : "transparent", color: active ? T.cyan : T.txtMuted, fontFamily: "Poppins, sans-serif", fontWeight: active ? 600 : 400, fontSize: "0.82rem", transition: "all 0.2s ease", boxShadow: active ? "inset 0 0 0 1px rgba(59,246,229,0.18)" : "none", position: "relative" }}>
        {active && <div style={{ position: "absolute", left: -12, top: "50%", transform: "translateY(-50%)", width: 3, height: 20, borderRadius: 99, background: T.cyan, boxShadow: `0 0 10px ${T.cyan}` }}/>}
        <span style={{ color: active ? T.cyan : "rgba(148,163,184,0.45)", flexShrink: 0 }}>{item.icon(active ? T.cyan : "rgba(148,163,184,0.45)")}</span>
        {sidebarExpanded && <span style={{ flex: 1, textAlign: "left" }}>{item.label}</span>}
        {sidebarExpanded && badge && (
          <span style={{ padding: "2px 7px", borderRadius: 99, background: "rgba(229,59,246,0.18)", border: "1px solid rgba(229,59,246,0.38)", fontSize: "0.6rem", fontWeight: 700, color: T.magenta }}>{badge}</span>
        )}
        {!sidebarExpanded && badge && (
          <span style={{ position: "absolute", top: 6, right: 6, width: 8, height: 8, borderRadius: "50%", background: T.magenta, border: `2px solid ${T.bg}` }}/>
        )}
      </button>
    );
  };

  const SidebarContent = () => (
    <div style={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <div style={{ padding: sidebarExpanded ? "24px 20px 20px" : "24px 14px 20px", display: "flex", alignItems: "center", justifyContent: sidebarExpanded ? "space-between" : "center", borderBottom: `1px solid ${T.border}` }}>
        {sidebarExpanded && (
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <div style={{ width: 32, height: 32, borderRadius: 9, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M4 6h16M4 10h10M4 14h13M4 18h7" stroke="white" strokeWidth="2.5" strokeLinecap="round"/></svg>
            </div>
            <div>
              <p style={{ fontSize: "0.95rem", fontWeight: 800, color: T.txt, lineHeight: 1.1 }}>Evalia</p>
              <p style={{ fontSize: "0.58rem", color: T.txtFaint, textTransform: "uppercase", letterSpacing: "0.08em" }}>Admin Sistema</p>
            </div>
          </div>
        )}
        {!isMobile && (
          <button onClick={() => setSidebarExpanded(!sidebarExpanded)} style={{ width: 28, height: 28, borderRadius: 8, border: `1px solid ${T.border}`, background: T.surface, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", flexShrink: 0 }}>
            {collapseIcon(T.txtMuted, !sidebarExpanded)}
          </button>
        )}
      </div>
      <nav style={{ flex: 1, padding: "14px 12px", display: "flex", flexDirection: "column", gap: 3, overflowY: "auto" }}>
        {sidebarExpanded && <p style={{ fontSize: "0.58rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", padding: "4px 6px 8px" }}>Principal</p>}
        {navItems.map((item) => <NavBtn key={item.id} item={item} />)}
      </nav>
      <div style={{ padding: "12px", borderTop: `1px solid ${T.border}` }}>
        {sidebarExpanded ? (
          <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px", borderRadius: 12, background: T.surface }}>
            <div style={{ width: 34, height: 34, borderRadius: 9, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5, flexShrink: 0 }}>
              <div style={{ width: "100%", height: "100%", borderRadius: 8, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 800, fontSize: "0.8rem", color: T.txt }}>{(user.name?.[0] ?? "U").toUpperCase()}</div>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <p style={{ fontSize: "0.78rem", fontWeight: 700, color: T.txt, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{user.name ?? ""}</p>
              <p style={{ fontSize: "0.62rem", color: T.txtFaint }}>{user.role ?? ""}</p>
            </div>
            <button onClick={onBack} title="Salir" style={{ background: "none", border: "none", cursor: "pointer", color: T.txtFaint, padding: 4 }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke={T.txtFaint} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
            </button>
          </div>
        ) : (
          <div style={{ display: "flex", justifyContent: "center" }}>
            <div style={{ width: 36, height: 36, borderRadius: 9, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5 }}>
              <div style={{ width: "100%", height: "100%", borderRadius: 8, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 800, fontSize: "0.85rem", color: T.txt }}>{(user.name?.[0] ?? "U").toUpperCase()}</div>
            </div>
          </div>
        )}
      </div>
    </div>
  );

  const chartSize = isMobile ? 200 : isTablet ? 210 : 230;

  const mobileTabItems = [
    { id: "dash",  label: "Inicio",   icon: gridIcon  },
    { id: "eval",  label: "Eval.",    icon: clipIcon  },
    { id: "alert", label: "Alertas",  icon: warnIcon  },
    { id: "rep",   label: "Reportes", icon: chartIcon },
    { id: "cfg",   label: "Config.",  icon: gearIcon  },
  ];

  const recentCases = [...cases].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).slice(0, 6);
  const companyName = (id: number) => companies.find(c => c.id === id)?.tradeName ?? `Empresa #${id}`;

  return (
    <div style={{ display: "flex", height: "100svh", background: T.bg, fontFamily: "Poppins, sans-serif", overflow: "hidden", position: "relative" }}>

      <aside style={{ ...glass, borderTop: "none", borderBottom: "none", borderLeft: "none", borderRight: GB, width: SIDEBAR_W, flexShrink: 0, height: "100%", transition: "width 0.28s cubic-bezier(.4,0,.2,1)", overflow: "hidden", zIndex: 10, borderRadius: 0, display: isMobile ? "none" : "flex", flexDirection: "column" }}>
        <SidebarContent />
      </aside>

      <main style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", minWidth: 0 }}>
        <header style={{ ...glass, borderTop: "none", borderLeft: "none", borderRight: "none", borderBottom: GB, flexShrink: 0, display: "flex", alignItems: "center", justifyContent: "space-between", padding: "14px 24px", borderRadius: 0 }}>
          <div>
            <h1 style={{ fontSize: isMobile ? "1rem" : "1.1rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>{SECTION_TITLE[activeNav] ?? "Dashboard"}</h1>
            {!isMobile && <p style={{ fontSize: "0.68rem", color: T.txtFaint, textTransform: "capitalize" }}>{new Date().toLocaleDateString("es-DO", { weekday: "long", day: "numeric", month: "long", year: "numeric" })}</p>}
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {!isMobile && (
              <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "7px 14px", borderRadius: 10, background: T.surface, border: `1px solid ${T.border}` }}>
                {searchIcon(T.txtFaint)}
                <span style={{ fontSize: "0.75rem", color: T.txtFaint }}>Buscar…</span>
              </div>
            )}
            {accessToken
              ? <NotificationsBell accessToken={accessToken} accent={T.cyan} />
              : (
                <div style={{ position: "relative", width: 36, height: 36, borderRadius: 10, background: T.surface, border: `1px solid ${T.border}`, display: "flex", alignItems: "center", justifyContent: "center" }}>
                  {bellIcon(T.txtMuted)}
                </div>
              )}
            <div style={{ width: 36, height: 36, borderRadius: 10, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5 }}>
              <div style={{ width: "100%", height: "100%", borderRadius: 8, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 800, fontSize: "0.8rem", color: T.txt }}>{(user.name?.[0] ?? "U").toUpperCase()}</div>
            </div>
            {isMobile && (
              <button onClick={onBack} title="Cerrar sesión" style={{ width: 36, height: 36, borderRadius: 10, background: T.surface, border: `1px solid ${T.border}`, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke={T.txtMuted} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
              </button>
            )}
          </div>
        </header>

        <div key={activeNav} style={{ flex: 1, overflowY: "auto", padding: isMobile ? "16px 14px 84px" : "24px 28px 36px", display: "flex", flexDirection: "column", gap: 20 }} className="hide-scroll">

          {activeNav === "dash" && (<>
            <div>
              <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Vista general</p>
              <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>
                Buen día, <span style={{ background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text" }}>{user.name ?? ""}</span>
              </h2>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {onCompanyDashboard && (
                <button onClick={onCompanyDashboard} style={{ width: "100%", display: "flex", alignItems: "center", gap: 10, padding: "13px 18px", borderRadius: 14, border: "none", cursor: "pointer", background: "linear-gradient(135deg, rgba(14,42,42,0.9) 0%, rgba(26,14,42,0.9) 60%, rgba(42,14,26,0.9) 100%)", borderTop: "1px solid rgba(59,246,229,0.25)", borderRight: "1px solid rgba(229,59,246,0.18)", borderBottom: "1px solid rgba(229,59,246,0.18)", borderLeft: "3px solid rgba(59,246,229,0.6)" }}>
                  <div style={{ width: 36, height: 36, borderRadius: 10, background: "linear-gradient(135deg, rgba(59,246,229,0.2), rgba(229,59,246,0.2))", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>{buildingIcon(T.cyan)}</div>
                  <div style={{ textAlign: "left", flex: 1 }}>
                    <p style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt, marginBottom: 1 }}>Dashboard de Empresa</p>
                    <p style={{ fontSize: "0.62rem", color: T.txtMuted }}>Solicitudes BPM · Evaluaciones · Trámites</p>
                  </div>
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" style={{ flexShrink: 0 }}><path d="M9 18l6-6-6-6" stroke={T.cyan} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
                </button>
              )}
              {onCoordinatorDashboard && (
                <button onClick={onCoordinatorDashboard} style={{ width: "100%", display: "flex", alignItems: "center", gap: 10, padding: "13px 18px", borderRadius: 14, border: "none", cursor: "pointer", background: "linear-gradient(135deg, rgba(42,14,42,0.9) 0%, rgba(14,14,42,0.9) 60%, rgba(14,32,42,0.9) 100%)", borderTop: "1px solid rgba(229,59,246,0.3)", borderRight: "1px solid rgba(59,246,229,0.18)", borderBottom: "1px solid rgba(59,246,229,0.18)", borderLeft: "3px solid rgba(229,59,246,0.7)" }}>
                  <div style={{ width: 36, height: 36, borderRadius: 10, background: "linear-gradient(135deg, rgba(229,59,246,0.2), rgba(59,246,229,0.2))", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="3" y="3" width="7" height="7" rx="2" stroke={T.magenta} strokeWidth="1.75"/><rect x="14" y="3" width="7" height="7" rx="2" stroke={T.magenta} strokeWidth="1.75"/><rect x="3" y="14" width="7" height="7" rx="2" stroke={T.magenta} strokeWidth="1.75"/><rect x="14" y="14" width="7" height="7" rx="2" stroke={T.magenta} strokeWidth="1.75"/></svg>
                  </div>
                  <div style={{ textAlign: "left", flex: 1 }}>
                    <p style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt, marginBottom: 1 }}>Panel Coordinador</p>
                    <p style={{ fontSize: "0.62rem", color: T.txtMuted }}>Asignaciones · Métricas · Alertas LAPCH</p>
                  </div>
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" style={{ flexShrink: 0 }}><path d="M9 18l6-6-6-6" stroke={T.magenta} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
                </button>
              )}
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))", gap: 14 }}>
              {stats.map((s) => <StatCard key={s.label} s={s} />)}
            </div>

            <div style={{ display: "grid", gridTemplateColumns: isMobile ? "1fr" : isTablet ? "1fr" : "auto 1fr", gap: 20, alignItems: "start" }}>
              <div style={{ ...glass, borderTop: GB, borderRight: GB, borderBottom: GB, borderLeft: GB, borderRadius: 20, padding: "28px 24px", display: "flex", flexDirection: "column", alignItems: "center", gap: 4 }}>
                <p style={{ fontSize: "0.68rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.12em", textTransform: "uppercase", marginBottom: 16, alignSelf: "flex-start" }}>Estado de los expedientes</p>
                <ProgressRings size={chartSize} metrics={metrics} />
              </div>
              <div style={{ ...glass, borderTop: GB, borderRight: GB, borderBottom: GB, borderLeft: GB, borderRadius: 20, padding: "22px 20px" }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 18 }}>
                  <div>
                    <p style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt, marginBottom: 2 }}>Expedientes recientes</p>
                    <p style={{ fontSize: "0.68rem", color: T.txtFaint }}>{opsLoading ? "Cargando…" : `${cases.length} expediente${cases.length === 1 ? "" : "s"} en total`}</p>
                  </div>
                  <button onClick={() => setActiveNav("eval")} style={{ padding: "6px 14px", borderRadius: 8, border: "1px solid rgba(59,246,229,0.3)", background: "rgba(59,246,229,0.08)", color: T.cyan, fontSize: "0.7rem", fontWeight: 600, cursor: "pointer", fontFamily: "Poppins, sans-serif" }}>Ver todas</button>
                </div>
                {opsError && <p role="alert" style={{ fontSize: "0.75rem", color: "#fda4af", marginBottom: 10 }}>{opsError}</p>}
                {!opsLoading && recentCases.length === 0 && <p style={{ fontSize: "0.75rem", color: T.txtFaint }}>Sin expedientes registrados.</p>}
                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12 }}>
                  {recentCases.map((c) => {
                    const meta = caseStatusMeta(c.status);
                    return (
                      <div key={c.id} onClick={onCompanyProfile} style={{ padding: "14px 16px", borderRadius: 14, background: T.surface, borderLeft: `3px solid ${meta.color}`, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, cursor: onCompanyProfile ? "pointer" : "default" }}>
                        <p style={{ fontSize: "0.8rem", fontWeight: 700, color: T.txt, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{companyName(c.companyId)}</p>
                        <p style={{ fontSize: "0.65rem", color: T.txtMuted, marginTop: 2 }}>CAS-{c.id} · {SOURCE_LABEL[c.sourceType] ?? c.sourceType}</p>
                        <span style={{ display: "inline-block", marginTop: 8, padding: "2px 8px", borderRadius: 99, background: `${meta.color}18`, border: `1px solid ${meta.color}40`, fontSize: "0.6rem", fontWeight: 700, color: meta.color }}>{meta.label}</span>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>
          </>)}

          {activeNav === "empresa" && (
            <CompanyProfile embedded onViewPDF={onRisk} onEditProfile={onCompanyForm} />
          )}

          {activeNav === "eval" && <EvaluacionesSection isMobile={isMobile} cases={cases} companies={companies} loading={opsLoading} error={opsError} />}
          {activeNav === "cal" && <CalendarioSection isMobile={isMobile} schedule={schedule} loading={opsLoading} error={opsError} />}
          {activeNav === "alert" && <AlertasSection isMobile={isMobile} alerts={alerts} complaints={complaints} companies={companies} loading={opsLoading} error={opsError} />}
          {activeNav === "rep" && <ReportesSection isMobile={isMobile} metrics={metrics} cases={cases} loading={opsLoading} />}
          {activeNav === "plant" && <TemplateAdmin accessToken={accessToken} />}
          {activeNav === "cfg" && <ConfiguracionSection isMobile={isMobile} onLogout={onBack} onUserValidation={onUserValidation} pendingUsers={pendingUsers} pendingLoading={pendingLoading} pendingError={pendingError} />}

        </div>
      </main>

      {isMobile && (
        <div style={{ position: "fixed", bottom: 0, left: 0, right: 0, zIndex: 60, background: "rgba(10,18,36,0.92)", backdropFilter: "blur(28px)", WebkitBackdropFilter: "blur(28px)", borderTop: "1px solid rgba(255,255,255,0.08)", height: 66, display: "flex", alignItems: "stretch" }}>
          {mobileTabItems.map(item => {
            const active = activeNav === item.id;
            const badge = item.id === "alert" && activeAlertCount > 0 ? activeAlertCount
              : item.id === "cfg" && pendingUsers.length > 0 ? pendingUsers.length : 0;
            return (
              <button key={item.id} onClick={() => setActiveNav(item.id)}
                style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 3, background: "none", border: "none", cursor: "pointer", position: "relative", borderTop: active ? `2px solid ${T.cyan}` : "2px solid transparent" }}>
                <div style={{ position: "relative" }}>
                  <span style={{ color: active ? T.cyan : "rgba(148,163,184,0.5)" }}>{item.icon(active ? T.cyan : "rgba(148,163,184,0.5)")}</span>
                  {badge > 0 && !active && (
                    <span style={{ position: "absolute", top: -4, right: -6, minWidth: 16, height: 16, borderRadius: 99, background: T.magenta, border: `2px solid #0a1224`, display: "flex", alignItems: "center", justifyContent: "center", fontSize: "0.5rem", fontWeight: 800, color: "#fff", padding: "0 3px" }}>{badge}</span>
                  )}
                </div>
                <span style={{ fontSize: "0.58rem", color: active ? T.cyan : "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif", fontWeight: active ? 700 : 400 }}>{item.label}</span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

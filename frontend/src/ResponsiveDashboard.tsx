import { useState, useEffect } from "react";
import CompanyProfile from "./CompanyProfile";
import { getDashboard } from "./features/operations/api";
import NotificationsBell from "./features/notifications/NotificationsBell";

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
type Priority = "Alta" | "Media" | "Baja";
type EvalStatus = "En Proceso" | "Completada" | "Pendiente" | "Rechazada";
type AlertSeverity = "Crítica" | "Advertencia" | "Informativa";

/* ── Data ────────────────────────────────────────────────── */
interface StatItem { label: string; value: number | string; pct: number; accent: string; trend: string }

const rings = [
  { label: "Evaluaciones", pct: 85, color: T.cyan,    r: 88 },
  { label: "Informes",     pct: 62, color: T.magenta, r: 66 },
  { label: "Cumplimiento", pct: 78, color: T.yellow,  r: 44 },
];

const tasks: { id: number; name: string; company: string; pct: number; priority: Priority; due: string; initials: string }[] = [
  { id: 1, name: "Inspección Seguridad LAPCH",  company: "Torres S.A.",        pct: 80, priority: "Alta",  due: "Hoy",    initials: "JL" },
  { id: 2, name: "Informe Ambiental",           company: "Distribuidora Norte", pct: 45, priority: "Media", due: "29 ago", initials: "MP" },
  { id: 3, name: "Evaluación Eléctrica",        company: "Industrias Montoya",  pct: 90, priority: "Baja",  due: "30 ago", initials: "CR" },
  { id: 4, name: "Auditoría LAPCH Completa",    company: "Montoya & Cía",       pct: 20, priority: "Alta",  due: "Hoy",    initials: "AL" },
  { id: 5, name: "Revisión Normativa ISO",      company: "Comercial Ángel",     pct: 65, priority: "Media", due: "31 ago", initials: "FS" },
  { id: 6, name: "Inspección Física Bodegas",   company: "Ferrería del Sur",    pct: 35, priority: "Alta",  due: "28 ago", initials: "RG" },
];

const EVALUACIONES: { id: string; empresa: string; tipo: string; tecnico: string; initials: string; fecha: string; status: EvalStatus; pct: number; zona: string }[] = [
  { id: "EVL-001", empresa: "Torres S.A.",          tipo: "LAPCH Seguridad",     tecnico: "J. López",      initials: "JL", fecha: "28 ago 2026", status: "En Proceso",  pct: 80, zona: "Norte" },
  { id: "EVL-002", empresa: "Distribuidora Norte",  tipo: "Ambiental",           tecnico: "M. Pérez",      initials: "MP", fecha: "29 ago 2026", status: "En Proceso",  pct: 45, zona: "Central" },
  { id: "EVL-003", empresa: "Industrias Montoya",   tipo: "Eléctrica",           tecnico: "C. Rodríguez",  initials: "CR", fecha: "30 ago 2026", status: "Completada",  pct: 100, zona: "Sur"   },
  { id: "EVL-004", empresa: "Montoya & Cía",        tipo: "LAPCH Completa",      tecnico: "A. Luna",       initials: "AL", fecha: "28 ago 2026", status: "En Proceso",  pct: 20, zona: "Norte" },
  { id: "EVL-005", empresa: "Comercial Ángel",      tipo: "Normativa ISO",       tecnico: "F. Santos",     initials: "FS", fecha: "31 ago 2026", status: "Pendiente",   pct: 0,  zona: "Este"  },
  { id: "EVL-006", empresa: "Ferrería del Sur",     tipo: "Inspección Física",   tecnico: "R. García",     initials: "RG", fecha: "28 ago 2026", status: "En Proceso",  pct: 35, zona: "Sur"   },
  { id: "EVL-007", empresa: "Constructora Vega",    tipo: "Higiene Ocupacional", tecnico: "Sin asignar",   initials: "?",  fecha: "02 sep 2026", status: "Pendiente",   pct: 0,  zona: "Oeste" },
  { id: "EVL-008", empresa: "Logística Express",    tipo: "LAPCH Seguridad",     tecnico: "J. López",      initials: "JL", fecha: "15 ago 2026", status: "Completada",  pct: 100, zona: "Norte" },
  { id: "EVL-009", empresa: "Farmacéutica RD",      tipo: "Normativa ISO",       tecnico: "C. Rodríguez",  initials: "CR", fecha: "10 ago 2026", status: "Rechazada",   pct: 60, zona: "Central" },
];

const ALERTAS: { id: string; severity: AlertSeverity; titulo: string; empresa: string; descripcion: string; hora: string; atendida: boolean }[] = [
  { id: "ALT-001", severity: "Crítica",      titulo: "Incumplimiento LAPCH",        empresa: "Torres S.A.",        descripcion: "3 indicadores críticos sin subsanar en 72h. Requiere acción inmediata.", hora: "hace 2h",   atendida: false },
  { id: "ALT-002", severity: "Crítica",      titulo: "Evaluación vencida",          empresa: "Ferrería del Sur",   descripcion: "Periodo de evaluación venció sin completar el informe final.",            hora: "hace 5h",   atendida: false },
  { id: "ALT-003", severity: "Crítica",      titulo: "Denuncia sin atender",        empresa: "Industrias Montoya", descripcion: "Denuncia anónima recibida hace 48h sin respuesta del coordinador.",       hora: "hace 1d",   atendida: false },
  { id: "ALT-004", severity: "Advertencia", titulo: "Técnico sin actividad",        empresa: "—",                  descripcion: "Carlos Méndez lleva 5 días sin registrar actividad en el sistema.",       hora: "hace 3h",   atendida: false },
  { id: "ALT-005", severity: "Advertencia", titulo: "Informe próximo a vencer",     empresa: "Montoya & Cía",      descripcion: "El informe EVL-004 vence en 2 días. Avance actual: 20%.",                  hora: "hace 6h",   atendida: false },
  { id: "ALT-006", severity: "Advertencia", titulo: "Solicitud BPM sin asignar",   empresa: "Constructora Vega",  descripcion: "La solicitud lleva 4 días sin técnico asignado.",                          hora: "hace 4d",   atendida: false },
  { id: "ALT-007", severity: "Informativa", titulo: "Evaluación completada",        empresa: "Logística Express",  descripcion: "EVL-008 completada exitosamente. Informe listo para revisión.",             hora: "hace 1d",   atendida: true  },
  { id: "ALT-008", severity: "Informativa", titulo: "Nuevo usuario registrado",     empresa: "Farmacéutica RD",    descripcion: "Un nuevo usuario delegado fue registrado y está pendiente de validación.",  hora: "hace 2d",   atendida: true  },
];

const USUARIOS_SISTEMA = [
  { id: "USR-01", name: "María García",    email: "coordinador@evalia.com", role: "Coordinador",               status: "Activo",   lastLogin: "Hoy 09:14" },
  { id: "USR-02", name: "Carlos Méndez",   email: "tecnico@evalia.com",     role: "Técnico Evaluador",         status: "Inactivo", lastLogin: "hace 5 días" },
  { id: "USR-03", name: "Gerencia",        email: "gerencia@empresa.com",   role: "Admin de Empresa",          status: "Activo",   lastLogin: "Ayer 15:30" },
  { id: "USR-04", name: "Delegado",        email: "delegado@empresa.com",   role: "Usuario Delegado",          status: "Activo",   lastLogin: "hace 2 días" },
  { id: "USR-05", name: "Administrador",   email: "admin@evalia.com",       role: "Administrador del Sistema", status: "Activo",   lastLogin: "Hoy 08:00" },
];

const CALENDAR_EVENTS: { day: number; empresa: string; tipo: string; tecnico: string; hora: string; color: string }[] = [
  { day: 28, empresa: "Torres S.A.",        tipo: "LAPCH Seguridad",   tecnico: "J. López",     hora: "08:00", color: T.cyan    },
  { day: 28, empresa: "Ferrería del Sur",   tipo: "Inspección Física", tecnico: "R. García",    hora: "10:30", color: T.magenta },
  { day: 28, empresa: "Montoya & Cía",      tipo: "LAPCH Completa",    tecnico: "A. Luna",      hora: "14:00", color: T.yellow  },
  { day: 29, empresa: "Distribuidora Norte",tipo: "Ambiental",         tecnico: "M. Pérez",     hora: "09:00", color: T.cyan    },
  { day: 30, empresa: "Industrias Montoya", tipo: "Eléctrica",         tecnico: "C. Rodríguez", hora: "11:00", color: T.magenta },
  { day: 31, empresa: "Comercial Ángel",    tipo: "Normativa ISO",     tecnico: "F. Santos",    hora: "08:30", color: T.cyan    },
  { day:  2, empresa: "Constructora Vega",  tipo: "Higiene Ocup.",     tecnico: "Por asignar",  hora: "10:00", color: T.yellow  },
  { day:  4, empresa: "Logística Express",  tipo: "LAPCH Seguridad",   tecnico: "J. López",     hora: "13:00", color: T.cyan    },
];

/* ── Priority / Status configs ───────────────────────────── */
const PRI: Record<Priority, { bg: string; border: string; color: string }> = {
  Alta:  { bg: "rgba(229,59,246,0.13)", border: "rgba(229,59,246,0.4)", color: T.magenta },
  Media: { bg: "rgba(246,229,59,0.11)", border: "rgba(246,229,59,0.4)", color: T.yellow  },
  Baja:  { bg: "rgba(59,246,229,0.10)", border: "rgba(59,246,229,0.35)",color: T.cyan    },
};

const EVAL_STATUS: Record<EvalStatus, { bg: string; border: string; color: string; label: string }> = {
  "En Proceso":  { bg: "rgba(59,246,229,0.10)",  border: "rgba(59,246,229,0.35)",  color: T.cyan,    label: "En Proceso"  },
  "Completada":  { bg: "rgba(59,246,100,0.12)",  border: "rgba(59,246,100,0.35)",  color: "#4ade80", label: "Completada"  },
  "Pendiente":   { bg: "rgba(148,163,184,0.09)", border: "rgba(148,163,184,0.22)", color: "rgba(148,163,184,0.7)", label: "Pendiente" },
  "Rechazada":   { bg: "rgba(229,59,246,0.13)",  border: "rgba(229,59,246,0.4)",   color: T.magenta, label: "Rechazada"   },
};

const ALERT_SEV: Record<AlertSeverity, { bg: string; border: string; color: string; dot: string }> = {
  "Crítica":      { bg: "rgba(229,59,246,0.08)",  border: "rgba(229,59,246,0.3)",   color: T.magenta, dot: T.magenta },
  "Advertencia":  { bg: "rgba(246,229,59,0.07)",  border: "rgba(246,229,59,0.28)",  color: T.yellow,  dot: T.yellow  },
  "Informativa":  { bg: "rgba(59,246,229,0.06)",  border: "rgba(59,246,229,0.2)",   color: T.cyan,    dot: T.cyan    },
};

/* ── Nav items ───────────────────────────────────────────── */
const navItems = [
  { id: "dash",    label: "Dashboard",     icon: gridIcon     },
  { id: "empresa", label: "Mi Empresa",    icon: buildingIcon },
  { id: "eval",    label: "Evaluaciones",  icon: clipIcon     },
  { id: "cal",     label: "Calendario",    icon: calIcon      },
  { id: "alert",   label: "Alertas",       icon: warnIcon,  badge: 6 },
  { id: "rep",     label: "Reportes",      icon: chartIcon    },
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
function editIcon(c: string) {
  return <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}
function menuIcon(c: string) {
  return <svg width="20" height="20" viewBox="0 0 24 24" fill="none"><path d="M3 12h18M3 6h18M3 18h18" stroke={c} strokeWidth="2" strokeLinecap="round"/></svg>;
}
function closeIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M18 6L6 18M6 6l12 12" stroke={c} strokeWidth="2" strokeLinecap="round"/></svg>;
}
function userIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2M12 11a4 4 0 100-8 4 4 0 000 8z" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}
function shieldIcon(c: string) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}
function downloadIcon(c: string) {
  return <svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5 5 5-5M12 15V3" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}
function checkIcon(c: string) {
  return <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke={c} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>;
}
function trashIcon(c: string) {
  return <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><polyline points="3 6 5 6 21 6" stroke={c} strokeWidth="1.75" strokeLinecap="round"/><path d="M19 6l-1 14H6L5 6M10 11v6M14 11v6M9 6V4h6v2" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>;
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

function ProgressRings({ size = 240 }: { size?: number }) {
  const cx = size / 2, cy = size / 2;
  const scale = size / 220;
  const scaledRings = rings.map((r) => ({ ...r, r: r.r * scale, sw: 13 * scale }));
  const overall = 74;
  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 20 }}>
      <div style={{ position: "relative", width: size, height: size }}>
        <svg viewBox={`0 0 ${size} ${size}`} width={size} height={size}>
          <defs>
            {scaledRings.map((r) => (
              <filter key={r.label} id={`rg-${r.label}`}>
                <feGaussianBlur stdDeviation="2.5" result="b"/>
                <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
              </filter>
            ))}
            <radialGradient id="center-glow" cx="50%" cy="50%" r="50%">
              <stop offset="0%" stopColor={T.cyan} stopOpacity="0.08"/>
              <stop offset="100%" stopColor={T.cyan} stopOpacity="0"/>
            </radialGradient>
          </defs>
          <circle cx={cx} cy={cy} r={scaledRings[2].r - scaledRings[2].sw} fill="url(#center-glow)"/>
          {scaledRings.map((r) => {
            const C = 2 * Math.PI * r.r;
            const dash = (r.pct / 100) * C;
            return (
              <g key={r.label}>
                <circle cx={cx} cy={cy} r={r.r} fill="none" stroke="rgba(255,255,255,0.05)" strokeWidth={r.sw}/>
                <circle cx={cx} cy={cy} r={r.r} fill="none" stroke={r.color} strokeWidth={r.sw}
                  strokeDasharray={`${dash} ${C}`} strokeLinecap="round"
                  transform={`rotate(-90 ${cx} ${cy})`} filter={`url(#rg-${r.label})`}/>
              </g>
            );
          })}
          <text x={cx} y={cy - 8} textAnchor="middle" fill={T.txt} fontSize={size * 0.145} fontWeight="900" fontFamily="Poppins,sans-serif">{overall}%</text>
          <text x={cx} y={cy + 14} textAnchor="middle" fill={T.txtMuted} fontSize={size * 0.053} fontFamily="Poppins,sans-serif">Progreso General</text>
        </svg>
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "8px 20px", justifyContent: "center" }}>
        {rings.map((r) => (
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

function TaskCard({ task, onCompanyClick }: { task: typeof tasks[number]; onCompanyClick?: () => void }) {
  const [hovered, setHovered] = useState(false);
  const pri = PRI[task.priority];
  return (
    <div onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}
      style={{ padding: "16px 18px", borderRadius: 16, background: hovered ? T.surfaceHi : T.surface, borderTop: `1px solid ${hovered ? "rgba(59,246,229,0.18)" : T.border}`, borderRight: `1px solid ${hovered ? "rgba(59,246,229,0.18)" : T.border}`, borderBottom: `1px solid ${hovered ? "rgba(59,246,229,0.18)" : T.border}`, borderLeft: `3px solid ${pri.color}`, transition: "all 0.2s ease", display: "flex", flexDirection: "column", gap: 12, cursor: "default" }}>
      <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 8 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <p style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt, marginBottom: 3, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{task.name}</p>
          <p onClick={onCompanyClick} style={{ fontSize: "0.7rem", color: onCompanyClick ? T.cyan : T.txtMuted, fontWeight: onCompanyClick ? 500 : 300, cursor: onCompanyClick ? "pointer" : "default" }}>{task.company}</p>
        </div>
        <span style={{ padding: "3px 9px", borderRadius: 99, background: pri.bg, border: `1px solid ${pri.border}`, fontSize: "0.62rem", fontWeight: 700, color: pri.color, whiteSpace: "nowrap", flexShrink: 0 }}>{task.priority}</span>
      </div>
      <div>
        <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
          <span style={{ fontSize: "0.65rem", color: T.txtFaint }}>Progreso</span>
          <span style={{ fontSize: "0.65rem", fontWeight: 700, color: pri.color }}>{task.pct}%</span>
        </div>
        <div style={{ height: 4, borderRadius: 99, background: "rgba(255,255,255,0.06)" }}>
          <div style={{ height: "100%", width: `${task.pct}%`, borderRadius: 99, background: `linear-gradient(90deg, ${pri.color}80, ${pri.color})` }}/>
        </div>
      </div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <div style={{ width: 26, height: 26, borderRadius: "50%", background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5 }}>
            <div style={{ width: "100%", height: "100%", borderRadius: "50%", background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "0.55rem", fontWeight: 800, color: T.txt }}>{task.initials}</div>
          </div>
          <span style={{ fontSize: "0.65rem", color: T.txtFaint }}>Asignado</span>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
          <svg width="11" height="11" viewBox="0 0 24 24" fill="none"><rect x="3" y="5" width="18" height="16" rx="3" stroke={T.txtFaint} strokeWidth="1.75"/><path d="M8 3v4M16 3v4M3 10h18" stroke={T.txtFaint} strokeWidth="1.75" strokeLinecap="round"/></svg>
          <span style={{ fontSize: "0.65rem", color: task.due === "Hoy" ? T.magenta : T.txtFaint, fontWeight: task.due === "Hoy" ? 600 : 300 }}>{task.due}</span>
        </div>
      </div>
    </div>
  );
}

/* ── Section: Evaluaciones ───────────────────────────────── */
function EvaluacionesSection({ isMobile }: { isMobile: boolean }) {
  const [filter, setFilter] = useState<"Todas" | EvalStatus>("Todas");
  const chips: ("Todas" | EvalStatus)[] = ["Todas", "En Proceso", "Completada", "Pendiente", "Rechazada"];
  const evalStats = [
    { label: "Total",       value: EVALUACIONES.length,                              accent: T.cyan    },
    { label: "En Proceso",  value: EVALUACIONES.filter(e => e.status==="En Proceso").length,  accent: T.yellow  },
    { label: "Completadas", value: EVALUACIONES.filter(e => e.status==="Completada").length, accent: "#4ade80" },
    { label: "Pendientes",  value: EVALUACIONES.filter(e => e.status==="Pendiente").length,  accent: T.txtMuted as string },
  ];
  const visible = filter === "Todas" ? EVALUACIONES : EVALUACIONES.filter(e => e.status === filter);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Evaluaciones</h2>
      </div>

      {/* stat tiles */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(120px, 1fr))", gap: 12 }}>
        {evalStats.map(s => (
          <div key={s.label} style={{ padding: "18px 16px", borderRadius: 16, background: T.surface, borderTop: `2px solid ${s.accent}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, textAlign: "center" }}>
            <p style={{ fontSize: "1.7rem", fontWeight: 900, color: s.accent, lineHeight: 1, textShadow: `0 0 14px ${s.accent}44` }}>{s.value}</p>
            <p style={{ fontSize: "0.68rem", color: T.txtMuted, marginTop: 4 }}>{s.label}</p>
          </div>
        ))}
      </div>

      {/* filter chips */}
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        {chips.map(c => {
          const active = filter === c;
          const col = c === "Todas" ? T.cyan : c === "En Proceso" ? T.yellow : c === "Completada" ? "#4ade80" : c === "Rechazada" ? T.magenta : T.txtMuted as string;
          return (
            <button key={c} onClick={() => setFilter(c)} style={{ padding: "6px 14px", borderRadius: 99, border: `1px solid ${active ? col : T.border}`, background: active ? `${col}18` : "transparent", color: active ? col : T.txtMuted, fontSize: "0.72rem", fontWeight: active ? 700 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif", transition: "all 0.2s" }}>{c}</button>
          );
        })}
      </div>

      {/* evaluaciones list */}
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {visible.map(ev => {
          const st = EVAL_STATUS[ev.status];
          return (
            <div key={ev.id} style={{ padding: "16px 18px", borderRadius: 16, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${st.color}`, display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
              {/* initials */}
              <div style={{ width: 38, height: 38, borderRadius: 11, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5, flexShrink: 0 }}>
                <div style={{ width: "100%", height: "100%", borderRadius: 9, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "0.68rem", fontWeight: 800, color: T.txt }}>{ev.initials}</div>
              </div>
              {/* main info */}
              <div style={{ flex: 1, minWidth: 140 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 2, flexWrap: "wrap" }}>
                  <span style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt }}>{ev.empresa}</span>
                  <span style={{ fontSize: "0.62rem", color: T.txtFaint, padding: "2px 7px", borderRadius: 99, background: T.surface, border: `1px solid ${T.border}` }}>{ev.id}</span>
                </div>
                <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                  <span style={{ fontSize: "0.7rem", color: T.txtMuted }}>{ev.tipo}</span>
                  <span style={{ fontSize: "0.7rem", color: T.txtFaint }}>·</span>
                  <span style={{ fontSize: "0.7rem", color: T.txtMuted }}>{ev.tecnico}</span>
                  <span style={{ fontSize: "0.7rem", color: T.txtFaint }}>·</span>
                  <span style={{ fontSize: "0.7rem", color: T.txtFaint }}>{ev.zona}</span>
                </div>
              </div>
              {/* progress */}
              {ev.status === "En Proceso" && (
                <div style={{ minWidth: 80, maxWidth: 100 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                    <span style={{ fontSize: "0.62rem", color: T.txtFaint }}>Avance</span>
                    <span style={{ fontSize: "0.62rem", fontWeight: 700, color: st.color }}>{ev.pct}%</span>
                  </div>
                  <div style={{ height: 4, borderRadius: 99, background: "rgba(255,255,255,0.06)" }}>
                    <div style={{ height: "100%", width: `${ev.pct}%`, borderRadius: 99, background: `linear-gradient(90deg, ${st.color}70, ${st.color})` }}/>
                  </div>
                </div>
              )}
              {/* status + date */}
              <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4, flexShrink: 0 }}>
                <span style={{ padding: "3px 9px", borderRadius: 99, background: st.bg, border: `1px solid ${st.border}`, fontSize: "0.62rem", fontWeight: 700, color: st.color }}>{st.label}</span>
                <span style={{ fontSize: "0.65rem", color: T.txtFaint }}>{ev.fecha}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ── Section: Calendario ─────────────────────────────────── */
function CalendarioSection({ isMobile }: { isMobile: boolean }) {
  const [selectedDay, setSelectedDay] = useState(28);
  const daysOfWeek = ["L", "M", "X", "J", "V", "S", "D"];
  /* Sep 2026 starts on Tuesday, offset=1 */
  const totalDays = 30;
  const startOffset = 1;
  const cells: (number | null)[] = [...Array(startOffset).fill(null), ...Array.from({ length: totalDays }, (_, i) => i + 1)];
  const eventDays = new Set(CALENDAR_EVENTS.map(e => e.day));
  const dayEvents = CALENDAR_EVENTS.filter(e => e.day === selectedDay);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Calendario de Evaluaciones</h2>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: isMobile ? "1fr" : "1fr 1fr", gap: 20, alignItems: "start" }}>
        {/* Calendar grid */}
        <div style={{ padding: "22px", borderRadius: 20, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 18 }}>
            <p style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt }}>Septiembre 2026</p>
            <div style={{ display: "flex", gap: 6 }}>
              <button style={{ width: 28, height: 28, borderRadius: 8, border: `1px solid ${T.border}`, background: T.surface, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}>
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><path d="M15 18l-6-6 6-6" stroke={T.txtMuted} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
              </button>
              <button style={{ width: 28, height: 28, borderRadius: 8, border: `1px solid ${T.border}`, background: T.surface, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}>
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><path d="M9 18l6-6-6-6" stroke={T.txtMuted} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
              </button>
            </div>
          </div>
          {/* day headers */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 4, marginBottom: 8 }}>
            {daysOfWeek.map(d => <div key={d} style={{ textAlign: "center", fontSize: "0.62rem", fontWeight: 700, color: T.txtFaint, padding: "4px 0" }}>{d}</div>)}
          </div>
          {/* day cells */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 4 }}>
            {cells.map((day, i) => {
              if (!day) return <div key={`e-${i}`}/>;
              const hasEvent = eventDays.has(day);
              const isSelected = day === selectedDay;
              const isToday = day === 2;
              return (
                <button key={day} onClick={() => setSelectedDay(day)}
                  style={{ position: "relative", aspectRatio: "1", borderRadius: 10, border: isSelected ? `1px solid ${T.cyan}` : isToday ? `1px solid rgba(59,246,229,0.3)` : "1px solid transparent", background: isSelected ? "rgba(59,246,229,0.15)" : isToday ? "rgba(59,246,229,0.07)" : "transparent", color: isSelected ? T.cyan : T.txt, fontSize: "0.78rem", fontWeight: isSelected ? 700 : isToday ? 600 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif", display: "flex", alignItems: "center", justifyContent: "center", transition: "all 0.15s" }}>
                  {day}
                  {hasEvent && <span style={{ position: "absolute", bottom: 3, left: "50%", transform: "translateX(-50%)", width: 4, height: 4, borderRadius: "50%", background: isSelected ? T.cyan : T.magenta }}/>}
                </button>
              );
            })}
          </div>
        </div>

        {/* Events for selected day */}
        <div style={{ padding: "22px", borderRadius: 20, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
          <p style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt, marginBottom: 4 }}>
            {selectedDay} de septiembre
          </p>
          <p style={{ fontSize: "0.68rem", color: T.txtFaint, marginBottom: 18 }}>
            {dayEvents.length === 0 ? "Sin evaluaciones programadas" : `${dayEvents.length} evaluación${dayEvents.length > 1 ? "es" : ""} programada${dayEvents.length > 1 ? "s" : ""}`}
          </p>
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {dayEvents.length === 0 ? (
              <div style={{ padding: "32px 20px", textAlign: "center", borderRadius: 14, border: `1px dashed ${T.border}` }}>
                <p style={{ fontSize: "0.78rem", color: T.txtFaint }}>Día sin actividad programada</p>
              </div>
            ) : dayEvents.map((ev, i) => (
              <div key={i} style={{ padding: "14px 16px", borderRadius: 14, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${ev.color}` }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6 }}>
                  <span style={{ fontSize: "0.8rem", fontWeight: 700, color: T.txt }}>{ev.empresa}</span>
                  <span style={{ fontSize: "0.68rem", fontWeight: 700, color: ev.color }}>{ev.hora}</span>
                </div>
                <p style={{ fontSize: "0.7rem", color: T.txtMuted, marginBottom: 2 }}>{ev.tipo}</p>
                <p style={{ fontSize: "0.65rem", color: T.txtFaint }}>{ev.tecnico}</p>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Upcoming list */}
      <div style={{ padding: "22px", borderRadius: 20, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
        <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt, marginBottom: 16 }}>Próximas evaluaciones</p>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 10 }}>
          {CALENDAR_EVENTS.slice(0, 6).map((ev, i) => (
            <div key={i} style={{ padding: "12px 14px", borderRadius: 12, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${ev.color}`, display: "flex", alignItems: "center", gap: 12 }}>
              <div style={{ width: 36, height: 36, borderRadius: 10, background: `${ev.color}18`, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                <span style={{ fontSize: "0.9rem", fontWeight: 800, color: ev.color, lineHeight: 1 }}>{ev.day}</span>
                <span style={{ fontSize: "0.52rem", color: ev.color, opacity: 0.7 }}>sep</span>
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <p style={{ fontSize: "0.78rem", fontWeight: 600, color: T.txt, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{ev.empresa}</p>
                <p style={{ fontSize: "0.65rem", color: T.txtMuted }}>{ev.tipo} · {ev.hora}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ── Section: Alertas ────────────────────────────────────── */
function AlertasSection({ isMobile }: { isMobile: boolean }) {
  const [alertas, setAlertas] = useState(ALERTAS);
  const [filter, setFilter] = useState<"Todas" | AlertSeverity | "Atendidas">("Todas");

  const atender = (id: string) => setAlertas(prev => prev.map(a => a.id === id ? { ...a, atendida: true } : a));
  const desestimar = (id: string) => setAlertas(prev => prev.filter(a => a.id !== id));

  const visible = filter === "Todas" ? alertas.filter(a => !a.atendida)
    : filter === "Atendidas" ? alertas.filter(a => a.atendida)
    : alertas.filter(a => a.severity === filter && !a.atendida);

  const countBySeverity = (s: AlertSeverity) => alertas.filter(a => a.severity === s && !a.atendida).length;

  const sevChips: ("Todas" | AlertSeverity | "Atendidas")[] = ["Todas", "Crítica", "Advertencia", "Informativa", "Atendidas"];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Alertas del Sistema</h2>
      </div>

      {/* severity stats */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(120px, 1fr))", gap: 12 }}>
        {(["Crítica", "Advertencia", "Informativa"] as AlertSeverity[]).map(s => {
          const cfg = ALERT_SEV[s];
          return (
            <div key={s} style={{ padding: "18px 16px", borderRadius: 16, background: T.surface, borderTop: `2px solid ${cfg.color}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, textAlign: "center" }}>
              <p style={{ fontSize: "1.7rem", fontWeight: 900, color: cfg.color, lineHeight: 1, textShadow: `0 0 14px ${cfg.color}44` }}>{countBySeverity(s)}</p>
              <p style={{ fontSize: "0.68rem", color: T.txtMuted, marginTop: 4 }}>{s}s</p>
            </div>
          );
        })}
        <div style={{ padding: "18px 16px", borderRadius: 16, background: T.surface, borderTop: `2px solid #4ade80`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, textAlign: "center" }}>
          <p style={{ fontSize: "1.7rem", fontWeight: 900, color: "#4ade80", lineHeight: 1 }}>{alertas.filter(a => a.atendida).length}</p>
          <p style={{ fontSize: "0.68rem", color: T.txtMuted, marginTop: 4 }}>Atendidas</p>
        </div>
      </div>

      {/* filter chips */}
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        {sevChips.map(c => {
          const active = filter === c;
          const col = c === "Crítica" ? T.magenta : c === "Advertencia" ? T.yellow : c === "Informativa" ? T.cyan : c === "Atendidas" ? "#4ade80" : T.txtMuted as string;
          return (
            <button key={c} onClick={() => setFilter(c)} style={{ padding: "6px 14px", borderRadius: 99, border: `1px solid ${active ? col : T.border}`, background: active ? `${col}18` : "transparent", color: active ? col : T.txtMuted, fontSize: "0.72rem", fontWeight: active ? 700 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif", transition: "all 0.2s" }}>{c}</button>
          );
        })}
      </div>

      {/* alerts list */}
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {visible.length === 0 && (
          <div style={{ padding: "40px 20px", textAlign: "center", borderRadius: 16, border: `1px dashed ${T.border}` }}>
            <p style={{ fontSize: "0.82rem", color: T.txtFaint }}>No hay alertas en esta categoría</p>
          </div>
        )}
        {visible.map(alerta => {
          const cfg = ALERT_SEV[alerta.severity];
          return (
            <div key={alerta.id} style={{ padding: "16px 18px", borderRadius: 16, background: cfg.bg, borderTop: `1px solid ${cfg.border}`, borderRight: `1px solid ${cfg.border}`, borderBottom: `1px solid ${cfg.border}`, borderLeft: `4px solid ${cfg.color}`, display: "flex", gap: 14, flexWrap: "wrap", alignItems: "flex-start", opacity: alerta.atendida ? 0.55 : 1 }}>
              <div style={{ width: 36, height: 36, borderRadius: 10, background: `${cfg.color}18`, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0, marginTop: 2 }}>
                {warnIcon(cfg.color)}
              </div>
              <div style={{ flex: 1, minWidth: 200 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4, flexWrap: "wrap" }}>
                  <span style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt }}>{alerta.titulo}</span>
                  <span style={{ padding: "2px 8px", borderRadius: 99, background: `${cfg.color}20`, border: `1px solid ${cfg.border}`, fontSize: "0.6rem", fontWeight: 700, color: cfg.color }}>{alerta.severity}</span>
                  {alerta.atendida && <span style={{ padding: "2px 8px", borderRadius: 99, background: "rgba(74,222,128,0.12)", border: "1px solid rgba(74,222,128,0.3)", fontSize: "0.6rem", fontWeight: 700, color: "#4ade80" }}>Atendida</span>}
                </div>
                <p style={{ fontSize: "0.7rem", color: T.txtMuted, marginBottom: 4 }}><span style={{ color: T.cyan, fontWeight: 600 }}>{alerta.empresa}</span> · {alerta.hora}</p>
                <p style={{ fontSize: "0.72rem", color: T.txtMuted, lineHeight: 1.5 }}>{alerta.descripcion}</p>
              </div>
              {!alerta.atendida && (
                <div style={{ display: "flex", gap: 8, flexShrink: 0, marginTop: 2 }}>
                  <button onClick={() => atender(alerta.id)} title="Marcar atendida" style={{ width: 32, height: 32, borderRadius: 9, border: "1px solid rgba(74,222,128,0.35)", background: "rgba(74,222,128,0.1)", display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", transition: "all 0.15s" }}>
                    {checkIcon("#4ade80")}
                  </button>
                  <button onClick={() => desestimar(alerta.id)} title="Desestimar" style={{ width: 32, height: 32, borderRadius: 9, border: `1px solid ${T.border}`, background: T.surface, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", transition: "all 0.15s" }}>
                    {trashIcon(T.txtMuted)}
                  </button>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ── Section: Reportes ───────────────────────────────────── */
function ReportesSection({ isMobile }: { isMobile: boolean }) {
  const kpis = [
    { label: "Evaluaciones este mes",   value: "47",  sub: "+12% vs mes ant.", accent: T.cyan    },
    { label: "Tasa de cumplimiento",    value: "83%", sub: "Meta: 85%",        accent: T.yellow  },
    { label: "Tiempo prom. evaluación", value: "4.2d",sub: "Objetivo: <5 días",accent: T.magenta },
    { label: "Técnicos activos",        value: "8",   sub: "de 10 disponibles",accent: "#4ade80" },
  ];

  const barData = [
    { mes: "Mar", val: 28 }, { mes: "Abr", val: 34 }, { mes: "May", val: 31 },
    { mes: "Jun", val: 40 }, { mes: "Jul", val: 38 }, { mes: "Ago", val: 47 },
  ];
  const maxVal = Math.max(...barData.map(b => b.val));

  const reportes = [
    { titulo: "Reporte Mensual — Agosto 2026",        tipo: "PDF",  size: "2.1 MB", fecha: "01 sep 2026", color: T.cyan    },
    { titulo: "Informe de Cumplimiento Q3 2026",      tipo: "PDF",  size: "3.4 MB", fecha: "28 ago 2026", color: T.magenta },
    { titulo: "Análisis de Riesgo — Planta Norte",    tipo: "XLSX", size: "890 KB", fecha: "25 ago 2026", color: T.yellow  },
    { titulo: "Evaluaciones por Zona — Sep 2026",     tipo: "PDF",  size: "1.6 MB", fecha: "02 sep 2026", color: T.cyan    },
    { titulo: "Resumen Ejecutivo — Agosto 2026",      tipo: "PDF",  size: "550 KB", fecha: "31 ago 2026", color: "#4ade80" },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Reportes y Analítica</h2>
      </div>

      {/* KPIs */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))", gap: 12 }}>
        {kpis.map(k => (
          <div key={k.label} style={{ padding: "20px 16px", borderRadius: 16, background: T.surface, borderTop: `2px solid ${k.accent}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, position: "relative", overflow: "hidden" }}>
            <div style={{ position: "absolute", top: -16, right: -16, width: 60, height: 60, borderRadius: "50%", background: `radial-gradient(ellipse, ${k.accent}18 0%, transparent 70%)`, pointerEvents: "none" }}/>
            <p style={{ fontSize: "1.9rem", fontWeight: 900, color: k.accent, lineHeight: 1, marginBottom: 4, textShadow: `0 0 14px ${k.accent}44` }}>{k.value}</p>
            <p style={{ fontSize: "0.75rem", fontWeight: 600, color: T.txt, marginBottom: 3 }}>{k.label}</p>
            <p style={{ fontSize: "0.64rem", color: T.txtMuted }}>{k.sub}</p>
          </div>
        ))}
      </div>

      {/* Bar chart + report list */}
      <div style={{ display: "grid", gridTemplateColumns: isMobile ? "1fr" : "1fr 1fr", gap: 20, alignItems: "start" }}>
        {/* Bar chart */}
        <div style={{ padding: "22px", borderRadius: 20, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
          <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt, marginBottom: 4 }}>Evaluaciones por mes</p>
          <p style={{ fontSize: "0.68rem", color: T.txtFaint, marginBottom: 20 }}>Últimos 6 meses</p>
          <div style={{ display: "flex", alignItems: "flex-end", gap: 8, height: 140 }}>
            {barData.map(b => {
              const pct = b.val / maxVal;
              const isLast = b.mes === "Ago";
              return (
                <div key={b.mes} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", gap: 6 }}>
                  <span style={{ fontSize: "0.62rem", fontWeight: 700, color: isLast ? T.cyan : T.txtMuted }}>{b.val}</span>
                  <div style={{ width: "100%", height: `${pct * 110}px`, borderRadius: "6px 6px 3px 3px", background: isLast ? `linear-gradient(180deg, ${T.cyan}, ${T.cyan}70)` : "rgba(255,255,255,0.1)", boxShadow: isLast ? `0 0 12px ${T.cyan}44` : "none", transition: "all 0.3s", minHeight: 4 }}/>
                  <span style={{ fontSize: "0.62rem", color: isLast ? T.cyan : T.txtFaint, fontWeight: isLast ? 700 : 400 }}>{b.mes}</span>
                </div>
              );
            })}
          </div>
        </div>

        {/* Zona breakdown */}
        <div style={{ padding: "22px", borderRadius: 20, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
          <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt, marginBottom: 4 }}>Distribución por zona</p>
          <p style={{ fontSize: "0.68rem", color: T.txtFaint, marginBottom: 20 }}>Evaluaciones activas</p>
          <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            {[{ zona: "Norte", val: 14, color: T.cyan }, { zona: "Sur", val: 10, color: T.magenta }, { zona: "Central", val: 9, color: T.yellow }, { zona: "Este", val: 6, color: "#4ade80" }, { zona: "Oeste", val: 8, color: "rgba(148,163,184,0.6)" }].map(z => (
              <div key={z.zona}>
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
                  <span style={{ fontSize: "0.75rem", color: T.txt }}>{z.zona}</span>
                  <span style={{ fontSize: "0.75rem", fontWeight: 700, color: z.color }}>{z.val} eval.</span>
                </div>
                <div style={{ height: 6, borderRadius: 99, background: "rgba(255,255,255,0.06)" }}>
                  <div style={{ height: "100%", width: `${(z.val / 47) * 100}%`, borderRadius: 99, background: `linear-gradient(90deg, ${z.color}80, ${z.color})`, boxShadow: `0 0 6px ${z.color}50` }}/>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Documents */}
      <div style={{ padding: "22px", borderRadius: 20, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}` }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 16 }}>
          <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt }}>Informes generados</p>
          <button style={{ padding: "6px 14px", borderRadius: 8, border: `1px solid rgba(59,246,229,0.3)`, background: "rgba(59,246,229,0.08)", color: T.cyan, fontSize: "0.7rem", fontWeight: 600, cursor: "pointer", fontFamily: "Poppins, sans-serif" }}>
            Nuevo informe
          </button>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {reportes.map((r, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", gap: 12, padding: "12px 14px", borderRadius: 12, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${r.color}` }}>
              <div style={{ width: 34, height: 34, borderRadius: 9, background: `${r.color}18`, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                <span style={{ fontSize: "0.58rem", fontWeight: 800, color: r.color }}>{r.tipo}</span>
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <p style={{ fontSize: "0.78rem", fontWeight: 600, color: T.txt, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{r.titulo}</p>
                <p style={{ fontSize: "0.64rem", color: T.txtFaint }}>{r.fecha} · {r.size}</p>
              </div>
              <button style={{ width: 30, height: 30, borderRadius: 8, border: `1px solid ${T.border}`, background: T.surface, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", flexShrink: 0 }}>
                {downloadIcon(T.txtMuted)}
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ── Section: Configuración ──────────────────────────────── */
function ConfiguracionSection({ isMobile, onLogout, onUserValidation }: { isMobile: boolean; onLogout: () => void; onUserValidation?: () => void }) {
  const [cfgTab, setCfgTab] = useState<"usuarios" | "roles" | "notif" | "sistema">("usuarios");
  const tabs: { id: typeof cfgTab; label: string }[] = [
    { id: "usuarios", label: "Usuarios" },
    { id: "roles",    label: "Roles y Permisos" },
    { id: "notif",    label: "Notificaciones" },
    { id: "sistema",  label: "Sistema" },
  ];

  const roleColors: Record<string, string> = {
    "Administrador del Sistema": T.magenta,
    "Coordinador": T.cyan,
    "Técnico Evaluador": T.yellow,
    "Admin de Empresa": "#4ade80",
    "Usuario Delegado": "rgba(148,163,184,0.7)",
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Gestión del Sistema</p>
        <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Configuración</h2>
      </div>

      {/* sub-tabs */}
      <div style={{ display: "flex", gap: 6, flexWrap: "wrap", padding: "6px", borderRadius: 14, background: T.surface, border: `1px solid ${T.border}`, width: "fit-content" }}>
        {tabs.map(t => (
          <button key={t.id} onClick={() => setCfgTab(t.id)} style={{ padding: "8px 16px", borderRadius: 10, border: cfgTab === t.id ? `1px solid rgba(59,246,229,0.3)` : "1px solid transparent", background: cfgTab === t.id ? "rgba(59,246,229,0.12)" : "transparent", color: cfgTab === t.id ? T.cyan : T.txtMuted, fontSize: "0.78rem", fontWeight: cfgTab === t.id ? 700 : 400, cursor: "pointer", fontFamily: "Poppins, sans-serif", transition: "all 0.2s", whiteSpace: "nowrap" }}>{t.label}</button>
        ))}
      </div>

      {/* Usuarios tab */}
      {cfgTab === "usuarios" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 10 }}>
            <p style={{ fontSize: "0.82rem", color: T.txtMuted }}>{USUARIOS_SISTEMA.length} usuarios registrados en el sistema</p>
            <button onClick={onUserValidation} style={{ padding: "8px 16px", borderRadius: 10, border: "none", background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, color: "#fff", fontSize: "0.75rem", fontWeight: 700, cursor: "pointer", fontFamily: "Poppins, sans-serif" }}>
              Revisar solicitudes
            </button>
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {USUARIOS_SISTEMA.map(u => {
              const rc = roleColors[u.role] ?? T.cyan;
              const isActive = u.status === "Activo";
              return (
                <div key={u.id} style={{ padding: "16px 18px", borderRadius: 16, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${rc}`, display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
                  <div style={{ width: 40, height: 40, borderRadius: 11, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5, flexShrink: 0 }}>
                    <div style={{ width: "100%", height: "100%", borderRadius: 9, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 800, fontSize: "0.82rem", color: T.txt }}>{u.name[0]}</div>
                  </div>
                  <div style={{ flex: 1, minWidth: 160 }}>
                    <p style={{ fontSize: "0.84rem", fontWeight: 700, color: T.txt, marginBottom: 2 }}>{u.name}</p>
                    <p style={{ fontSize: "0.68rem", color: T.txtMuted }}>{u.email}</p>
                  </div>
                  <div style={{ minWidth: 120 }}>
                    <span style={{ padding: "3px 9px", borderRadius: 99, background: `${rc}15`, border: `1px solid ${rc}40`, fontSize: "0.62rem", fontWeight: 700, color: rc }}>{u.role}</span>
                  </div>
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <div style={{ width: 7, height: 7, borderRadius: "50%", background: isActive ? "#4ade80" : T.txtFaint, boxShadow: isActive ? "0 0 6px #4ade8080" : "none" }}/>
                    <span style={{ fontSize: "0.68rem", color: isActive ? "#4ade80" : T.txtFaint }}>{u.status}</span>
                  </div>
                  <div style={{ textAlign: "right", flexShrink: 0 }}>
                    <p style={{ fontSize: "0.62rem", color: T.txtFaint }}>Último acceso</p>
                    <p style={{ fontSize: "0.68rem", color: T.txtMuted }}>{u.lastLogin}</p>
                  </div>
                  <button style={{ width: 32, height: 32, borderRadius: 9, border: `1px solid ${T.border}`, background: T.surface, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", flexShrink: 0 }}>
                    {editIcon(T.txtMuted)}
                  </button>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Roles tab */}
      {cfgTab === "roles" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 14 }}>
          {[
            { rol: "Administrador del Sistema", permisos: ["Gestión completa de usuarios", "Configuración del sistema", "Acceso a todos los módulos", "Exportar reportes", "Ver panel coordinador"], color: T.magenta },
            { rol: "Coordinador",               permisos: ["Asignar técnicos", "Ver todas las evaluaciones", "Gestionar alertas", "Exportar reportes", "Ver calendario"], color: T.cyan },
            { rol: "Técnico Evaluador",         permisos: ["Ver casos asignados", "Registrar avance", "Subir evidencias", "Generar informes propios"], color: T.yellow },
            { rol: "Admin de Empresa",          permisos: ["Enviar solicitudes BPM", "Ver sus evaluaciones", "Gestionar perfil empresa", "Ver resultados propios"], color: "#4ade80" },
            { rol: "Usuario Delegado",          permisos: ["Ver solicitudes empresa", "Acceso de solo lectura", "Descargar informes propios"], color: "rgba(148,163,184,0.7)" },
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

      {/* Notificaciones tab */}
      {cfgTab === "notif" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {[
            { label: "Alertas críticas",          sub: "Recibir email al generarse una alerta crítica",  on: true,  color: T.magenta },
            { label: "Nuevas solicitudes BPM",    sub: "Notificar cuando empresa envía solicitud",        on: true,  color: T.cyan    },
            { label: "Asignaciones pendientes",   sub: "Recordatorio diario de casos sin técnico",        on: true,  color: T.yellow  },
            { label: "Informes completados",      sub: "Notificar cuando un técnico finaliza evaluación", on: false, color: "#4ade80" },
            { label: "Resumen semanal",           sub: "Reporte ejecutivo cada lunes 08:00",              on: true,  color: T.cyan    },
            { label: "Denuncias recibidas",       sub: "Alertar inmediatamente ante nueva denuncia",      on: true,  color: T.magenta },
          ].map((n, i) => (
            <div key={i} style={{ padding: "16px 18px", borderRadius: 14, background: T.surface, borderTop: `1px solid ${T.border}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `3px solid ${n.on ? n.color : T.border}`, display: "flex", alignItems: "center", gap: 14 }}>
              <div style={{ flex: 1 }}>
                <p style={{ fontSize: "0.82rem", fontWeight: 600, color: T.txt, marginBottom: 2 }}>{n.label}</p>
                <p style={{ fontSize: "0.68rem", color: T.txtMuted }}>{n.sub}</p>
              </div>
              <div style={{ width: 44, height: 24, borderRadius: 99, background: n.on ? `linear-gradient(90deg, ${n.color}80, ${n.color})` : "rgba(255,255,255,0.08)", border: `1px solid ${n.on ? n.color : T.border}`, position: "relative", cursor: "pointer", flexShrink: 0, boxShadow: n.on ? `0 0 8px ${n.color}50` : "none" }}>
                <div style={{ position: "absolute", top: 2, left: n.on ? 22 : 2, width: 18, height: 18, borderRadius: "50%", background: n.on ? "#fff" : T.txtFaint, transition: "left 0.2s" }}/>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Sistema tab */}
      {cfgTab === "sistema" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 14 }}>
          {[
            { titulo: "Versión del sistema",     valor: "Evalia BPM v2.4.1",       sub: "Actualizado el 1 sep 2026",    accent: T.cyan    },
            { titulo: "Base de datos",           valor: "PostgreSQL 15.3",         sub: "Último backup: hace 2h",       accent: "#4ade80" },
            { titulo: "Almacenamiento",          valor: "14.8 GB / 50 GB",         sub: "29% utilizado",                accent: T.yellow  },
            { titulo: "Sesiones activas",        valor: "3 usuarios en línea",     sub: "Pico hoy: 7 usuarios",         accent: T.cyan    },
            { titulo: "Solicitudes procesadas",  valor: "1,247 este mes",          sub: "Promedio: 41/día",             accent: T.magenta },
            { titulo: "Tiempo de actividad",     valor: "99.97% uptime",           sub: "Último incidente: hace 42 días",accent: "#4ade80" },
          ].map((s, i) => (
            <div key={i} style={{ padding: "20px", borderRadius: 16, background: T.surface, borderTop: `2px solid ${s.accent}`, borderRight: `1px solid ${T.border}`, borderBottom: `1px solid ${T.border}`, borderLeft: `1px solid ${T.border}`, position: "relative", overflow: "hidden" }}>
              <div style={{ position: "absolute", top: -12, right: -12, width: 50, height: 50, borderRadius: "50%", background: `radial-gradient(ellipse, ${s.accent}18 0%, transparent 70%)`, pointerEvents: "none" }}/>
              <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.1em", textTransform: "uppercase", marginBottom: 8 }}>{s.titulo}</p>
              <p style={{ fontSize: "1rem", fontWeight: 800, color: s.accent, marginBottom: 4 }}>{s.valor}</p>
              <p style={{ fontSize: "0.68rem", color: T.txtMuted }}>{s.sub}</p>
            </div>
          ))}
        </div>
      )}

      {/* Logout — always visible in Configuración */}
      <div style={{ marginTop: 8, padding: "20px", borderRadius: 18, background: "rgba(229,59,246,0.05)", borderTop: "1px solid rgba(229,59,246,0.15)", borderRight: "1px solid rgba(229,59,246,0.15)", borderBottom: "1px solid rgba(229,59,246,0.15)", borderLeft: "3px solid rgba(229,59,246,0.4)", display: "flex", alignItems: "center", justifyContent: "space-between", gap: 16, flexWrap: "wrap" }}>
        <div>
          <p style={{ fontSize: "0.85rem", fontWeight: 700, color: T.txt, marginBottom: 3 }}>Cerrar sesión</p>
          <p style={{ fontSize: "0.7rem", color: T.txtMuted }}>Salir del panel de administración del sistema</p>
        </div>
        <button onClick={onLogout} style={{ padding: "10px 22px", borderRadius: 12, border: "1px solid rgba(229,59,246,0.4)", background: "rgba(229,59,246,0.1)", color: T.magenta, fontSize: "0.78rem", fontWeight: 700, cursor: "pointer", fontFamily: "Poppins, sans-serif", display: "flex", alignItems: "center", gap: 8, transition: "all 0.2s", whiteSpace: "nowrap" }}>
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

  const [stats, setStats] = useState<StatItem[]>([
    { label: "Expedientes totales", value: "…", pct: 0, accent: T.cyan, trend: "Cargando" },
    { label: "En evaluación", value: "…", pct: 0, accent: T.magenta, trend: "Cargando" },
    { label: "Pendientes de asignación", value: "…", pct: 0, accent: T.yellow, trend: "Cargando" },
  ]);

  useEffect(() => {
    if (!accessToken) return;
    let active = true;
    void getDashboard(accessToken)
      .then(metrics => {
        if (!active) return;
        const total = metrics.totalCases || 1;
        const count = (status: string) => metrics.byStatus.find(s => s.status === status)?.count ?? 0;
        const inEval = count("IN_EVALUATION") + count("PENDING_REPORT") + count("IN_REVIEW");
        const pendingAssign = count("PENDING_ASSIGNMENT");
        setStats([
          { label: "Expedientes totales", value: metrics.totalCases, pct: 100, accent: T.cyan, trend: `${count("APPROVED") + count("CLOSED")} cerrados` },
          { label: "En evaluación", value: inEval, pct: Math.round((inEval / total) * 100), accent: T.magenta, trend: `${count("IN_EVALUATION")} en curso` },
          { label: "Pendientes de asignación", value: pendingAssign, pct: Math.round((pendingAssign / total) * 100), accent: T.yellow, trend: `${metrics.unreadNotifications} notificación(es)` },
        ]);
      })
      .catch(() => {
        if (!active) return;
        setStats(current => current.map(s => ({ ...s, value: "—", trend: "No disponible" })));
      });
    return () => { active = false; };
  }, [accessToken]);

  if (!user) return null;

  const SIDEBAR_W = sidebarExpanded ? 232 : 68;

  const glass = {
    background: "rgba(255,255,255,0.03)",
    backdropFilter: "blur(28px)",
    WebkitBackdropFilter: "blur(28px)",
  };
  const GB = `1px solid ${T.border}`;

  const SECTION_TITLE: Record<string, string> = {
    dash:    "Dashboard Admin",
    empresa: "Mi Empresa",
    eval:    "Evaluaciones",
    cal:     "Calendario",
    alert:   "Alertas",
    rep:     "Reportes",
    cfg:     "Configuración",
  };

  const NavBtn = ({ item }: { item: typeof navItems[number] }) => {
    const active = activeNav === item.id;
    return (
      <button onClick={() => setActiveNav(item.id)} title={!sidebarExpanded ? item.label : undefined}
        style={{ display: "flex", alignItems: "center", gap: 11, padding: sidebarExpanded ? "10px 12px" : "10px", borderRadius: 12, border: "none", cursor: "pointer", width: "100%", justifyContent: sidebarExpanded ? "flex-start" : "center", background: active ? "rgba(59,246,229,0.11)" : "transparent", color: active ? T.cyan : T.txtMuted, fontFamily: "Poppins, sans-serif", fontWeight: active ? 600 : 400, fontSize: "0.82rem", transition: "all 0.2s ease", boxShadow: active ? "inset 0 0 0 1px rgba(59,246,229,0.18)" : "none", position: "relative" }}>
        {active && <div style={{ position: "absolute", left: -12, top: "50%", transform: "translateY(-50%)", width: 3, height: 20, borderRadius: 99, background: T.cyan, boxShadow: `0 0 10px ${T.cyan}` }}/>}
        <span style={{ color: active ? T.cyan : "rgba(148,163,184,0.45)", flexShrink: 0 }}>{item.icon(active ? T.cyan : "rgba(148,163,184,0.45)")}</span>
        {sidebarExpanded && <span style={{ flex: 1, textAlign: "left" }}>{item.label}</span>}
        {sidebarExpanded && item.badge && (
          <span style={{ padding: "2px 7px", borderRadius: 99, background: "rgba(229,59,246,0.18)", border: "1px solid rgba(229,59,246,0.38)", fontSize: "0.6rem", fontWeight: 700, color: T.magenta }}>{item.badge}</span>
        )}
        {!sidebarExpanded && item.badge && (
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

  /* mobile tab items: Dashboard · Evaluaciones · Alertas(badge) · Reportes · Config */
  const mobileTabItems = [
    { id: "dash",  label: "Inicio",     icon: gridIcon    },
    { id: "eval",  label: "Eval.",      icon: clipIcon    },
    { id: "alert", label: "Alertas",    icon: warnIcon, badge: 6 },
    { id: "rep",   label: "Reportes",   icon: chartIcon   },
    { id: "cfg",   label: "Config.",    icon: gearIcon    },
  ];

  return (
    <div style={{ display: "flex", height: "100svh", background: T.bg, fontFamily: "Poppins, sans-serif", overflow: "hidden", position: "relative" }}>

      {/* Sidebar — desktop/tablet only */}
      <aside style={{ ...glass, borderTop: "none", borderBottom: "none", borderLeft: "none", borderRight: GB, width: SIDEBAR_W, flexShrink: 0, height: "100%", transition: "width 0.28s cubic-bezier(.4,0,.2,1)", overflow: "hidden", zIndex: 10, borderRadius: 0, display: isMobile ? "none" : "flex", flexDirection: "column" }}>
        <SidebarContent />
      </aside>

      {/* Main */}
      <main style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", minWidth: 0 }}>

        {/* Top bar */}
        <header style={{ ...glass, borderTop: "none", borderLeft: "none", borderRight: "none", borderBottom: GB, flexShrink: 0, display: "flex", alignItems: "center", justifyContent: "space-between", padding: "14px 24px", borderRadius: 0 }}>
          <div>
            <h1 style={{ fontSize: isMobile ? "1rem" : "1.1rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>{SECTION_TITLE[activeNav] ?? "Dashboard"}</h1>
            {!isMobile && <p style={{ fontSize: "0.68rem", color: T.txtFaint }}>Miércoles, 2 de septiembre de 2026</p>}
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
            <div style={{ width: 36, height: 36, borderRadius: 10, background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, padding: 1.5, cursor: "pointer" }}>
              <div style={{ width: "100%", height: "100%", borderRadius: 8, background: "#1e293b", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 800, fontSize: "0.8rem", color: T.txt }}>{(user.name?.[0] ?? "U").toUpperCase()}</div>
            </div>
            {isMobile && (
              <button onClick={onBack} title="Cerrar sesión" style={{ width: 36, height: 36, borderRadius: 10, background: T.surface, border: `1px solid ${T.border}`, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke={T.txtMuted} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
              </button>
            )}
          </div>
        </header>

        {/* Scrollable body */}
        <div key={activeNav} style={{ flex: 1, overflowY: "auto", padding: isMobile ? "16px 14px 84px" : "24px 28px 36px", display: "flex", flexDirection: "column", gap: 20 }} className="hide-scroll">

          {/* ── Dashboard home ── */}
          {activeNav === "dash" && (<>
            <div>
              <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>Vista general</p>
              <h2 style={{ fontSize: isMobile ? "1.3rem" : "1.6rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>
                Buen día, <span style={{ background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text" }}>{user.name ?? ""}</span> 👋
              </h2>
            </div>

            {/* quick access banners */}
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {onCompanyDashboard && (
                <button onClick={onCompanyDashboard} style={{ width: "100%", display: "flex", alignItems: "center", gap: 10, padding: "13px 18px", borderRadius: 14, border: "none", cursor: "pointer", background: "linear-gradient(135deg, rgba(14,42,42,0.9) 0%, rgba(26,14,42,0.9) 60%, rgba(42,14,26,0.9) 100%)", borderTop: "1px solid rgba(59,246,229,0.25)", borderRight: "1px solid rgba(229,59,246,0.18)", borderBottom: "1px solid rgba(229,59,246,0.18)", borderLeft: "3px solid rgba(59,246,229,0.6)", boxShadow: "0 4px 24px rgba(59,246,229,0.1)", transition: "all 0.2s ease" }}>
                  <div style={{ width: 36, height: 36, borderRadius: 10, background: "linear-gradient(135deg, rgba(59,246,229,0.2), rgba(229,59,246,0.2))", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>{buildingIcon(T.cyan)}</div>
                  <div style={{ textAlign: "left", flex: 1 }}>
                    <p style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt, marginBottom: 1 }}>Dashboard de Empresa</p>
                    <p style={{ fontSize: "0.62rem", color: T.txtMuted }}>Solicitudes BPM · Evaluaciones · Trámites</p>
                  </div>
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" style={{ flexShrink: 0 }}><path d="M9 18l6-6-6-6" stroke={T.cyan} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
                </button>
              )}
              {onCoordinatorDashboard && (
                <button onClick={onCoordinatorDashboard} style={{ width: "100%", display: "flex", alignItems: "center", gap: 10, padding: "13px 18px", borderRadius: 14, border: "none", cursor: "pointer", background: "linear-gradient(135deg, rgba(42,14,42,0.9) 0%, rgba(14,14,42,0.9) 60%, rgba(14,32,42,0.9) 100%)", borderTop: "1px solid rgba(229,59,246,0.3)", borderRight: "1px solid rgba(59,246,229,0.18)", borderBottom: "1px solid rgba(59,246,229,0.18)", borderLeft: "3px solid rgba(229,59,246,0.7)", boxShadow: "0 4px 24px rgba(229,59,246,0.1)", transition: "all 0.2s ease" }}>
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
              {onRisk && (
                <div onClick={onRisk} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, padding: "14px 18px", borderRadius: 16, background: "rgba(229,59,246,0.06)", borderTop: "1px solid rgba(229,59,246,0.22)", borderRight: "1px solid rgba(229,59,246,0.22)", borderBottom: "1px solid rgba(229,59,246,0.22)", borderLeft: "3px solid rgba(229,59,246,0.7)", cursor: "pointer", transition: "background 0.2s ease" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 12, minWidth: 0 }}>
                    <div style={{ width: 36, height: 36, borderRadius: 10, background: "rgba(229,59,246,0.15)", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>{warnIcon(T.magenta)}</div>
                    <div style={{ minWidth: 0 }}>
                      <p style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt, marginBottom: 2 }}>Motor de Riesgo · Planta Industrial Norte</p>
                      <p style={{ fontSize: "0.67rem", color: T.txtMuted }}>Expediente RF-16 · Puntaje 7.2/10 · <span style={{ color: T.magenta, fontWeight: 600 }}>RIESGO ALTO</span></p>
                    </div>
                  </div>
                  <div style={{ display: "flex", alignItems: "center", gap: 7, flexShrink: 0 }}>
                    <span style={{ padding: "4px 10px", borderRadius: 99, background: "rgba(229,59,246,0.14)", border: "1px solid rgba(229,59,246,0.38)", fontSize: "0.6rem", fontWeight: 700, color: T.magenta, letterSpacing: "0.04em" }}>VER ANÁLISIS</span>
                  </div>
                </div>
              )}
            </div>

            {/* Stats */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))", gap: 14 }}>
              {stats.map((s) => <StatCard key={s.label} s={s} />)}
            </div>

            {/* Progress + tasks */}
            <div style={{ display: "grid", gridTemplateColumns: isMobile ? "1fr" : isTablet ? "1fr" : "auto 1fr", gap: 20, alignItems: "start" }}>
              <div style={{ ...glass, borderTop: GB, borderRight: GB, borderBottom: GB, borderLeft: GB, borderRadius: 20, padding: "28px 24px", display: "flex", flexDirection: "column", alignItems: "center", gap: 4 }}>
                <p style={{ fontSize: "0.68rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.12em", textTransform: "uppercase", marginBottom: 16, alignSelf: "flex-start" }}>Progreso Global</p>
                <ProgressRings size={chartSize} />
                <div style={{ display: "flex", gap: 10, marginTop: 20, width: "100%" }}>
                  {[{ label: "Esta semana", val: "+18%", color: T.cyan }, { label: "Meta mensual", val: "74%", color: T.magenta }].map((m) => (
                    <div key={m.label} style={{ flex: 1, padding: "12px 14px", borderRadius: 12, background: T.surface, border: `1px solid ${T.border}`, textAlign: "center" }}>
                      <p style={{ fontSize: "1.1rem", fontWeight: 800, color: m.color, marginBottom: 3 }}>{m.val}</p>
                      <p style={{ fontSize: "0.65rem", color: T.txtFaint }}>{m.label}</p>
                    </div>
                  ))}
                </div>
              </div>
              <div style={{ ...glass, borderTop: GB, borderRight: GB, borderBottom: GB, borderLeft: GB, borderRadius: 20, padding: "22px 20px" }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 18 }}>
                  <div>
                    <p style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt, marginBottom: 2 }}>Tareas Pendientes</p>
                    <p style={{ fontSize: "0.68rem", color: T.txtFaint }}>{tasks.length} tareas activas</p>
                  </div>
                  <button onClick={() => setActiveNav("eval")} style={{ padding: "6px 14px", borderRadius: 8, border: "1px solid rgba(59,246,229,0.3)", background: "rgba(59,246,229,0.08)", color: T.cyan, fontSize: "0.7rem", fontWeight: 600, cursor: "pointer", fontFamily: "Poppins, sans-serif" }}>Ver todas</button>
                </div>
                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 12 }}>
                  {tasks.map((task) => <TaskCard key={task.id} task={task} onCompanyClick={onCompanyProfile} />)}
                </div>
              </div>
            </div>
          </>)}

          {/* ── Mi Empresa ── */}
          {activeNav === "empresa" && (
            <CompanyProfile embedded onViewPDF={onRisk} onEditProfile={onCompanyForm} />
          )}

          {/* ── Evaluaciones ── */}
          {activeNav === "eval" && <EvaluacionesSection isMobile={isMobile} />}

          {/* ── Calendario ── */}
          {activeNav === "cal" && <CalendarioSection isMobile={isMobile} />}

          {/* ── Alertas ── */}
          {activeNav === "alert" && <AlertasSection isMobile={isMobile} />}

          {/* ── Reportes ── */}
          {activeNav === "rep" && <ReportesSection isMobile={isMobile} />}

          {/* ── Configuración ── */}
          {activeNav === "cfg" && <ConfiguracionSection isMobile={isMobile} onLogout={onBack} onUserValidation={onUserValidation} />}

        </div>
      </main>

      {/* ── Mobile bottom tab bar ── */}
      {isMobile && (
        <div style={{ position: "fixed", bottom: 0, left: 0, right: 0, zIndex: 60, background: "rgba(10,18,36,0.92)", backdropFilter: "blur(28px)", WebkitBackdropFilter: "blur(28px)", borderTop: "1px solid rgba(255,255,255,0.08)", height: 66, display: "flex", alignItems: "stretch" }}>
          {mobileTabItems.map(item => {
            const active = activeNav === item.id;
            return (
              <button key={item.id} onClick={() => setActiveNav(item.id)}
                style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 3, background: "none", border: "none", cursor: "pointer", position: "relative", borderTop: active ? `2px solid ${T.cyan}` : "2px solid transparent", transition: "border-color 0.2s" }}>
                <div style={{ position: "relative" }}>
                  <span style={{ color: active ? T.cyan : "rgba(148,163,184,0.5)" }}>{item.icon(active ? T.cyan : "rgba(148,163,184,0.5)")}</span>
                  {item.badge && !active && (
                    <span style={{ position: "absolute", top: -4, right: -6, minWidth: 16, height: 16, borderRadius: 99, background: T.magenta, border: `2px solid #0a1224`, display: "flex", alignItems: "center", justifyContent: "center", fontSize: "0.5rem", fontWeight: 800, color: "#fff", padding: "0 3px" }}>{item.badge}</span>
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

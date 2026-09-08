import { useState, useEffect } from "react";

/* ── Responsive hook ─────────────────────────────────────────── */
function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

/* ── Types & sample data ─────────────────────────────────────── */
type SolStatus = "Pendiente" | "En Revisión" | "Aprobado" | "Rechazado";
type RiskLevel  = "Riesgo Bajo" | "Riesgo Moderado" | "Riesgo Alto" | "Riesgo Crítico";

interface Solicitud {
  id: string; nombre: string; tipo: string;
  fecha: string; status: SolStatus; progress: number;
}
interface Evaluacion {
  id: string; tipo: string; fecha: string;
  risk: RiskLevel; score: number; inspector: string;
}

const SOLICITUDES: Solicitud[] = [
  { id: "BPM-2026-0112", nombre: "Licencia de Operación",    tipo: "LAPCH",        fecha: "22 Ago 2026", status: "Pendiente",   progress: 20 },
  { id: "BPM-2026-0089", nombre: "Registro Sanitario",       tipo: "Salud",        fecha: "14 Ago 2026", status: "En Revisión", progress: 55 },
  { id: "BPM-2026-0061", nombre: "Certificación ISO 22000",  tipo: "Calidad",      fecha: "03 Jul 2026", status: "Aprobado",    progress: 100},
  { id: "BPM-2026-0044", nombre: "Actualización de Datos",   tipo: "Registro",     fecha: "18 Jun 2026", status: "Rechazado",   progress: 0  },
  { id: "BPM-2026-0031", nombre: "Permiso Ambiental",        tipo: "Ambiental",    fecha: "05 Jun 2026", status: "En Revisión", progress: 72 },
];

const EVALUACIONES: Evaluacion[] = [
  { id: "EVA-2025-0341", tipo: "Inspección General",      fecha: "15 Mar 2025", risk: "Riesgo Bajo",      score: 91, inspector: "Ing. M. Santos"    },
  { id: "EVA-2024-0887", tipo: "Auditoría de Procesos",   fecha: "12 Sep 2024", risk: "Riesgo Moderado",  score: 68, inspector: "Lic. A. Fernández" },
  { id: "EVA-2024-0312", tipo: "Inspección de Seguridad", fecha: "03 May 2024", risk: "Riesgo Alto",      score: 44, inspector: "Ing. R. Méndez"    },
];

/* ── Color maps ──────────────────────────────────────────────── */
const SOL_STYLE: Record<SolStatus, { color: string; bg: string; border: string; dot: string }> = {
  "Pendiente":   { color: "#F6E53B", bg: "rgba(246,229,59,0.11)",  border: "rgba(246,229,59,0.38)",  dot: "#F6E53B" },
  "En Revisión": { color: "#3BF6E5", bg: "rgba(59,246,229,0.10)",  border: "rgba(59,246,229,0.35)",  dot: "#3BF6E5" },
  "Aprobado":    { color: "#22c55e", bg: "rgba(34,197,94,0.11)",   border: "rgba(34,197,94,0.38)",   dot: "#22c55e" },
  "Rechazado":   { color: "#E53BF6", bg: "rgba(229,59,246,0.11)",  border: "rgba(229,59,246,0.38)",  dot: "#E53BF6" },
};

const RISK_STYLE: Record<RiskLevel, { color: string; bg: string; border: string }> = {
  "Riesgo Bajo":     { color: "#22c55e", bg: "rgba(34,197,94,0.11)",  border: "rgba(34,197,94,0.35)"  },
  "Riesgo Moderado": { color: "#f59e0b", bg: "rgba(245,158,11,0.11)", border: "rgba(245,158,11,0.35)" },
  "Riesgo Alto":     { color: "#E53BF6", bg: "rgba(229,59,246,0.11)", border: "rgba(229,59,246,0.35)" },
  "Riesgo Crítico":  { color: "#ef4444", bg: "rgba(239,68,68,0.11)",  border: "rgba(239,68,68,0.35)"  },
};

/* ── Icons ───────────────────────────────────────────────────── */
function BellIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
      <path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0"
        stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}
function MenuIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
      <path d="M3 12h18M3 6h18M3 18h12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
function CloseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
      <path d="M18 6L6 18M6 6l12 12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
function ArrowRightIcon({ color = "currentColor" }: { color?: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
      <path d="M9 18l6-6-6-6" stroke={color} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
function ShieldIcon({ color }: { color: string }) {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none">
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
function DocIcon({ color }: { color: string }) {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M14 2v6h6M16 13H8M16 17H8" stroke={color} strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}
function PlusIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
      <path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  );
}

/* ── Nav icons ───────────────────────────────────────────────── */
const NAV_ITEMS = [
  {
    id: "dash", label: "Dashboard", icon: (c: string) => (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="3" y="3" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/><rect x="14" y="3" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/><rect x="3" y="14" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/><rect x="14" y="14" width="7" height="7" rx="2" stroke={c} strokeWidth="1.75"/></svg>
    ),
  },
  {
    id: "empresa", label: "Mi Empresa", icon: (c: string) => (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M3 21h18M3 7l9-4 9 4M4 7v14M20 7v14M9 21v-4a3 3 0 016 0v4" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
    ),
  },
  {
    id: "sol", label: "Solicitudes BPM", icon: (c: string) => (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><rect x="8" y="2" width="8" height="4" rx="1" stroke={c} strokeWidth="1.5"/><path d="M8 3H6a2 2 0 00-2 2v14a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2h-2" stroke={c} strokeWidth="1.5"/><path d="M9 12h6M9 16h4" stroke={c} strokeWidth="1.5" strokeLinecap="round"/></svg>
    ),
  },
  {
    id: "eval", label: "Evaluaciones", icon: (c: string) => (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
    ),
  },
  {
    id: "notif", label: "Notificaciones", icon: (c: string) => (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg>
    ), badge: 3,
  },
  {
    id: "cfg", label: "Configuración", icon: (c: string) => (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="3" stroke={c} strokeWidth="1.75"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" stroke={c} strokeWidth="1.75"/></svg>
    ),
  },
];

/* ── Section header ──────────────────────────────────────────── */
function SectionHeader({ title, count, onSeeAll }: { title: string; count: number; onSeeAll?: () => void }) {
  return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
        <div style={{ width: 3, height: 18, borderRadius: 99, background: "linear-gradient(180deg, #3BF6E5, #E53BF6)" }} />
        <h3 style={{ fontSize: "0.95rem", fontWeight: 700, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", letterSpacing: "-0.01em" }}>
          {title}
        </h3>
        <span style={{
          padding: "2px 8px", borderRadius: 99, fontSize: "0.58rem", fontWeight: 700,
          background: "rgba(59,246,229,0.1)", color: "#3BF6E5",
          borderTop: "1px solid rgba(59,246,229,0.28)", borderRight: "1px solid rgba(59,246,229,0.28)",
          borderBottom: "1px solid rgba(59,246,229,0.28)", borderLeft: "1px solid rgba(59,246,229,0.28)",
          fontFamily: "Poppins, sans-serif",
        }}>
          {count}
        </span>
      </div>
      {onSeeAll && (
        <button onClick={onSeeAll} style={{ display: "flex", alignItems: "center", gap: 4, background: "none", border: "none", cursor: "pointer", color: "rgba(59,246,229,0.7)", fontSize: "0.68rem", fontWeight: 600, fontFamily: "Poppins, sans-serif" }}>
          Ver todas <ArrowRightIcon color="rgba(59,246,229,0.7)" />
        </button>
      )}
    </div>
  );
}

/* ── Solicitud card ──────────────────────────────────────────── */
function SolicitudCard({ sol }: { sol: Solicitud }) {
  const s = SOL_STYLE[sol.status];
  return (
    <div style={{
      borderRadius: 15, padding: "13px 14px",
      background: "rgba(12,20,40,0.75)",
      backdropFilter: "blur(20px)", WebkitBackdropFilter: "blur(20px)",
      borderTop:    "1px solid rgba(255,255,255,0.07)",
      borderRight:  "1px solid rgba(255,255,255,0.05)",
      borderBottom: "1px solid rgba(255,255,255,0.05)",
      borderLeft:   `3px solid ${s.color}77`,
      transition: "all 0.18s ease",
    }}>
      {/* Top row */}
      <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 8, marginBottom: 8 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 3 }}>
            <span style={{ fontSize: "0.56rem", fontFamily: "'Courier New', monospace", color: "rgba(148,163,184,0.38)", letterSpacing: "0.03em" }}>
              {sol.id}
            </span>
            <span style={{ width: 2, height: 2, borderRadius: "50%", background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />
            <span style={{ fontSize: "0.57rem", fontWeight: 600, color: "rgba(148,163,184,0.38)", fontFamily: "Poppins, sans-serif", textTransform: "uppercase", letterSpacing: "0.07em" }}>
              {sol.tipo}
            </span>
          </div>
          <p style={{ fontSize: "0.84rem", fontWeight: 700, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {sol.nombre}
          </p>
        </div>
        {/* Status chip */}
        <div style={{
          display: "inline-flex", alignItems: "center", gap: 5,
          padding: "4px 10px", borderRadius: 99, flexShrink: 0,
          background: s.bg, borderTop: `1px solid ${s.border}`,
          borderRight: `1px solid ${s.border}`, borderBottom: `1px solid ${s.border}`,
          borderLeft: `1px solid ${s.border}`,
        }}>
          <span style={{ width: 6, height: 6, borderRadius: "50%", background: s.dot, boxShadow: `0 0 5px ${s.dot}`, flexShrink: 0, display: "inline-block" }} />
          <span style={{ fontSize: "0.62rem", fontWeight: 700, color: s.color, fontFamily: "Poppins, sans-serif", whiteSpace: "nowrap" }}>
            {sol.status}
          </span>
        </div>
      </div>

      {/* Progress bar (only for in-progress) */}
      {sol.status !== "Aprobado" && sol.status !== "Rechazado" && (
        <div style={{ marginBottom: 8 }}>
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
            <span style={{ fontSize: "0.58rem", color: "rgba(148,163,184,0.38)", fontFamily: "Poppins, sans-serif" }}>Progreso de revisión</span>
            <span style={{ fontSize: "0.58rem", fontWeight: 600, color: s.color, fontFamily: "Poppins, sans-serif" }}>{sol.progress}%</span>
          </div>
          <div style={{ height: 3, borderRadius: 99, background: "rgba(255,255,255,0.06)" }}>
            <div style={{ height: "100%", width: `${sol.progress}%`, borderRadius: 99, background: `linear-gradient(90deg, ${s.color}66, ${s.color})`, transition: "width 0.5s ease" }} />
          </div>
        </div>
      )}

      {/* Footer row */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div style={{ display: "flex", alignItems: "center", gap: 5 }}>
          <svg width="10" height="10" viewBox="0 0 24 24" fill="none"><rect x="3" y="4" width="18" height="18" rx="2" stroke="rgba(148,163,184,0.3)" strokeWidth="1.75"/><path d="M16 2v4M8 2v4M3 10h18" stroke="rgba(148,163,184,0.3)" strokeWidth="1.75" strokeLinecap="round"/></svg>
          <span style={{ fontSize: "0.6rem", color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif" }}>{sol.fecha}</span>
        </div>
        <button style={{
          display: "flex", alignItems: "center", gap: 5,
          padding: "4px 10px", borderRadius: 8,
          background: "rgba(255,255,255,0.04)",
          borderTop: "1px solid rgba(255,255,255,0.08)", borderRight: "1px solid rgba(255,255,255,0.08)",
          borderBottom: "1px solid rgba(255,255,255,0.08)", borderLeft: "1px solid rgba(255,255,255,0.08)",
          color: "rgba(148,163,184,0.5)", fontSize: "0.6rem", fontWeight: 600,
          fontFamily: "Poppins, sans-serif", cursor: "pointer",
        }}>
          <DocIcon color="rgba(148,163,184,0.5)" />
          Detalle
        </button>
      </div>
    </div>
  );
}

/* ── Evaluacion card ─────────────────────────────────────────── */
function EvalCard({ ev, onViewPDF }: { ev: Evaluacion; onViewPDF?: () => void }) {
  const r = RISK_STYLE[ev.risk];
  const rLabel = ev.risk.replace("Riesgo ", "");

  /* Donut */
  const R = 18, cx = 22, cy = 22;
  const circ = 2 * Math.PI * R;
  const filled = (ev.score / 100) * circ;

  return (
    <div style={{
      borderRadius: 15, padding: "13px 14px",
      background: "rgba(12,20,40,0.75)",
      backdropFilter: "blur(20px)", WebkitBackdropFilter: "blur(20px)",
      borderTop:    "1px solid rgba(255,255,255,0.07)",
      borderRight:  "1px solid rgba(255,255,255,0.05)",
      borderBottom: "1px solid rgba(255,255,255,0.05)",
      borderLeft:   `3px solid ${r.color}77`,
      display: "flex", alignItems: "center", gap: 12,
    }}>
      {/* Score donut */}
      <div style={{ flexShrink: 0 }}>
        <svg width="44" height="44" viewBox="0 0 44 44">
          <defs>
            <filter id={`cdG-${ev.id}`} x="-40%" y="-40%" width="180%" height="180%">
              <feGaussianBlur in="SourceGraphic" stdDeviation="1.5" result="b" />
              <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
            </filter>
          </defs>
          <circle cx={cx} cy={cy} r={R} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="4.5" />
          <circle cx={cx} cy={cy} r={R} fill="none"
            stroke={r.color} strokeWidth="4.5"
            strokeDasharray={`${filled} ${circ}`}
            strokeLinecap="round"
            strokeDashoffset={circ * 0.25}
            filter={`url(#cdG-${ev.id})`}
          />
          <text x={cx} y={cy + 4} textAnchor="middle" fill={r.color}
            fontSize="10" fontWeight="800" fontFamily="Poppins, sans-serif">
            {ev.score}
          </text>
        </svg>
      </div>

      {/* Content */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <p style={{ fontSize: "0.82rem", fontWeight: 700, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", marginBottom: 5, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
          {ev.tipo}
        </p>
        <div style={{ display: "flex", alignItems: "center", gap: 7, flexWrap: "wrap" }}>
          <div style={{
            display: "inline-flex", alignItems: "center", gap: 4,
            padding: "3px 8px", borderRadius: 99,
            background: r.bg, borderTop: `1px solid ${r.border}`,
            borderRight: `1px solid ${r.border}`, borderBottom: `1px solid ${r.border}`,
            borderLeft: `1px solid ${r.border}`,
          }}>
            <ShieldIcon color={r.color} />
            <span style={{ fontSize: "0.6rem", fontWeight: 700, color: r.color, fontFamily: "Poppins, sans-serif" }}>
              {rLabel}
            </span>
          </div>
          <span style={{ fontSize: "0.58rem", color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif" }}>
            {ev.fecha}
          </span>
        </div>
      </div>

      {/* PDF button */}
      <button
        type="button"
        onClick={onViewPDF}
        style={{
          flexShrink: 0, display: "flex", flexDirection: "column", alignItems: "center", gap: 3,
          padding: "7px 9px", borderRadius: 10,
          background: "rgba(255,255,255,0.04)",
          borderTop:    "1px solid rgba(255,255,255,0.09)",
          borderRight:  "1px solid rgba(255,255,255,0.09)",
          borderBottom: "1px solid rgba(255,255,255,0.09)",
          borderLeft:   "1px solid rgba(255,255,255,0.09)",
          color: "rgba(148,163,184,0.55)", cursor: "pointer",
          transition: "all 0.18s ease",
        }}
        onMouseEnter={(e) => {
          const el = e.currentTarget as HTMLElement;
          el.style.background = "rgba(59,246,229,0.07)";
          el.style.borderTop = el.style.borderRight = el.style.borderBottom = el.style.borderLeft = "1px solid rgba(59,246,229,0.28)";
          el.style.color = "#3BF6E5";
        }}
        onMouseLeave={(e) => {
          const el = e.currentTarget as HTMLElement;
          el.style.background = "rgba(255,255,255,0.04)";
          el.style.borderTop = el.style.borderRight = el.style.borderBottom = el.style.borderLeft = "1px solid rgba(255,255,255,0.09)";
          el.style.color = "rgba(148,163,184,0.55)";
        }}
      >
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/><path d="M14 2v6h6M16 13H8M16 17H8" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round"/></svg>
        <span style={{ fontSize: "0.5rem", fontWeight: 700, letterSpacing: "0.05em", fontFamily: "Poppins, sans-serif" }}>PDF</span>
      </button>
    </div>
  );
}

/* ── Main component ──────────────────────────────────────────── */
export default function CompanyDashboard({
  onBack,
  onCompanyProfile,
  onCompanyForm,
  onViewPDF,
  userName = "Gerencia",
}: {
  onBack?: () => void;
  onCompanyProfile?: () => void;
  onCompanyForm?: () => void;
  onViewPDF?: () => void;
  userName?: string;
}) {
  const width    = useWidth();
  const isMobile = width < 768;

  const [menuOpen,       setMenuOpen]       = useState(false);
  const [activeNav,      setActiveNav]      = useState("dash");
  const [showAllSol,     setShowAllSol]     = useState(false);
  const [newSolToast,    setNewSolToast]    = useState(false);

  const visibleSol = showAllSol ? SOLICITUDES : SOLICITUDES.slice(0, 3);

  const pendientes  = SOLICITUDES.filter((s) => s.status === "Pendiente").length;
  const enRevision  = SOLICITUDES.filter((s) => s.status === "En Revisión").length;
  const aprobadas   = SOLICITUDES.filter((s) => s.status === "Aprobado").length;

  const handleNewSol = () => {
    setNewSolToast(true);
    setTimeout(() => setNewSolToast(false), 2800);
  };

  return (
    <>
      <style>{`
        @keyframes cdBannerShift {
          0%,100% { background-position: 0% 50%; }
          50%      { background-position: 100% 50%; }
        }
        @keyframes cdToastIn {
          from { opacity:0; transform: translateY(20px) scale(0.95); }
          to   { opacity:1; transform: translateY(0) scale(1); }
        }
        @keyframes cdToastOut {
          from { opacity:1; }
          to   { opacity:0; transform: translateY(12px); }
        }
        @keyframes cdSlideIn {
          from { transform: translateX(-100%); }
          to   { transform: translateX(0); }
        }
        @keyframes cdPulse {
          0%,100% { transform: scale(1); opacity: 1; }
          50%     { transform: scale(1.25); opacity: 0.65; }
        }
        .cd-menu-open { animation: cdSlideIn 0.26s cubic-bezier(.22,1,.36,1) both; }
        .hide-scroll::-webkit-scrollbar { display: none; }
        .hide-scroll { -ms-overflow-style: none; scrollbar-width: none; }
      `}</style>

      <div style={{ minHeight: "100svh", background: "#0F172A", position: "relative", fontFamily: "Poppins, sans-serif", overflowX: "hidden" }}>

        {/* Mesh orbs */}
        <div className="mesh-orb"            style={{ width: 500, height: 500, background: "rgba(59,246,229,0.28)",  top: "-150px", right: "-100px" }} />
        <div className="mesh-orb mesh-orb-2" style={{ width: 400, height: 400, background: "rgba(229,59,246,0.24)", bottom: "60px",  left:  "-90px"  }} />
        <div className="mesh-orb mesh-orb-3" style={{ width: 220, height: 220, background: "rgba(246,229,59,0.09)", top:  "50%",   left:  "52%"    }} />

        {/* Grain */}
        <div style={{ position: "absolute", inset: 0, opacity: 0.04, backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`, backgroundSize: "180px", pointerEvents: "none", zIndex: 0 }} />

        {/* ── Drawer backdrop ── */}
        {menuOpen && (
          <div
            onClick={() => setMenuOpen(false)}
            style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.65)", zIndex: 40, backdropFilter: "blur(3px)" }}
          />
        )}

        {/* ── Side drawer ── */}
        <aside
          className={menuOpen ? "cd-menu-open" : ""}
          style={{
            position: "fixed", top: 0, left: 0, height: "100%", width: 268,
            zIndex: 50, background: "rgba(8,14,28,0.97)",
            backdropFilter: "blur(40px)", WebkitBackdropFilter: "blur(40px)",
            borderTop:    "none",
            borderRight:  "1px solid rgba(255,255,255,0.09)",
            borderBottom: "none",
            borderLeft:   "none",
            transform: menuOpen ? "translateX(0)" : "translateX(-100%)",
            transition: "transform 0.28s cubic-bezier(.4,0,.2,1)",
            display: "flex", flexDirection: "column",
          }}
        >
          {/* Drawer header */}
          <div style={{ padding: "22px 20px 18px", borderBottom: "1px solid rgba(255,255,255,0.07)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <div style={{ width: 34, height: 34, borderRadius: 10, background: "linear-gradient(135deg, #E53BF6, #3BF6E5)", padding: 1.5 }}>
                <div style={{ width: "100%", height: "100%", borderRadius: 8, background: "#0F172A", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 800, fontSize: "0.82rem", color: "#f1f5f9" }}>
                  {userName[0].toUpperCase()}
                </div>
              </div>
              <div>
                <p style={{ fontSize: "0.85rem", fontWeight: 700, color: "#f1f5f9" }}>{userName}</p>
                <p style={{ fontSize: "0.58rem", color: "rgba(148,163,184,0.45)", letterSpacing: "0.06em", textTransform: "uppercase" }}>Admin de Empresa</p>
              </div>
            </div>
            <button onClick={() => setMenuOpen(false)} style={{ background: "none", border: "none", cursor: "pointer", color: "rgba(148,163,184,0.5)", display: "flex" }}>
              <CloseIcon />
            </button>
          </div>

          {/* Nav items */}
          <nav style={{ flex: 1, padding: "14px 14px", display: "flex", flexDirection: "column", gap: 3, overflowY: "auto" }}>
            <p style={{ fontSize: "0.56rem", fontWeight: 700, color: "rgba(148,163,184,0.28)", letterSpacing: "0.14em", textTransform: "uppercase", padding: "4px 6px 10px" }}>MENÚ PRINCIPAL</p>
            {NAV_ITEMS.map((item) => {
              const active = activeNav === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => {
                    setActiveNav(item.id);
                    setMenuOpen(false);
                    if (item.id === "empresa") onCompanyProfile?.();
                    if (item.id === "eval") onCompanyProfile?.();
                  }}
                  style={{
                    display: "flex", alignItems: "center", gap: 12,
                    padding: "11px 12px", borderRadius: 12,
                    border: "none", cursor: "pointer", width: "100%", textAlign: "left",
                    background: active ? "rgba(59,246,229,0.10)" : "transparent",
                    color: active ? "#3BF6E5" : "rgba(148,163,184,0.55)",
                    fontFamily: "Poppins, sans-serif", fontWeight: active ? 600 : 400,
                    fontSize: "0.82rem", transition: "all 0.18s ease",
                    boxShadow: active ? "inset 0 0 0 1px rgba(59,246,229,0.16)" : "none",
                    position: "relative",
                  }}
                >
                  {active && <div style={{ position: "absolute", left: -14, top: "50%", transform: "translateY(-50%)", width: 3, height: 18, borderRadius: 99, background: "#3BF6E5", boxShadow: "0 0 10px #3BF6E5" }} />}
                  <span>{item.icon(active ? "#3BF6E5" : "rgba(148,163,184,0.45)")}</span>
                  <span style={{ flex: 1 }}>{item.label}</span>
                  {item.badge && (
                    <span style={{ padding: "2px 7px", borderRadius: 99, background: "rgba(229,59,246,0.18)", borderTop: "1px solid rgba(229,59,246,0.38)", borderRight: "1px solid rgba(229,59,246,0.38)", borderBottom: "1px solid rgba(229,59,246,0.38)", borderLeft: "1px solid rgba(229,59,246,0.38)", fontSize: "0.6rem", fontWeight: 700, color: "#E53BF6" }}>
                      {item.badge}
                    </span>
                  )}
                </button>
              );
            })}
          </nav>

          {/* Drawer footer */}
          <div style={{ padding: "14px", borderTop: "1px solid rgba(255,255,255,0.06)" }}>
            <button
              onClick={onBack}
              style={{ display: "flex", alignItems: "center", gap: 10, width: "100%", padding: "10px 12px", borderRadius: 12, background: "rgba(229,59,246,0.06)", borderTop: "1px solid rgba(229,59,246,0.18)", borderRight: "1px solid rgba(229,59,246,0.18)", borderBottom: "1px solid rgba(229,59,246,0.18)", borderLeft: "1px solid rgba(229,59,246,0.18)", color: "#E53BF6", fontFamily: "Poppins, sans-serif", fontSize: "0.82rem", fontWeight: 600, cursor: "pointer", border: "none" }}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#E53BF6" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
              Cerrar Sesión
            </button>
          </div>
        </aside>

        {/* ── Main content ── */}
        <div
          className="hide-scroll"
          style={{ position: "relative", zIndex: 1, maxWidth: isMobile ? "100%" : 540, margin: "0 auto", overflowY: "auto" }}
        >
          {/* ══ HEADER ══ */}
          <header style={{
            position: "sticky", top: 0, zIndex: 20,
            display: "flex", alignItems: "center", justifyContent: "space-between",
            padding: isMobile ? "16px 16px 14px" : "20px 24px 16px",
            background: "rgba(10,16,30,0.88)",
            backdropFilter: "blur(24px)", WebkitBackdropFilter: "blur(24px)",
            borderBottom: "1px solid rgba(255,255,255,0.06)",
          }}>
            {/* Hamburger */}
            <button
              onClick={() => setMenuOpen(true)}
              style={{ width: 38, height: 38, borderRadius: 11, background: "rgba(255,255,255,0.06)", borderTop: "1px solid rgba(255,255,255,0.11)", borderRight: "1px solid rgba(255,255,255,0.11)", borderBottom: "1px solid rgba(255,255,255,0.11)", borderLeft: "1px solid rgba(255,255,255,0.11)", color: "rgba(148,163,184,0.7)", display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}
            >
              <MenuIcon />
            </button>

            {/* Greeting */}
            <div style={{ textAlign: "center" }}>
              <p style={{ fontSize: "0.6rem", fontWeight: 700, color: "rgba(148,163,184,0.35)", letterSpacing: "0.1em", textTransform: "uppercase" }}>BIENVENIDO</p>
              <h1 style={{ fontSize: "0.95rem", fontWeight: 800, color: "#f1f5f9", letterSpacing: "-0.01em", lineHeight: 1.2 }}>
                {userName} 👋
              </h1>
            </div>

            {/* Bell */}
            <button
              style={{ width: 38, height: 38, borderRadius: 11, background: "rgba(255,255,255,0.06)", borderTop: "1px solid rgba(255,255,255,0.11)", borderRight: "1px solid rgba(255,255,255,0.11)", borderBottom: "1px solid rgba(255,255,255,0.11)", borderLeft: "1px solid rgba(255,255,255,0.11)", color: "rgba(148,163,184,0.7)", display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", position: "relative" }}
            >
              <BellIcon />
              {/* Magenta indicator */}
              <span style={{
                position: "absolute", top: 7, right: 8,
                width: 8, height: 8, borderRadius: "50%",
                background: "#E53BF6", border: "2px solid #0F172A",
                boxShadow: "0 0 8px #E53BF6",
                animation: "cdPulse 2.2s ease-in-out infinite",
              }} />
            </button>
          </header>

          <div style={{ padding: isMobile ? "16px 14px 100px" : "20px 24px 60px", display: "flex", flexDirection: "column", gap: 18 }}>

            {/* ══ BPM ACTION BANNER ══ */}
            <div style={{
              borderRadius: 22, padding: isMobile ? "20px 18px" : "24px 22px",
              background: "linear-gradient(135deg, #0e2a2a 0%, #1a0e2a 45%, #2a0e1a 100%)",
              backgroundSize: "200% 200%",
              animation: "cdBannerShift 8s ease infinite",
              borderTop:    "1px solid rgba(59,246,229,0.22)",
              borderRight:  "1px solid rgba(229,59,246,0.18)",
              borderBottom: "1px solid rgba(229,59,246,0.18)",
              borderLeft:   "1px solid rgba(59,246,229,0.22)",
              boxShadow: "0 20px 60px rgba(0,0,0,0.5), 0 0 60px rgba(59,246,229,0.08) inset",
              position: "relative", overflow: "hidden",
            }}>
              {/* Inner glow orbs */}
              <div style={{ position: "absolute", top: -30, right: -30, width: 140, height: 140, borderRadius: "50%", background: "rgba(59,246,229,0.15)", filter: "blur(40px)", pointerEvents: "none" }} />
              <div style={{ position: "absolute", bottom: -40, left: 10, width: 160, height: 160, borderRadius: "50%", background: "rgba(229,59,246,0.18)", filter: "blur(48px)", pointerEvents: "none" }} />

              {/* Badge */}
              <div style={{ position: "relative", zIndex: 1, display: "inline-flex", alignItems: "center", gap: 6, padding: "4px 12px", borderRadius: 99, background: "rgba(59,246,229,0.12)", borderTop: "1px solid rgba(59,246,229,0.3)", borderRight: "1px solid rgba(59,246,229,0.3)", borderBottom: "1px solid rgba(59,246,229,0.3)", borderLeft: "1px solid rgba(59,246,229,0.3)", marginBottom: 12 }}>
                <div style={{ width: 6, height: 6, borderRadius: "50%", background: "#3BF6E5", boxShadow: "0 0 6px #3BF6E5", animation: "cdPulse 2s ease-in-out infinite" }} />
                <span style={{ fontSize: "0.58rem", fontWeight: 700, color: "#3BF6E5", letterSpacing: "0.1em" }}>PLATAFORMA BPM ACTIVA</span>
              </div>

              <h2 style={{ position: "relative", zIndex: 1, fontSize: isMobile ? "1.25rem" : "1.45rem", fontWeight: 800, color: "#f1f5f9", letterSpacing: "-0.02em", lineHeight: 1.2, marginBottom: 6 }}>
                Gestiona tus Trámites<br />
                <span style={{ background: "linear-gradient(135deg, #3BF6E5 20%, #E53BF6 80%)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text" }}>
                  de forma digital
                </span>
              </h2>
              <p style={{ position: "relative", zIndex: 1, fontSize: "0.73rem", color: "rgba(148,163,184,0.65)", fontWeight: 300, lineHeight: 1.5, marginBottom: 20, maxWidth: 300 }}>
                Solicita licencias, registros y certificaciones directamente desde la plataforma Evalia.
              </p>

              <button
                onClick={handleNewSol}
                style={{
                  position: "relative", zIndex: 1,
                  display: "inline-flex", alignItems: "center", gap: 8,
                  padding: "13px 22px", borderRadius: 14,
                  background: "linear-gradient(135deg, #3BF6E5 0%, #8b5cf6 50%, #E53BF6 100%)",
                  border: "none", cursor: "pointer",
                  color: "#0F172A", fontWeight: 800, fontSize: "0.88rem",
                  fontFamily: "Poppins, sans-serif", letterSpacing: "0.03em",
                  boxShadow: "0 0 30px rgba(59,246,229,0.4), 0 0 60px rgba(229,59,246,0.25)",
                  transition: "all 0.22s ease",
                }}
              >
                <PlusIcon />
                Nueva Solicitud BPM
              </button>
            </div>

            {/* ══ QUICK STATS ══ */}
            <div style={{ display: "flex", gap: 8 }}>
              {[
                { value: pendientes,       label: "Pendientes",   color: "#F6E53B" },
                { value: enRevision,       label: "En Revisión",  color: "#3BF6E5" },
                { value: aprobadas,        label: "Aprobadas",    color: "#22c55e" },
                { value: EVALUACIONES.length, label: "Evaluac.",  color: "#E53BF6" },
              ].map((st) => (
                <div key={st.label} style={{
                  flex: 1, padding: "10px 8px", borderRadius: 13, textAlign: "center",
                  background: `${st.color}0d`,
                  borderTop:    `1px solid ${st.color}28`,
                  borderRight:  `1px solid ${st.color}28`,
                  borderBottom: `1px solid ${st.color}28`,
                  borderLeft:   `1px solid ${st.color}28`,
                }}>
                  <p style={{ fontSize: "1.3rem", fontWeight: 900, color: st.color, lineHeight: 1, fontFamily: "Poppins, sans-serif", textShadow: `0 0 14px ${st.color}55` }}>
                    {st.value}
                  </p>
                  <p style={{ fontSize: "0.52rem", fontWeight: 600, color: "rgba(148,163,184,0.38)", textTransform: "uppercase", letterSpacing: "0.07em", marginTop: 3, fontFamily: "Poppins, sans-serif" }}>
                    {st.label}
                  </p>
                </div>
              ))}
            </div>

            {/* ══ MIS SOLICITUDES ══ */}
            <section>
              <SectionHeader title="Mis Solicitudes" count={SOLICITUDES.length} onSeeAll={() => setShowAllSol((p) => !p)} />
              <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                {visibleSol.map((sol) => <SolicitudCard key={sol.id} sol={sol} />)}
              </div>
              {!showAllSol && SOLICITUDES.length > 3 && (
                <button
                  onClick={() => setShowAllSol(true)}
                  style={{ width: "100%", marginTop: 10, padding: "10px 0", borderRadius: 12, background: "rgba(255,255,255,0.03)", borderTop: "1px solid rgba(255,255,255,0.07)", borderRight: "1px solid rgba(255,255,255,0.07)", borderBottom: "1px solid rgba(255,255,255,0.07)", borderLeft: "1px solid rgba(255,255,255,0.07)", color: "rgba(148,163,184,0.45)", fontSize: "0.72rem", fontWeight: 600, fontFamily: "Poppins, sans-serif", cursor: "pointer" }}
                >
                  Ver {SOLICITUDES.length - 3} más →
                </button>
              )}
            </section>

            {/* ══ EVALUACIONES ══ */}
            <section>
              <SectionHeader title="Evaluaciones" count={EVALUACIONES.length} onSeeAll={onCompanyProfile} />
              <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                {EVALUACIONES.map((ev) => (
                  <EvalCard key={ev.id} ev={ev} onViewPDF={onViewPDF} />
                ))}
              </div>
              {onCompanyProfile && (
                <button
                  onClick={onCompanyProfile}
                  style={{
                    width: "100%", marginTop: 10, padding: "12px 0", borderRadius: 12,
                    background: "rgba(59,246,229,0.06)",
                    borderTop:    "1px solid rgba(59,246,229,0.2)",
                    borderRight:  "1px solid rgba(59,246,229,0.2)",
                    borderBottom: "1px solid rgba(59,246,229,0.2)",
                    borderLeft:   "1px solid rgba(59,246,229,0.2)",
                    color: "#3BF6E5", fontSize: "0.72rem", fontWeight: 700,
                    fontFamily: "Poppins, sans-serif", cursor: "pointer",
                    display: "flex", alignItems: "center", justifyContent: "center", gap: 6,
                  }}
                >
                  Ver historial completo <ArrowRightIcon color="#3BF6E5" />
                </button>
              )}
            </section>
          </div>
        </div>

        {/* ══ TOAST ══ */}
        {newSolToast && (
          <div style={{
            position: "fixed", bottom: 28, left: "50%", transform: "translateX(-50%)",
            zIndex: 60, display: "flex", alignItems: "center", gap: 10,
            padding: "13px 20px", borderRadius: 16, whiteSpace: "nowrap",
            background: "rgba(10,20,40,0.95)",
            backdropFilter: "blur(24px)", WebkitBackdropFilter: "blur(24px)",
            borderTop:    "1px solid rgba(59,246,229,0.35)",
            borderRight:  "1px solid rgba(59,246,229,0.35)",
            borderBottom: "1px solid rgba(59,246,229,0.35)",
            borderLeft:   "1px solid rgba(59,246,229,0.35)",
            boxShadow: "0 16px 48px rgba(0,0,0,0.6), 0 0 30px rgba(59,246,229,0.18)",
            animation: "cdToastIn 0.3s cubic-bezier(.22,1,.36,1) both",
          }}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M12 5v14M5 12h14" stroke="#3BF6E5" strokeWidth="2.5" strokeLinecap="round"/></svg>
            <span style={{ fontSize: "0.82rem", fontWeight: 600, color: "#f1f5f9", fontFamily: "Poppins, sans-serif" }}>
              Formulario de nueva solicitud abierto
            </span>
          </div>
        )}
      </div>
    </>
  );
}

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

/* ── Sample data ─────────────────────────────────────────────── */
const COMPANY = {
  nombre:    "Constructora García & Asoc.",
  rnc:       "1-31-01234-5",
  comercial: "García Constructores",
  actividad: "Construcción y Obras Civiles",
  provincia: "Santo Domingo",
  telefono:  "+1 (809) 555-0192",
  correo:    "operaciones@garcia-const.com.do",
  since:     "2019",
};

type RiskLevel = "Riesgo Bajo" | "Riesgo Moderado" | "Riesgo Alto" | "Riesgo Crítico";

interface Evaluation {
  id: string;
  date: string;        // ISO string
  dateLabel: string;   // display
  tipo: string;
  risk: RiskLevel;
  inspector: string;
  score: number;       // 0-100
}

const EVALS: Evaluation[] = [
  { id: "EVA-2025-0341", date: "2025-03-15", dateLabel: "15 Mar 2025", tipo: "Inspección General",         risk: "Riesgo Bajo",      inspector: "Ing. M. Santos",    score: 91 },
  { id: "EVA-2024-0887", date: "2024-09-12", dateLabel: "12 Sep 2024", tipo: "Auditoría de Procesos",      risk: "Riesgo Moderado",  inspector: "Lic. A. Fernández", score: 68 },
  { id: "EVA-2024-0312", date: "2024-05-03", dateLabel: "03 May 2024", tipo: "Inspección de Seguridad",    risk: "Riesgo Alto",      inspector: "Ing. R. Méndez",    score: 44 },
  { id: "EVA-2023-0764", date: "2023-11-18", dateLabel: "18 Nov 2023", tipo: "Inspección General",         risk: "Riesgo Bajo",      inspector: "Ing. M. Santos",    score: 88 },
  { id: "EVA-2023-0291", date: "2023-06-07", dateLabel: "07 Jun 2023", tipo: "Revisión Extraordinaria",    risk: "Riesgo Crítico",   inspector: "Lic. C. Vargas",    score: 28 },
  { id: "EVA-2023-0091", date: "2023-01-22", dateLabel: "22 Ene 2023", tipo: "Auditoría de Procesos",      risk: "Riesgo Moderado",  inspector: "Lic. A. Fernández", score: 62 },
  { id: "EVA-2022-0509", date: "2022-08-30", dateLabel: "30 Ago 2022", tipo: "Inspección General",         risk: "Riesgo Bajo",      inspector: "Ing. M. Santos",    score: 85 },
];

/* ── Risk palette ────────────────────────────────────────────── */
const RISK: Record<RiskLevel, { color: string; bg: string; border: string; label: string }> = {
  "Riesgo Bajo":     { color: "#22c55e", bg: "rgba(34,197,94,0.11)",  border: "rgba(34,197,94,0.35)",  label: "Bajo"     },
  "Riesgo Moderado": { color: "#f59e0b", bg: "rgba(245,158,11,0.11)", border: "rgba(245,158,11,0.35)", label: "Moderado" },
  "Riesgo Alto":     { color: "#E53BF6", bg: "rgba(229,59,246,0.11)", border: "rgba(229,59,246,0.35)", label: "Alto"     },
  "Riesgo Crítico":  { color: "#ef4444", bg: "rgba(239,68,68,0.11)",  border: "rgba(239,68,68,0.35)",  label: "Crítico"  },
};

/* ── Filter options ──────────────────────────────────────────── */
type Filter = "Todos" | RiskLevel;
const FILTERS: Filter[] = ["Todos", "Riesgo Bajo", "Riesgo Moderado", "Riesgo Alto", "Riesgo Crítico"];

/* ── Icons ───────────────────────────────────────────────────── */
function PhoneIcon({ color = "#94a3b8" }: { color?: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
      <path d="M22 16.92v3a2 2 0 01-2.18 2 19.79 19.79 0 01-8.63-3.07A19.5 19.5 0 013.07 10.8a19.79 19.79 0 01-3.07-8.7A2 2 0 012 0h3a2 2 0 012 1.72c.127.96.361 1.903.7 2.81a2 2 0 01-.45 2.11L6.09 7.91a16 16 0 006 6l1.27-1.27a2 2 0 012.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0122 14.92z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function MailIcon({ color = "#94a3b8" }: { color?: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
      <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" />
      <path d="M22 6l-10 7L2 6" stroke={color} strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function DocIcon({ color = "#94a3b8" }: { color?: string }) {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M14 2v6h6M16 13H8M16 17H8M10 9H8"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function BackArrow() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none">
      <path d="M19 12H5M5 12l7 7M5 12l7-7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function EditIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
      <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"
        stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function ShieldIcon({ color }: { color: string }) {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none">
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function CalendarIcon({ color = "#94a3b8" }: { color?: string }) {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none">
      <rect x="3" y="4" width="18" height="18" rx="2" stroke={color} strokeWidth="1.75" />
      <path d="M16 2v4M8 2v4M3 10h18" stroke={color} strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

/* ── Donut chart (SVG) ───────────────────────────────────────── */
function MiniDonut({ score, color }: { score: number; color: string }) {
  const r = 20;
  const cx = 28;
  const cy = 28;
  const circ = 2 * Math.PI * r;
  const filled = (score / 100) * circ;

  return (
    <svg width="56" height="56" viewBox="0 0 56 56">
      <defs>
        <filter id="cpDonutGlow" x="-40%" y="-40%" width="180%" height="180%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="2" result="b" />
          <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
      </defs>
      <circle cx={cx} cy={cy} r={r} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="5" />
      <circle
        cx={cx} cy={cy} r={r} fill="none"
        stroke={color} strokeWidth="5"
        strokeDasharray={`${filled} ${circ}`}
        strokeLinecap="round"
        strokeDashoffset={circ * 0.25}
        filter="url(#cpDonutGlow)"
        style={{ transition: "stroke-dasharray 0.6s ease" }}
      />
      <text x={cx} y={cy + 4} textAnchor="middle" fill={color}
        fontSize="11" fontWeight="700" fontFamily="Poppins, sans-serif">
        {score}
      </text>
    </svg>
  );
}

/* ── Evaluation card ─────────────────────────────────────────── */
function EvalCard({ ev, animate, onViewPDF }: { ev: Evaluation; animate: boolean; onViewPDF?: () => void }) {
  const risk = RISK[ev.risk];

  return (
    <div
      className={animate ? "animate-fade-in" : ""}
      style={{
        borderRadius: 14,
        padding: "13px 14px",
        background: "rgba(12,20,40,0.75)",
        backdropFilter: "blur(20px)",
        WebkitBackdropFilter: "blur(20px)",
        borderTop:    "1px solid rgba(255,255,255,0.07)",
        borderRight:  "1px solid rgba(255,255,255,0.05)",
        borderBottom: "1px solid rgba(255,255,255,0.05)",
        borderLeft:   `3px solid ${risk.color}88`,
        display: "flex",
        alignItems: "center",
        gap: 12,
        boxShadow: `0 4px 20px rgba(0,0,0,0.35), 0 0 0 1px ${risk.color}0a inset`,
      }}
    >
      {/* Donut score */}
      <div style={{ flexShrink: 0 }}>
        <MiniDonut score={ev.score} color={risk.color} />
      </div>

      {/* Content */}
      <div style={{ flex: 1, minWidth: 0 }}>
        {/* Date + ID */}
        <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 4 }}>
          <CalendarIcon color="rgba(148,163,184,0.4)" />
          <span style={{ fontSize: "0.6rem", color: "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>
            {ev.dateLabel}
          </span>
          <span style={{ width: 2, height: 2, borderRadius: "50%", background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />
          <span style={{ fontSize: "0.58rem", color: "rgba(148,163,184,0.28)", fontFamily: "'Courier New', monospace" }}>
            {ev.id}
          </span>
        </div>

        {/* Tipo */}
        <p style={{ fontSize: "0.8rem", fontWeight: 600, color: "#e2e8f0", fontFamily: "Poppins, sans-serif", marginBottom: 6, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
          {ev.tipo}
        </p>

        {/* Risk chip + inspector */}
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <div style={{
            display: "inline-flex", alignItems: "center", gap: 5,
            padding: "3px 9px", borderRadius: 99,
            background: risk.bg,
            borderTop:    `1px solid ${risk.border}`,
            borderRight:  `1px solid ${risk.border}`,
            borderBottom: `1px solid ${risk.border}`,
            borderLeft:   `1px solid ${risk.border}`,
          }}>
            <ShieldIcon color={risk.color} />
            <span style={{ fontSize: "0.62rem", fontWeight: 700, color: risk.color, fontFamily: "Poppins, sans-serif", letterSpacing: "0.02em" }}>
              {risk.label}
            </span>
          </div>
          <span style={{ fontSize: "0.58rem", color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {ev.inspector}
          </span>
        </div>
      </div>

      {/* PDF button */}
      <button
        type="button"
        onClick={onViewPDF}
        title={`Ver informe ${ev.id}`}
        style={{
          flexShrink: 0,
          display: "flex", flexDirection: "column", alignItems: "center", gap: 3,
          padding: "8px 10px", borderRadius: 10,
          background: "rgba(255,255,255,0.04)",
          borderTop:    "1px solid rgba(255,255,255,0.09)",
          borderRight:  "1px solid rgba(255,255,255,0.09)",
          borderBottom: "1px solid rgba(255,255,255,0.09)",
          borderLeft:   "1px solid rgba(255,255,255,0.09)",
          color: "rgba(148,163,184,0.6)",
          cursor: "pointer",
          transition: "all 0.2s ease",
        }}
        onMouseEnter={(e) => {
          (e.currentTarget as HTMLElement).style.background = "rgba(59,246,229,0.07)";
          (e.currentTarget as HTMLElement).style.borderTop = "1px solid rgba(59,246,229,0.28)";
          (e.currentTarget as HTMLElement).style.borderRight = "1px solid rgba(59,246,229,0.28)";
          (e.currentTarget as HTMLElement).style.borderBottom = "1px solid rgba(59,246,229,0.28)";
          (e.currentTarget as HTMLElement).style.borderLeft = "1px solid rgba(59,246,229,0.28)";
          (e.currentTarget as HTMLElement).style.color = "#3BF6E5";
        }}
        onMouseLeave={(e) => {
          (e.currentTarget as HTMLElement).style.background = "rgba(255,255,255,0.04)";
          (e.currentTarget as HTMLElement).style.borderTop = "1px solid rgba(255,255,255,0.09)";
          (e.currentTarget as HTMLElement).style.borderRight = "1px solid rgba(255,255,255,0.09)";
          (e.currentTarget as HTMLElement).style.borderBottom = "1px solid rgba(255,255,255,0.09)";
          (e.currentTarget as HTMLElement).style.borderLeft = "1px solid rgba(255,255,255,0.09)";
          (e.currentTarget as HTMLElement).style.color = "rgba(148,163,184,0.6)";
        }}
      >
        <DocIcon color="currentColor" />
        <span style={{ fontSize: "0.52rem", fontWeight: 600, letterSpacing: "0.04em", fontFamily: "Poppins, sans-serif" }}>
          PDF
        </span>
      </button>
    </div>
  );
}

/* ── Company initials avatar ─────────────────────────────────── */
function CompanyAvatar({ name, size = 60 }: { name: string; size?: number }) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join("");

  return (
    <div style={{
      width: size, height: size, borderRadius: size * 0.28, flexShrink: 0,
      background: "linear-gradient(135deg, rgba(59,246,229,0.25) 0%, rgba(229,59,246,0.25) 100%)",
      borderTop:    "1.5px solid rgba(59,246,229,0.45)",
      borderRight:  "1.5px solid rgba(229,59,246,0.35)",
      borderBottom: "1.5px solid rgba(229,59,246,0.35)",
      borderLeft:   "1.5px solid rgba(59,246,229,0.45)",
      display: "flex", alignItems: "center", justifyContent: "center",
      boxShadow: "0 0 28px rgba(59,246,229,0.2), 0 0 52px rgba(229,59,246,0.12)",
    }}>
      <span style={{
        fontSize: size * 0.3, fontWeight: 800,
        fontFamily: "Poppins, sans-serif",
        background: "linear-gradient(135deg, #3BF6E5, #E53BF6)",
        WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent",
        backgroundClip: "text",
        letterSpacing: "-0.02em",
      }}>
        {initials}
      </span>
    </div>
  );
}

/* ── Stat pill ───────────────────────────────────────────────── */
function StatPill({ value, label, color }: { value: string | number; label: string; color: string }) {
  return (
    <div style={{
      display: "flex", flexDirection: "column", alignItems: "center",
      padding: "8px 14px", borderRadius: 12,
      background: `${color}0d`,
      borderTop:    `1px solid ${color}28`,
      borderRight:  `1px solid ${color}28`,
      borderBottom: `1px solid ${color}28`,
      borderLeft:   `1px solid ${color}28`,
      flex: 1,
    }}>
      <span style={{ fontSize: "1.1rem", fontWeight: 800, color, fontFamily: "Poppins, sans-serif", lineHeight: 1 }}>
        {value}
      </span>
      <span style={{ fontSize: "0.55rem", fontWeight: 600, color: "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif", letterSpacing: "0.07em", textTransform: "uppercase", marginTop: 3 }}>
        {label}
      </span>
    </div>
  );
}

/* ── Main component ──────────────────────────────────────────── */
export default function CompanyProfile({ onBack, onViewPDF, onEditProfile, embedded }: { onBack?: () => void; onViewPDF?: () => void; onEditProfile?: () => void; embedded?: boolean }) {
  const width    = useWidth();
  const isMobile = width < 768;

  const [filter, setFilter] = useState<Filter>("Todos");

  const filtered = filter === "Todos"
    ? EVALS
    : EVALS.filter((e) => e.risk === filter);

  /* Stats */
  const totalBajo     = EVALS.filter((e) => e.risk === "Riesgo Bajo").length;
  const totalModerado = EVALS.filter((e) => e.risk === "Riesgo Moderado").length;
  const totalAlto     = EVALS.filter((e) => e.risk === "Riesgo Alto").length + EVALS.filter((e) => e.risk === "Riesgo Crítico").length;
  const avgScore      = Math.round(EVALS.reduce((s, e) => s + e.score, 0) / EVALS.length);

  const content = (
    <div style={{ position: "relative", zIndex: 1, padding: embedded ? "0" : isMobile ? "0 0 32px" : "28px 20px 32px" }}>
      {/* ── Top nav bar ── */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: embedded ? "0 0 18px" : isMobile ? "16px 16px 12px" : "0 0 18px" }}>
        {!embedded && (
          <button
            type="button" onClick={onBack}
            style={{ width: 36, height: 36, borderRadius: 10, background: "rgba(255,255,255,0.06)", borderTop: "1px solid rgba(255,255,255,0.12)", borderRight: "1px solid rgba(255,255,255,0.12)", borderBottom: "1px solid rgba(255,255,255,0.12)", borderLeft: "1px solid rgba(255,255,255,0.12)", color: "rgba(148,163,184,0.7)", display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}
          >
            <BackArrow />
          </button>
        )}
        <span style={{ fontSize: "0.7rem", fontWeight: 700, letterSpacing: "0.1em", color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif", textTransform: "uppercase" }}>
          Perfil de Empresa
        </span>
        <button
          type="button"
          onClick={onEditProfile}
          title="Editar Perfil"
          style={{ width: 36, height: 36, borderRadius: 10, background: "rgba(59,246,229,0.07)", borderTop: "1px solid rgba(59,246,229,0.22)", borderRight: "1px solid rgba(59,246,229,0.22)", borderBottom: "1px solid rgba(59,246,229,0.22)", borderLeft: "1px solid rgba(59,246,229,0.22)", color: "#3BF6E5", display: "flex", alignItems: "center", justifyContent: "center", cursor: onEditProfile ? "pointer" : "default", opacity: onEditProfile ? 1 : 0.4 }}
        >
          <EditIcon />
        </button>
      </div>

          {/* ═══ HEADER CARD ═══ */}
          <div
            className="cp-header-card animate-fade-in"
            style={{
              margin: isMobile ? "0 12px 14px" : "0 0 14px",
              borderRadius: 22,
              overflow: "hidden",
              background: "rgba(10,18,36,0.92)",
              backdropFilter: "blur(40px) saturate(160%)",
              WebkitBackdropFilter: "blur(40px) saturate(160%)",
              borderTop:    "1px solid rgba(59,246,229,0.22)",
              borderRight:  "1px solid rgba(255,255,255,0.07)",
              borderBottom: "1px solid rgba(255,255,255,0.06)",
              borderLeft:   "1px solid rgba(229,59,246,0.18)",
            }}
          >
            {/* Gradient bar top */}
            <div style={{ height: 3, background: "linear-gradient(90deg, #3BF6E5 0%, #8b5cf6 50%, #E53BF6 100%)" }} />

            <div style={{ padding: "18px 18px 16px" }}>
              {/* Avatar + identity */}
              <div style={{ display: "flex", alignItems: "flex-start", gap: 14, marginBottom: 16 }}>
                <CompanyAvatar name={COMPANY.comercial} size={isMobile ? 58 : 66} />

                <div style={{ flex: 1, minWidth: 0, paddingTop: 2 }}>
                  {/* Status chip */}
                  <div style={{ display: "inline-flex", alignItems: "center", gap: 5, padding: "2px 9px", borderRadius: 99, background: "rgba(34,197,94,0.1)", borderTop: "1px solid rgba(34,197,94,0.3)", borderRight: "1px solid rgba(34,197,94,0.3)", borderBottom: "1px solid rgba(34,197,94,0.3)", borderLeft: "1px solid rgba(34,197,94,0.3)", marginBottom: 6 }}>
                    <span style={{ width: 6, height: 6, borderRadius: "50%", background: "#22c55e", boxShadow: "0 0 6px #22c55e", display: "inline-block" }} />
                    <span style={{ fontSize: "0.57rem", fontWeight: 700, color: "#22c55e", fontFamily: "Poppins, sans-serif", letterSpacing: "0.06em" }}>ACTIVO</span>
                  </div>

                  {/* Company name */}
                  <h2 style={{ fontSize: isMobile ? "1.15rem" : "1.3rem", fontWeight: 800, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", letterSpacing: "-0.02em", lineHeight: 1.15, marginBottom: 4, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {COMPANY.comercial}
                  </h2>

                  {/* RNC */}
                  <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 4 }}>
                    <span style={{ fontSize: "0.58rem", fontWeight: 700, color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif", letterSpacing: "0.08em", textTransform: "uppercase" }}>RNC</span>
                    <span style={{ fontSize: "0.72rem", fontWeight: 600, color: "#3BF6E5", fontFamily: "'Courier New', monospace", letterSpacing: "0.06em" }}>{COMPANY.rnc}</span>
                  </div>

                  {/* Razón Social */}
                  <p style={{ fontSize: "0.67rem", color: "rgba(148,163,184,0.5)", fontFamily: "Poppins, sans-serif", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {COMPANY.nombre}
                  </p>
                </div>
              </div>

              {/* Divider */}
              <div style={{ height: 1, background: "rgba(255,255,255,0.05)", marginBottom: 14 }} />

              {/* Activity + province row */}
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 14, flexWrap: "wrap" }}>
                <div style={{ display: "inline-flex", alignItems: "center", gap: 6, padding: "4px 11px", borderRadius: 99, background: "rgba(246,229,59,0.07)", borderTop: "1px solid rgba(246,229,59,0.2)", borderRight: "1px solid rgba(246,229,59,0.2)", borderBottom: "1px solid rgba(246,229,59,0.2)", borderLeft: "1px solid rgba(246,229,59,0.2)" }}>
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" stroke="#F6E53B" strokeWidth="1.75" /><path d="M9 22V12h6v10" stroke="#F6E53B" strokeWidth="1.75" strokeLinecap="round" /></svg>
                  <span style={{ fontSize: "0.62rem", fontWeight: 600, color: "#F6E53B", fontFamily: "Poppins, sans-serif" }}>{COMPANY.actividad}</span>
                </div>
                <div style={{ display: "inline-flex", alignItems: "center", gap: 5, padding: "4px 10px", borderRadius: 99, background: "rgba(255,255,255,0.04)", borderTop: "1px solid rgba(255,255,255,0.09)", borderRight: "1px solid rgba(255,255,255,0.09)", borderBottom: "1px solid rgba(255,255,255,0.09)", borderLeft: "1px solid rgba(255,255,255,0.09)" }}>
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0118 0z" stroke="rgba(148,163,184,0.45)" strokeWidth="1.75" /><circle cx="12" cy="10" r="3" stroke="rgba(148,163,184,0.45)" strokeWidth="1.75" /></svg>
                  <span style={{ fontSize: "0.62rem", fontWeight: 500, color: "rgba(148,163,184,0.5)", fontFamily: "Poppins, sans-serif" }}>{COMPANY.provincia}</span>
                </div>
              </div>

              {/* Contact row */}
              <div style={{ display: "flex", gap: 9 }}>
                <a
                  href={`tel:${COMPANY.telefono}`}
                  style={{
                    flex: 1, display: "flex", alignItems: "center", gap: 8,
                    padding: "9px 12px", borderRadius: 11, textDecoration: "none",
                    background: "rgba(59,246,229,0.05)",
                    borderTop:    "1px solid rgba(59,246,229,0.18)",
                    borderRight:  "1px solid rgba(59,246,229,0.18)",
                    borderBottom: "1px solid rgba(59,246,229,0.18)",
                    borderLeft:   "1px solid rgba(59,246,229,0.18)",
                  }}
                >
                  <div style={{ width: 26, height: 26, borderRadius: 8, background: "rgba(59,246,229,0.12)", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                    <PhoneIcon color="#3BF6E5" />
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <p style={{ fontSize: "0.52rem", fontWeight: 700, color: "rgba(59,246,229,0.55)", fontFamily: "Poppins, sans-serif", letterSpacing: "0.07em", textTransform: "uppercase", marginBottom: 1 }}>Teléfono</p>
                    <p style={{ fontSize: "0.68rem", fontWeight: 600, color: "#3BF6E5", fontFamily: "Poppins, sans-serif", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{COMPANY.telefono}</p>
                  </div>
                </a>

                <a
                  href={`mailto:${COMPANY.correo}`}
                  style={{
                    flex: 1, display: "flex", alignItems: "center", gap: 8,
                    padding: "9px 12px", borderRadius: 11, textDecoration: "none",
                    background: "rgba(229,59,246,0.05)",
                    borderTop:    "1px solid rgba(229,59,246,0.18)",
                    borderRight:  "1px solid rgba(229,59,246,0.18)",
                    borderBottom: "1px solid rgba(229,59,246,0.18)",
                    borderLeft:   "1px solid rgba(229,59,246,0.18)",
                  }}
                >
                  <div style={{ width: 26, height: 26, borderRadius: 8, background: "rgba(229,59,246,0.12)", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                    <MailIcon color="#E53BF6" />
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <p style={{ fontSize: "0.52rem", fontWeight: 700, color: "rgba(229,59,246,0.55)", fontFamily: "Poppins, sans-serif", letterSpacing: "0.07em", textTransform: "uppercase", marginBottom: 1 }}>Correo</p>
                    <p style={{ fontSize: "0.68rem", fontWeight: 600, color: "#E53BF6", fontFamily: "Poppins, sans-serif", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{COMPANY.correo}</p>
                  </div>
                </a>
              </div>

              {/* Stats row */}
              <div style={{ display: "flex", gap: 8, marginTop: 14 }}>
                <StatPill value={EVALS.length}  label="Evaluaciones" color="#3BF6E5"  />
                <StatPill value={totalBajo}      label="Bajo riesgo"  color="#22c55e"  />
                <StatPill value={totalAlto}      label="Alto riesgo"  color="#E53BF6"  />
                <StatPill value={`${avgScore}%`} label="Promedio"     color="#F6E53B"  />
              </div>

              {/* Since note */}
              <p style={{ marginTop: 12, fontSize: "0.56rem", color: "rgba(148,163,184,0.22)", fontFamily: "Poppins, sans-serif", textAlign: "center", letterSpacing: "0.05em" }}>
                REGISTRADO DESDE {COMPANY.since} · PLATAFORMA EVALIA BPM
              </p>
            </div>
          </div>

          {/* ═══ EVALUACIONES PREVIAS ═══ */}
          <div style={{ margin: isMobile ? "0 12px" : "0" }}>
            {/* Section header */}
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <div style={{ width: 3, height: 18, borderRadius: 99, background: "linear-gradient(180deg, #3BF6E5, #E53BF6)" }} />
                <h3 style={{ fontSize: "0.95rem", fontWeight: 700, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", letterSpacing: "-0.01em" }}>
                  Evaluaciones Previas
                </h3>
              </div>
              <span style={{ fontSize: "0.62rem", fontWeight: 600, color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif" }}>
                {filtered.length} de {EVALS.length}
              </span>
            </div>

            {/* Filter chips */}
            <div
              className="hide-scroll"
              style={{ display: "flex", gap: 7, overflowX: "auto", paddingBottom: 2, marginBottom: 12 }}
            >
              {FILTERS.map((f) => {
                const active = filter === f;
                const riskData = f !== "Todos" ? RISK[f as RiskLevel] : null;
                const accentColor = riskData ? riskData.color : "#3BF6E5";
                return (
                  <button
                    key={f}
                    type="button"
                    onClick={() => setFilter(f)}
                    style={{
                      flexShrink: 0,
                      padding: "5px 13px", borderRadius: 99,
                      fontFamily: "Poppins, sans-serif", fontSize: "0.65rem", fontWeight: 600,
                      cursor: "pointer", transition: "all 0.18s ease",
                      background: active ? `${accentColor}18` : "rgba(255,255,255,0.04)",
                      borderTop:    active ? `1px solid ${accentColor}50` : "1px solid rgba(255,255,255,0.09)",
                      borderRight:  active ? `1px solid ${accentColor}50` : "1px solid rgba(255,255,255,0.09)",
                      borderBottom: active ? `1px solid ${accentColor}50` : "1px solid rgba(255,255,255,0.09)",
                      borderLeft:   active ? `1px solid ${accentColor}50` : "1px solid rgba(255,255,255,0.09)",
                      color: active ? accentColor : "rgba(148,163,184,0.45)",
                      boxShadow: active ? `0 0 12px ${accentColor}18` : "none",
                    }}
                  >
                    {f === "Todos" ? "Todos" : RISK[f as RiskLevel].label}
                  </button>
                );
              })}
            </div>

            {/* Evaluation list */}
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {filtered.length > 0 ? (
                filtered.map((ev, i) => (
                  <EvalCard key={ev.id} ev={ev} animate={i < 3} onViewPDF={onViewPDF} />
                ))
              ) : (
                <div style={{ textAlign: "center", padding: "40px 0" }}>
                  <p style={{ fontSize: "0.82rem", color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif" }}>
                    Sin evaluaciones en esta categoría
                  </p>
                </div>
              )}
            </div>
          </div>
        </div>
  );

  if (embedded) {
    return (
      <>
        <style>{`
          @keyframes cpHeaderGlow {
            0%,100% { box-shadow: 0 0 40px rgba(59,246,229,0.1), 0 0 80px rgba(229,59,246,0.08); }
            50%      { box-shadow: 0 0 60px rgba(59,246,229,0.18), 0 0 110px rgba(229,59,246,0.14); }
          }
          .cp-header-card { animation: cpHeaderGlow 5s ease-in-out infinite; }
        `}</style>
        {content}
      </>
    );
  }

  return (
    <>
      <style>{`
        @keyframes cpHeaderGlow {
          0%,100% { box-shadow: 0 0 40px rgba(59,246,229,0.1), 0 0 80px rgba(229,59,246,0.08); }
          50%      { box-shadow: 0 0 60px rgba(59,246,229,0.18), 0 0 110px rgba(229,59,246,0.14); }
        }
        .cp-header-card { animation: cpHeaderGlow 5s ease-in-out infinite; }
        .hide-scroll::-webkit-scrollbar { display: none; }
        .hide-scroll { -ms-overflow-style: none; scrollbar-width: none; }
      `}</style>
      <div style={{ minHeight: "100svh", background: "#0F172A", position: "relative", overflow: "hidden" }}>
        <div className="mesh-orb"            style={{ width: 480, height: 480, background: "rgba(59,246,229,0.3)",  top: "-160px", right: "-100px" }} />
        <div className="mesh-orb mesh-orb-2" style={{ width: 380, height: 380, background: "rgba(229,59,246,0.28)", bottom: "0px",  left:  "-80px"  }} />
        <div className="mesh-orb mesh-orb-3" style={{ width: 200, height: 200, background: "rgba(246,229,59,0.1)",  top: "55%",   left:  "60%"    }} />
        <div style={{ position: "absolute", inset: 0, opacity: 0.04, backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`, backgroundSize: "180px", pointerEvents: "none", zIndex: 0 }} />
        <div style={{ maxWidth: isMobile ? "100%" : 540, margin: "0 auto" }}>
          {content}
        </div>
      </div>
    </>
  );
}

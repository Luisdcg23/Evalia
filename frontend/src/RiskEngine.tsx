import { useEffect, useState } from "react";

// ── Gauge math ────────────────────────────────────────────────
function polar(cx: number, cy: number, r: number, deg: number) {
  const rad = (deg - 90) * (Math.PI / 180);
  return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
}

function arcPath(cx: number, cy: number, r: number, a1: number, a2: number) {
  const s = polar(cx, cy, r, a1);
  const e = polar(cx, cy, r, a2);
  let d = a2 - a1;
  while (d <= 0) d += 360;
  return `M ${s.x.toFixed(2)} ${s.y.toFixed(2)} A ${r} ${r} 0 ${d > 180 ? 1 : 0} 1 ${e.x.toFixed(2)} ${e.y.toFixed(2)}`;
}

// ── Gauge constants ───────────────────────────────────────────
const GCX = 140, GCY = 138, GR = 84, GSW = 14;
const SCORE = 7.2;
const SCORE_PCT = SCORE / 10;
const ANG_START  = 225;                                    // gauge min (7.5 o'clock)
const ANG_SWEEP  = 270;                                    // total sweep
const ANG_Z40    = (ANG_START + ANG_SWEEP * 0.40) % 360;  // 333° green→yellow
const ANG_Z70    = (ANG_START + ANG_SWEEP * 0.70) % 360;  // 54°  yellow→red
const ANG_Z100   = (ANG_START + ANG_SWEEP) % 360;          // 135° gauge max
const ANG_SCORE  = (ANG_START + ANG_SWEEP * SCORE_PCT) % 360; // ~59.4°

const ptScore = polar(GCX, GCY, GR, ANG_SCORE);
const ptNOut  = polar(GCX, GCY, GR - GSW / 2 + 1, ANG_SCORE);
const ptNIn   = polar(GCX, GCY, 18, ANG_SCORE);

// ── Data ─────────────────────────────────────────────────────
const metrics = [
  { label: "Puntaje", value: "7.2", unit: "/10", accent: "#E53BF6", grad: "linear-gradient(135deg,#E53BF6,#c026d3)" },
  { label: "Cumplimiento", value: "45", unit: "%", accent: "#F59E0B", grad: "linear-gradient(135deg,#F59E0B,#d97706)" },
  { label: "No Conformidades", value: "12", unit: "", accent: "#EF4444", grad: "linear-gradient(135deg,#EF4444,#dc2626)" },
];

const findings = [
  { id: "RF-16-01", title: "Sistema de extinción de incendios no operativo", cat: "Seguridad Contra Incendios", deadline: "30 días" },
  { id: "RF-16-02", title: "Ausencia de equipos de protección personal (EPP) en área de producción", cat: "Salud Ocupacional", deadline: "15 días" },
  { id: "RF-16-03", title: "Plan de evacuación y emergencia desactualizado (más de 12 meses)", cat: "Gestión de Emergencias", deadline: "45 días" },
];

// ── Icons ─────────────────────────────────────────────────────
function WarningIcon({ color = "#E53BF6", size = 18 }: { color?: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" style={{ flexShrink: 0 }}>
      <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"
        fill={`${color}22`} stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
      <line x1="12" y1="9" x2="12" y2="13" stroke={color} strokeWidth="1.75" strokeLinecap="round" />
      <circle cx="12" cy="17" r="0.5" fill={color} stroke={color} strokeWidth="1.5" />
    </svg>
  );
}

function BackIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
      <path d="M19 12H5M5 12l7 7M5 12l7-7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function PdfIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none">
      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6z" stroke="currentColor" strokeWidth="1.75" fill="none" />
      <path d="M14 2v6h6" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
      <path d="M9 15h6M9 11h6M9 19h3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none">
      <path d="M18 6L6 18M6 6l12 12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function CalIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
      <rect x="3" y="4" width="18" height="18" rx="2" stroke="currentColor" strokeWidth="1.75" fill="none" />
      <path d="M3 10h18" stroke="currentColor" strokeWidth="1.75" />
      <path d="M8 2v4M16 2v4" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

// ── Gauge SVG ─────────────────────────────────────────────────
function GaugeChart() {
  const [on, setOn] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setOn(true), 180);
    return () => clearTimeout(t);
  }, []);

  // Tick marks at 0, 2, 4, 6, 8, 10
  const ticks = [0, 2, 4, 6, 8, 10].map((v) => ({
    v,
    ang: (ANG_START + ANG_SWEEP * (v / 10)) % 360,
    major: v === 0 || v === 10,
  }));

  return (
    <svg viewBox="0 0 280 208" style={{ width: "100%", maxWidth: 280, display: "block", overflow: "visible" }}>
      <defs>
        {/* Score gradient — cyan (low) → amber (mid) → magenta (high) */}
        <linearGradient id="rScoreGrad" x1="0%" y1="100%" x2="100%" y2="0%">
          <stop offset="0%" stopColor="#3BF6E5" />
          <stop offset="40%" stopColor="#F59E0B" />
          <stop offset="100%" stopColor="#E53BF6" />
        </linearGradient>

        {/* Glow filter for score arc */}
        <filter id="rArcGlow" x="-25%" y="-25%" width="150%" height="150%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="6" result="blur" />
          <feMerge>
            <feMergeNode in="blur" />
            <feMergeNode in="SourceGraphic" />
          </feMerge>
        </filter>

        {/* Soft glow for needle + dot */}
        <filter id="rSoftGlow" x="-50%" y="-50%" width="200%" height="200%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="3" result="blur" />
          <feMerge>
            <feMergeNode in="blur" />
            <feMergeNode in="SourceGraphic" />
          </feMerge>
        </filter>

        {/* Subtle hub inner shadow */}
        <radialGradient id="rHubGrad" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#1e293b" />
          <stop offset="100%" stopColor="#0f172a" />
        </radialGradient>
      </defs>

      {/* Zone arcs – dim background scale */}
      <path d={arcPath(GCX, GCY, GR, ANG_START, ANG_Z40)} fill="none" stroke="#34d399" strokeWidth={GSW} opacity={0.13} strokeLinecap="butt" />
      <path d={arcPath(GCX, GCY, GR, ANG_Z40, ANG_Z70)} fill="none" stroke="#F59E0B" strokeWidth={GSW} opacity={0.13} strokeLinecap="butt" />
      <path d={arcPath(GCX, GCY, GR, ANG_Z70, ANG_Z100)} fill="none" stroke="#E53BF6" strokeWidth={GSW} opacity={0.13} strokeLinecap="butt" />

      {/* Zone inner glow ring (thin, slightly brighter) */}
      <path d={arcPath(GCX, GCY, GR - GSW / 2 + 1, ANG_START, ANG_Z40)} fill="none" stroke="#34d399" strokeWidth={1.5} opacity={0.22} strokeLinecap="butt" />
      <path d={arcPath(GCX, GCY, GR - GSW / 2 + 1, ANG_Z40, ANG_Z70)} fill="none" stroke="#F59E0B" strokeWidth={1.5} opacity={0.22} strokeLinecap="butt" />
      <path d={arcPath(GCX, GCY, GR - GSW / 2 + 1, ANG_Z70, ANG_Z100)} fill="none" stroke="#E53BF6" strokeWidth={1.5} opacity={0.22} strokeLinecap="butt" />

      {/* Score arc – gradient + glow */}
      <path
        d={arcPath(GCX, GCY, GR, ANG_START, ANG_SCORE)}
        fill="none"
        stroke="url(#rScoreGrad)"
        strokeWidth={GSW}
        strokeLinecap="round"
        filter="url(#rArcGlow)"
        style={{ opacity: on ? 1 : 0, transition: "opacity 1.1s ease" }}
      />

      {/* Tick marks */}
      {ticks.map(({ v, ang, major }) => {
        const outerR = GR + (major ? 5 : 3);
        const innerR = GR - GSW / 2 - (major ? 5 : 3);
        const op = polar(GCX, GCY, outerR, ang);
        const ip = polar(GCX, GCY, innerR, ang);
        return (
          <line
            key={v}
            x1={ip.x} y1={ip.y}
            x2={op.x} y2={op.y}
            stroke={major ? "rgba(148,163,184,0.45)" : "rgba(148,163,184,0.22)"}
            strokeWidth={major ? 1.5 : 1}
            strokeLinecap="round"
          />
        );
      })}

      {/* Needle line */}
      <line
        x1={ptNIn.x} y1={ptNIn.y}
        x2={ptNOut.x} y2={ptNOut.y}
        stroke="rgba(255,255,255,0.92)"
        strokeWidth={1.75}
        strokeLinecap="round"
        filter="url(#rSoftGlow)"
        style={{ opacity: on ? 1 : 0, transition: "opacity 0.9s ease 0.5s" }}
      />

      {/* Score dot on arc */}
      <circle
        cx={ptScore.x} cy={ptScore.y} r={5.5}
        fill="#E53BF6"
        filter="url(#rSoftGlow)"
        style={{ opacity: on ? 1 : 0, transition: "opacity 0.7s ease 0.8s" }}
      />
      <circle cx={ptScore.x} cy={ptScore.y} r={3} fill="white" opacity={0.9}
        style={{ opacity: on ? 0.9 : 0, transition: "opacity 0.7s ease 0.8s" }}
      />

      {/* Center hub */}
      <circle cx={GCX} cy={GCY} r={31} fill="url(#rHubGrad)" />
      <circle cx={GCX} cy={GCY} r={31} fill="none" stroke="rgba(229,59,246,0.2)" strokeWidth={1.5} />
      <circle cx={GCX} cy={GCY} r={4} fill="rgba(255,255,255,0.7)" />

      {/* Score text */}
      <text
        x={GCX} y={GCY - 4}
        textAnchor="middle"
        fill="#E53BF6"
        fontSize="33"
        fontWeight="900"
        fontFamily="Poppins, sans-serif"
        style={{ letterSpacing: "-0.5px" }}
      >
        {SCORE}
      </text>
      <text x={GCX} y={GCY + 16} textAnchor="middle" fill="rgba(148,163,184,0.6)" fontSize="9.5" fontFamily="Poppins, sans-serif">
        de 10
      </text>

      {/* Scale labels: "0" and "10" just inside the gauge ends */}
      {(() => {
        const p0 = polar(GCX, GCY, GR - GSW - 6, ANG_START);
        const p10 = polar(GCX, GCY, GR - GSW - 6, ANG_Z100);
        return (
          <>
            <text x={p0.x + 3} y={p0.y - 2} textAnchor="middle" fill="rgba(148,163,184,0.4)" fontSize="8.5" fontFamily="Poppins, sans-serif">0</text>
            <text x={p10.x - 3} y={p10.y - 2} textAnchor="middle" fill="rgba(148,163,184,0.4)" fontSize="8.5" fontFamily="Poppins, sans-serif">10</text>
          </>
        );
      })()}
    </svg>
  );
}

// ── Main screen ───────────────────────────────────────────────
export default function RiskEngine({ onBack }: { onBack: () => void }) {
  return (
    <div
      style={{
        minHeight: "100svh",
        height: "100svh",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden",
        position: "relative",
        background: "#0F172A",
        fontFamily: "Poppins, sans-serif",
      }}
    >
      {/* Ambient orbs */}
      <div className="mesh-orb" style={{ width: 420, height: 420, background: "rgba(229,59,246,0.38)", top: "-120px", left: "-90px" }} />
      <div className="mesh-orb mesh-orb-2" style={{ width: 360, height: 360, background: "rgba(59,246,229,0.22)", bottom: "30px", right: "-70px" }} />
      <div className="mesh-orb mesh-orb-3" style={{ width: 260, height: 260, background: "rgba(239,68,68,0.28)", top: "45%", right: "15%" }} />

      {/* ── Header ── */}
      <header
        style={{
          position: "relative",
          zIndex: 10,
          display: "flex",
          alignItems: "center",
          gap: 12,
          padding: "14px 20px",
          backdropFilter: "blur(24px)",
          WebkitBackdropFilter: "blur(24px)",
          background: "rgba(15,23,42,0.75)",
          borderBottom: "1px solid rgba(255,255,255,0.07)",
          flexShrink: 0,
        }}
      >
        <button
          onClick={onBack}
          style={{
            width: 36, height: 36, borderRadius: 10,
            background: "rgba(255,255,255,0.06)",
            border: "1px solid rgba(255,255,255,0.1)",
            display: "flex", alignItems: "center", justifyContent: "center",
            cursor: "pointer", color: "#94a3b8", flexShrink: 0,
            transition: "background 0.2s ease",
          }}
        >
          <BackIcon />
        </button>

        <div style={{ flex: 1, minWidth: 0 }}>
          <p style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.55)", marginBottom: 1 }}>
            Expediente RF-16 · Planta Industrial Norte
          </p>
          <h1 style={{ fontSize: "0.95rem", fontWeight: 700, color: "#f1f5f9" }}>
            Motor de Riesgo
          </h1>
        </div>

        <div
          style={{
            padding: "4px 10px", borderRadius: 99,
            background: "rgba(229,59,246,0.1)",
            border: "1px solid rgba(229,59,246,0.3)",
            flexShrink: 0,
          }}
        >
          <span style={{ fontSize: "0.6rem", fontWeight: 700, color: "#E53BF6", letterSpacing: "0.06em" }}>
            ANÁLISIS COMPLETADO
          </span>
        </div>
      </header>

      {/* ── Scrollable content ── */}
      <div
        className="hide-scroll"
        style={{
          flex: 1,
          overflowY: "auto",
          padding: "20px 16px 0",
          position: "relative",
          zIndex: 1,
        }}
      >
        <div style={{ maxWidth: 640, margin: "0 auto" }}>

          {/* Gauge + verdict */}
          <div
            className="animate-fade-in-up"
            style={{
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: 4,
              marginBottom: 6,
            }}
          >
            {/* Gauge */}
            <div style={{ width: "100%", maxWidth: 260, padding: "4px 0" }}>
              <GaugeChart />
            </div>

            {/* Verdict */}
            <h2
              className="risk-text-glow animate-fade-in-up delay-200"
              style={{
                fontSize: "clamp(1.6rem, 5vw, 2.1rem)",
                fontWeight: 900,
                color: "#E53BF6",
                letterSpacing: "0.09em",
                textTransform: "uppercase",
                marginBottom: 10,
                marginTop: 2,
                textAlign: "center",
              }}
            >
              RIESGO ALTO
            </h2>

            {/* Frequency badge */}
            <div
              className="animate-fade-in-up delay-300"
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 7,
                padding: "7px 16px",
                borderRadius: 99,
                background: "rgba(59,246,229,0.08)",
                border: "1px solid rgba(59,246,229,0.25)",
                marginBottom: 20,
                color: "#3BF6E5",
              }}
            >
              <CalIcon />
              <span style={{ fontSize: "0.72rem", fontWeight: 600, color: "#3BF6E5" }}>
                Frecuencia de Inspección: Trimestral
              </span>
            </div>
          </div>

          {/* ── Metric cards ── */}
          <div
            className="animate-fade-in-up delay-300"
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(3, 1fr)",
              gap: 10,
              marginBottom: 20,
            }}
          >
            {metrics.map((m) => (
              <div
                key={m.label}
                style={{ padding: "1.5px", borderRadius: 20, background: m.grad }}
              >
                <div
                  style={{
                    borderRadius: "18.5px",
                    background: "rgba(15,23,42,0.92)",
                    backdropFilter: "blur(24px)",
                    WebkitBackdropFilter: "blur(24px)",
                    padding: "14px 12px 12px",
                    height: "100%",
                  }}
                >
                  <p
                    style={{
                      fontSize: "0.55rem",
                      color: "rgba(148,163,184,0.65)",
                      fontWeight: 600,
                      textTransform: "uppercase",
                      letterSpacing: "0.07em",
                      marginBottom: 6,
                    }}
                  >
                    {m.label}
                  </p>
                  <div style={{ display: "flex", alignItems: "baseline", gap: 2 }}>
                    <span
                      style={{
                        fontSize: "1.65rem",
                        fontWeight: 900,
                        color: m.accent,
                        lineHeight: 1,
                        textShadow: `0 0 16px ${m.accent}66`,
                      }}
                    >
                      {m.value}
                    </span>
                    {m.unit && (
                      <span style={{ fontSize: "0.7rem", color: "rgba(148,163,184,0.55)" }}>
                        {m.unit}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* ── Findings list ── */}
          <div className="animate-fade-in-up delay-400" style={{ marginBottom: 24 }}>
            {/* Section header */}
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                marginBottom: 12,
              }}
            >
              <WarningIcon size={15} />
              <h3 style={{ fontSize: "0.8rem", fontWeight: 700, color: "#f1f5f9" }}>
                Hallazgos Críticos
              </h3>
              <div
                style={{
                  padding: "3px 9px",
                  borderRadius: 99,
                  background: "rgba(229,59,246,0.12)",
                  border: "1px solid rgba(229,59,246,0.3)",
                }}
              >
                <span style={{ fontSize: "0.6rem", fontWeight: 700, color: "#E53BF6" }}>
                  {findings.length} críticos
                </span>
              </div>
            </div>

            {/* Finding items */}
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {findings.map((f, i) => (
                <div
                  key={f.id}
                  className="animate-fade-in-up"
                  style={{
                    animationDelay: `${0.38 + i * 0.08}s`,
                    display: "flex",
                    gap: 12,
                    alignItems: "flex-start",
                    padding: "14px 16px",
                    borderRadius: 14,
                    background: "rgba(229,59,246,0.05)",
                    border: "1px solid rgba(229,59,246,0.14)",
                    borderLeft: "3px solid rgba(229,59,246,0.7)",
                  }}
                >
                  <div style={{ marginTop: 1, flexShrink: 0 }}>
                    <WarningIcon color="#E53BF6" size={19} />
                  </div>

                  <div style={{ flex: 1, minWidth: 0 }}>
                    <p
                      style={{
                        fontSize: "0.8rem",
                        fontWeight: 600,
                        color: "#f1f5f9",
                        lineHeight: 1.45,
                        marginBottom: 5,
                      }}
                    >
                      {f.title}
                    </p>
                    <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
                      <span style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.55)" }}>
                        {f.cat}
                      </span>
                      <span style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.28)" }}>·</span>
                      <span style={{ fontSize: "0.62rem", fontWeight: 600, color: "#F59E0B" }}>
                        Plazo: {f.deadline}
                      </span>
                    </div>
                  </div>

                  <div style={{ flexShrink: 0 }}>
                    <span
                      style={{
                        display: "inline-block",
                        padding: "3px 8px",
                        borderRadius: 99,
                        fontSize: "0.56rem",
                        fontWeight: 700,
                        background: "rgba(229,59,246,0.12)",
                        border: "1px solid rgba(229,59,246,0.35)",
                        color: "#E53BF6",
                        letterSpacing: "0.04em",
                      }}
                    >
                      CRÍTICA
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Space for sticky buttons */}
          <div style={{ height: 148 }} />
        </div>
      </div>

      {/* ── Fixed bottom CTA ── */}
      <div
        style={{
          position: "absolute",
          bottom: 0,
          left: 0,
          right: 0,
          zIndex: 10,
          padding: "14px 16px 28px",
          backdropFilter: "blur(28px)",
          WebkitBackdropFilter: "blur(28px)",
          background: "rgba(15,23,42,0.88)",
          borderTop: "1px solid rgba(255,255,255,0.07)",
          display: "flex",
          flexDirection: "column",
          gap: 9,
        }}
      >
        {/* Primary — cyan */}
        <button
          style={{
            width: "100%",
            maxWidth: 640,
            margin: "0 auto",
            padding: "15px",
            borderRadius: 14,
            background: "linear-gradient(135deg, #3BF6E5 0%, #22c7e5 50%, #3BF6E5 100%)",
            border: "none",
            cursor: "pointer",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: 10,
            color: "#0F172A",
            fontSize: "0.9rem",
            fontWeight: 700,
            fontFamily: "Poppins, sans-serif",
            letterSpacing: "0.03em",
            boxShadow: "0 0 32px 6px rgba(59,246,229,0.35), 0 2px 20px rgba(0,0,0,0.3)",
            transition: "all 0.2s ease",
          }}
        >
          <PdfIcon />
          Generar Informe PDF
        </button>

        {/* Secondary */}
        <button
          className="passkey-btn"
          style={{
            width: "100%",
            maxWidth: 640,
            margin: "0 auto",
            padding: "13px",
            borderRadius: 14,
            cursor: "pointer",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: 8,
            color: "#94a3b8",
            fontSize: "0.83rem",
            fontWeight: 500,
            fontFamily: "Poppins, sans-serif",
          }}
        >
          <CloseIcon />
          Cerrar Expediente
        </button>
      </div>
    </div>
  );
}

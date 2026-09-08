import { useEffect, useState } from "react";
import evaliaLogo from "@/imports/Disen_o_sin_ti_tulo.png";

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

/* ── Hourglass SVG — animated falling sand ───────────────────── */
function HourglassIcon({ size = 110 }: { size?: number }) {
  const h = size * 1.2;
  return (
    <svg width={size} height={h} viewBox="0 0 100 120" fill="none">
      <defs>
        <linearGradient id="hgG1" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#F6E53B" />
          <stop offset="100%" stopColor="#F0C230" />
        </linearGradient>
        <linearGradient id="hgSand" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="#F6E53B" />
          <stop offset="100%" stopColor="#E8AC20" stopOpacity="0.85" />
        </linearGradient>
        <linearGradient id="hgFill" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="rgba(246,229,59,0.12)" />
          <stop offset="100%" stopColor="rgba(246,229,59,0.04)" />
        </linearGradient>

        {/* Ambient halo — large, soft */}
        <filter id="hgHalo" x="-70%" y="-60%" width="240%" height="220%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="16" />
        </filter>
        {/* Element glow */}
        <filter id="hgGlow" x="-35%" y="-35%" width="170%" height="170%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="3.5" result="b" />
          <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        {/* Subtle sand glow */}
        <filter id="hgSGlow" x="-20%" y="-20%" width="140%" height="140%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="2" result="b" />
          <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>

        {/* Clip top glass triangular area */}
        <clipPath id="hgTopClip">
          <path d="M 5 18 L 95 18 L 53 60 L 47 60 Z" />
        </clipPath>
        {/* Clip bottom glass triangular area */}
        <clipPath id="hgBotClip">
          <path d="M 47 60 L 53 60 L 95 104 L 5 104 Z" />
        </clipPath>
      </defs>

      {/* ── Ambient halo behind hourglass ── */}
      <ellipse cx="50" cy="62" rx="38" ry="48"
        fill="#E8A800" filter="url(#hgHalo)" opacity="0.5" />

      {/* ── Top horizontal bar ── */}
      <rect x="3" y="4" width="94" height="14" rx="7"
        stroke="url(#hgG1)" strokeWidth="1.75"
        fill="rgba(246,229,59,0.12)" filter="url(#hgGlow)" />

      {/* ── Bottom horizontal bar ── */}
      <rect x="3" y="102" width="94" height="14" rx="7"
        stroke="url(#hgG1)" strokeWidth="1.75"
        fill="rgba(246,229,59,0.12)" filter="url(#hgGlow)" />

      {/* ── Top glass (frame outline) ── */}
      <path d="M 5 18 L 95 18 L 53 60 L 47 60 Z"
        stroke="url(#hgG1)" strokeWidth="1.5"
        fill="url(#hgFill)" strokeLinejoin="round" />

      {/* ── Bottom glass (frame outline) ── */}
      <path d="M 47 60 L 53 60 L 95 102 L 5 102 Z"
        stroke="url(#hgG1)" strokeWidth="1.5"
        fill="url(#hgFill)" strokeLinejoin="round" />

      {/* ── Top sand (about 32% remaining — diminished) ── */}
      <rect x="0" y="18" width="100" height="24"
        fill="url(#hgSand)" clipPath="url(#hgTopClip)"
        opacity="0.88" filter="url(#hgSGlow)" />

      {/* ── Bottom sand (about 55% accumulated) ── */}
      <rect x="0" y="78" width="100" height="28"
        fill="url(#hgSand)" clipPath="url(#hgBotClip)"
        opacity="0.88" filter="url(#hgSGlow)" />

      {/* ── Sand surface shimmer line ── */}
      <line x1="28" y1="78.5" x2="72" y2="78.5"
        stroke="rgba(246,229,59,0.55)" strokeWidth="1" />

      {/* ── Neck glow dot ── */}
      <circle cx="50" cy="60" r="3.5"
        fill="#F6E53B" opacity="0.65" filter="url(#hgGlow)" />

      {/* ── Falling sand grains (SVG animate) ── */}
      {([
        { cx: 50,   delay: "0s",    dur: "2.1s", r: 1.9 },
        { cx: 49.3, delay: "0.7s",  dur: "2.1s", r: 1.5 },
        { cx: 50.7, delay: "1.4s",  dur: "2.1s", r: 1.2 },
      ] as const).map((g, i) => (
        <circle key={i} cx={g.cx} r={g.r} fill="#F6E53B" filter="url(#hgSGlow)">
          <animate
            attributeName="cy"
            values="60;77"
            dur={g.dur}
            begin={g.delay}
            repeatCount="indefinite"
            calcMode="linear"
          />
          <animate
            attributeName="opacity"
            values="0;1;0.8;0"
            dur={g.dur}
            begin={g.delay}
            repeatCount="indefinite"
          />
        </circle>
      ))}

      {/* ── Sparkle points around hourglass ── */}
      {([
        { cx: 11,  cy: 22,  maxR: 3.2, delay: "0s",    dur: "2.8s" },
        { cx: 89,  cy: 26,  maxR: 2.6, delay: "0.9s",  dur: "3.1s" },
        { cx: 7,   cy: 97,  maxR: 2.9, delay: "1.7s",  dur: "2.6s" },
        { cx: 93,  cy: 99,  maxR: 3.4, delay: "0.4s",  dur: "3.3s" },
        { cx: 50,  cy: 4,   maxR: 2.4, delay: "1.3s",  dur: "2.5s" },
        { cx: 18,  cy: 60,  maxR: 1.8, delay: "2.0s",  dur: "2.9s" },
        { cx: 82,  cy: 60,  maxR: 1.8, delay: "0.6s",  dur: "2.7s" },
      ] as const).map((s, i) => (
        <circle key={`sp${i}`} cx={s.cx} cy={s.cy} r={0}
          fill="#F6E53B" opacity="0" filter="url(#hgGlow)">
          <animate
            attributeName="r"
            values={`0;${s.maxR};0`}
            dur={s.dur}
            begin={s.delay}
            repeatCount="indefinite"
          />
          <animate
            attributeName="opacity"
            values="0;0.85;0"
            dur={s.dur}
            begin={s.delay}
            repeatCount="indefinite"
          />
        </circle>
      ))}
    </svg>
  );
}

/* ── Back arrow ──────────────────────────────────────────────── */
function BackArrow() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
      <path d="M19 12H5M5 12l7 7M5 12l7-7"
        stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* ── Check (small) ───────────────────────────────────────────── */
function CheckSm({ color = "#F6E53B" }: { color?: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" style={{ flexShrink: 0 }}>
      <path d="M20 6L9 17l-5-5" stroke={color} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* ── Progress step data ──────────────────────────────────────── */
const STEPS = [
  { label: "Registro\nenviado",     done: true,  current: false },
  { label: "Validación\npendiente", done: false, current: true  },
  { label: "Acceso\nactivado",      done: false, current: false },
];

/* ── Main screen ─────────────────────────────────────────────── */
export default function RegistrationPending({ onBack }: { onBack: () => void }) {
  const width    = useWidth();
  const isMobile = width < 768;

  const iconSz    = isMobile ? 90 : 110;
  const headingFs = isMobile ? "1.5rem" : "1.75rem";
  const cardPad   = isMobile ? "32px 20px 44px" : "44px 40px 44px";

  return (
    <>
      {/* Yellow-specific keyframes scoped to this screen */}
      <style>{`
        @keyframes yellowPulse {
          0%, 100% { box-shadow: 0 0 16px rgba(246,229,59,0.45), 0 0 36px rgba(246,229,59,0.22); }
          50%       { box-shadow: 0 0 28px rgba(246,229,59,0.75), 0 0 60px rgba(246,229,59,0.4); }
        }
        @keyframes dotBlink {
          0%, 100% { opacity: 1;   transform: scale(1); }
          50%       { opacity: 0.5; transform: scale(0.75); }
        }
        @keyframes hgCardGlow {
          0%, 100% { box-shadow: 0 0 60px rgba(246,229,59,0.07), 0 32px 80px rgba(0,0,0,0.5); }
          50%       { box-shadow: 0 0 90px rgba(246,229,59,0.14), 0 32px 80px rgba(0,0,0,0.5); }
        }
      `}</style>

      <div
        className="mesh-bg"
        style={{
          minHeight: "100svh",
          display: "flex",
          alignItems: isMobile ? "flex-start" : "center",
          justifyContent: "center",
          padding: isMobile ? "0" : "28px 20px",
          overflow: "auto",
          position: "relative",
        }}
      >
        {/* ── Mesh orbs — yellow/gold palette ── */}
        <div
          className="mesh-orb"
          style={{
            width: isMobile ? 360 : 520,
            height: isMobile ? 360 : 520,
            background: "rgba(246,229,59,0.28)",
            top: "-110px",
            left: "-90px",
          }}
        />
        <div
          className="mesh-orb mesh-orb-2"
          style={{
            width: isMobile ? 300 : 420,
            height: isMobile ? 300 : 420,
            background: "rgba(240,180,48,0.2)",
            bottom: "-90px",
            right: "-70px",
          }}
        />
        <div
          className="mesh-orb mesh-orb-3"
          style={{
            width: isMobile ? 200 : 280,
            height: isMobile ? 200 : 280,
            background: "rgba(59,246,229,0.16)",
            top: "42%",
            left: "40%",
          }}
        />

        {/* Grain */}
        <div
          className="absolute inset-0 opacity-[0.048]"
          style={{
            backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`,
            backgroundSize: "180px",
            pointerEvents: "none",
          }}
        />

        {/* ── Card ── */}
        <div
          className="glass-card animate-fade-in"
          style={{
            position: "relative",
            zIndex: 1,
            width: "100%",
            maxWidth: 480,
            minHeight: isMobile ? "100svh" : "auto",
            borderRadius: isMobile ? 0 : 28,
            padding: cardPad,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            textAlign: "center",
            animation: isMobile ? "none" : "hgCardGlow 4s ease-in-out infinite",
            ...(isMobile
              ? { borderTop: "none", borderRight: "none", borderBottom: "none", borderLeft: "none" }
              : {}),
          }}
        >
          {/* Logo */}
          <div className="animate-fade-in-up" style={{ marginBottom: 28 }}>
            <img
              src={evaliaLogo}
              alt="Evalia"
              style={{ height: isMobile ? 40 : 48, width: "auto" }}
            />
          </div>

          {/* ── Hourglass icon ── */}
          <div
            className="animate-fade-in-up delay-100"
            style={{ position: "relative", display: "inline-flex", marginBottom: 8 }}
          >
            {/* Outer pulsing yellow ring */}
            <div
              style={{
                position: "absolute",
                inset: -14,
                borderRadius: "50%",
                background:
                  "radial-gradient(ellipse, rgba(246,229,59,0.2) 0%, rgba(246,180,48,0.08) 50%, transparent 72%)",
                filter: "blur(16px)",
                animation: "shimmer 3.5s ease-in-out infinite",
              }}
            />
            {/* Slow-spinning dashed ring */}
            <div
              style={{
                position: "absolute",
                top: -10,
                left: "50%",
                transform: "translateX(-50%)",
                width: iconSz + 20,
                height: (iconSz * 1.2) + 20,
                borderRadius: 99,
                borderTop:    "1px dashed rgba(246,229,59,0.25)",
                borderRight:  "1px dashed rgba(246,229,59,0.1)",
                borderBottom: "1px dashed rgba(246,229,59,0.25)",
                borderLeft:   "1px dashed rgba(246,229,59,0.1)",
                animation: "spin 12s linear infinite",
              }}
            />
            <HourglassIcon size={iconSz} />
          </div>

          {/* ── Title ── */}
          <h1
            className="animate-fade-in-up delay-200"
            style={{
              fontSize: headingFs,
              fontWeight: 800,
              color: "#f1f5f9",
              fontFamily: "Poppins, sans-serif",
              letterSpacing: "-0.025em",
              marginBottom: 14,
              lineHeight: 1.15,
            }}
          >
            Registro Completado
          </h1>

          {/* ── Status chip — yellow ── */}
          <div
            className="animate-fade-in-up delay-200"
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: 8,
              padding: "7px 18px",
              borderRadius: 99,
              background: "rgba(246,229,59,0.1)",
              borderTop:    "1px solid rgba(246,229,59,0.38)",
              borderRight:  "1px solid rgba(246,229,59,0.38)",
              borderBottom: "1px solid rgba(246,229,59,0.38)",
              borderLeft:   "1px solid rgba(246,229,59,0.38)",
              animation: "yellowPulse 3s ease-in-out infinite",
              marginBottom: 22,
            }}
          >
            {/* Pulsing dot */}
            <span
              style={{
                display: "inline-block",
                width: 8,
                height: 8,
                borderRadius: "50%",
                background: "#F6E53B",
                boxShadow: "0 0 8px #F6E53B, 0 0 16px rgba(246,229,59,0.6)",
                animation: "dotBlink 1.8s ease-in-out infinite",
                flexShrink: 0,
              }}
            />
            <span
              style={{
                fontSize: "0.78rem",
                fontWeight: 700,
                color: "#F6E53B",
                fontFamily: "Poppins, sans-serif",
                letterSpacing: "0.04em",
                textShadow: "0 0 12px rgba(246,229,59,0.5)",
              }}
            >
              Estado: Pendiente Validación
            </span>
          </div>

          {/* ── 3-step progress timeline ── */}
          <div
            className="animate-fade-in-up delay-300"
            style={{ width: "100%", marginBottom: 24 }}
          >
            {/* Row 1: circles + connectors */}
            <div style={{ display: "flex", alignItems: "center" }}>
              {STEPS.map((step, i) => (
                <div key={step.label} style={{ display: "contents" }}>
                  {/* Step circle */}
                  <div
                    style={{
                      width: 32,
                      height: 32,
                      borderRadius: "50%",
                      flexShrink: 0,
                      background: step.done
                        ? "rgba(246,229,59,0.18)"
                        : step.current
                        ? "rgba(246,229,59,0.1)"
                        : "rgba(255,255,255,0.05)",
                      borderTop:    `2px solid ${step.done || step.current ? "#F6E53B" : "rgba(255,255,255,0.1)"}`,
                      borderRight:  `2px solid ${step.done || step.current ? "#F6E53B" : "rgba(255,255,255,0.1)"}`,
                      borderBottom: `2px solid ${step.done || step.current ? "#F6E53B" : "rgba(255,255,255,0.1)"}`,
                      borderLeft:   `2px solid ${step.done || step.current ? "#F6E53B" : "rgba(255,255,255,0.1)"}`,
                      boxShadow: step.current ? "0 0 14px rgba(246,229,59,0.35)" : "none",
                      animation: step.current ? "yellowPulse 2.5s ease-in-out infinite" : "none",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    {step.done ? (
                      <CheckSm />
                    ) : step.current ? (
                      /* Mini hourglass icon */
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
                        <path d="M5 22h14M5 2h14M17 22v-4.2l-4-3.8-4 3.8V22M7 2v4.2l4 3.8 4-3.8V2"
                          stroke="#F6E53B" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    ) : null}
                  </div>

                  {/* Connector line (except after last) */}
                  {i < STEPS.length - 1 && (
                    <div
                      style={{
                        flex: 1,
                        height: 2,
                        borderRadius: 1,
                        background:
                          i === 0
                            ? "linear-gradient(90deg, #F6E53B 0%, rgba(246,229,59,0.35) 100%)"
                            : "rgba(255,255,255,0.07)",
                      }}
                    />
                  )}
                </div>
              ))}
            </div>

            {/* Row 2: labels */}
            <div style={{ display: "flex", marginTop: 8 }}>
              {STEPS.map((step) => (
                <div key={step.label} style={{ flex: 1 }}>
                  {step.label.split("\n").map((line) => (
                    <p
                      key={line}
                      style={{
                        fontSize: "0.6rem",
                        fontFamily: "Poppins, sans-serif",
                        fontWeight: step.current ? 700 : step.done ? 500 : 400,
                        color: step.done || step.current
                          ? "rgba(246,229,59,0.85)"
                          : "rgba(148,163,184,0.28)",
                        margin: 0,
                        lineHeight: 1.4,
                      }}
                    >
                      {line}
                    </p>
                  ))}
                </div>
              ))}
            </div>
          </div>

          {/* ── Explanation paragraph ── */}
          <p
            className="animate-fade-in-up delay-300"
            style={{
              fontSize: "0.84rem",
              color: "rgba(148,163,184,0.75)",
              fontFamily: "Poppins, sans-serif",
              fontWeight: 300,
              lineHeight: 1.7,
              maxWidth: 360,
              marginBottom: 22,
            }}
          >
            Tus datos y carta de autorización han sido enviados. Te notificaremos por correo electrónico una vez que un administrador{" "}
            <span style={{ color: "#F6E53B", fontWeight: 500 }}>apruebe tu cuenta</span>.
          </p>

          {/* ── Info cards ── */}
          <div
            className="animate-fade-in-up delay-400"
            style={{
              width: "100%",
              borderRadius: 16,
              overflow: "hidden",
              marginBottom: 28,
              borderTop:    "1px solid rgba(246,229,59,0.12)",
              borderRight:  "1px solid rgba(246,229,59,0.12)",
              borderBottom: "1px solid rgba(246,229,59,0.12)",
              borderLeft:   "1px solid rgba(246,229,59,0.12)",
              background: "rgba(246,229,59,0.04)",
            }}
          >
            {[
              {
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"
                      stroke="currentColor" strokeWidth="1.75" />
                    <polyline points="22,6 12,13 2,6" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
                  </svg>
                ),
                label: "Confirma tu correo electrónico",
                note: "Revisa también la carpeta de spam",
              },
              {
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="1.75" />
                    <polyline points="12,6 12,12 16,14" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
                  </svg>
                ),
                label: "Tiempo estimado de validación",
                note: "Entre 24 y 48 horas hábiles",
              },
              {
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0"
                      stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                ),
                label: "Recibirás una notificación",
                note: "Al correo registrado al aprobar tu acceso",
              },
            ].map(({ icon, label, note }, i, arr) => (
              <div
                key={label}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 14,
                  padding: "13px 16px",
                  textAlign: "left",
                  borderBottom:
                    i < arr.length - 1 ? "1px solid rgba(246,229,59,0.08)" : "none",
                }}
              >
                {/* Icon bubble */}
                <div
                  style={{
                    width: 36,
                    height: 36,
                    borderRadius: 10,
                    background: "rgba(246,229,59,0.1)",
                    borderTop:    "1px solid rgba(246,229,59,0.2)",
                    borderRight:  "1px solid rgba(246,229,59,0.2)",
                    borderBottom: "1px solid rgba(246,229,59,0.2)",
                    borderLeft:   "1px solid rgba(246,229,59,0.2)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    color: "#F6E53B",
                    flexShrink: 0,
                  }}
                >
                  {icon}
                </div>
                <div>
                  <p
                    style={{
                      fontSize: "0.78rem",
                      fontWeight: 600,
                      color: "rgba(241,245,249,0.85)",
                      fontFamily: "Poppins, sans-serif",
                      marginBottom: 2,
                    }}
                  >
                    {label}
                  </p>
                  <p
                    style={{
                      fontSize: "0.67rem",
                      color: "rgba(148,163,184,0.5)",
                      fontFamily: "Poppins, sans-serif",
                      fontWeight: 300,
                    }}
                  >
                    {note}
                  </p>
                </div>
              </div>
            ))}
          </div>

          {/* ── Ghost back button ── */}
          <button
            type="button"
            onClick={onBack}
            className="animate-fade-in-up delay-500"
            style={{
              width: "100%",
              padding: isMobile ? "13px 0" : "14px 0",
              borderRadius: 14,
              background: "rgba(255,255,255,0.05)",
              borderTop:    "1px solid rgba(255,255,255,0.12)",
              borderRight:  "1px solid rgba(255,255,255,0.12)",
              borderBottom: "1px solid rgba(255,255,255,0.12)",
              borderLeft:   "1px solid rgba(255,255,255,0.12)",
              color: "rgba(148,163,184,0.7)",
              fontWeight: 600,
              fontSize: "0.9rem",
              fontFamily: "Poppins, sans-serif",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              gap: 8,
              letterSpacing: "0.02em",
              transition: "all 0.25s ease",
              marginBottom: 16,
            }}
          >
            <BackArrow />
            Volver al Inicio de Sesión
          </button>

          {/* ── Support link ── */}
          <p
            className="animate-fade-in-up delay-600"
            style={{
              fontSize: "0.68rem",
              color: "rgba(148,163,184,0.32)",
              fontFamily: "Poppins, sans-serif",
              lineHeight: 1.5,
            }}
          >
            ¿Tienes dudas?{" "}
            <span
              style={{
                color: "rgba(246,229,59,0.45)",
                fontWeight: 500,
                cursor: "pointer",
                textDecoration: "underline",
                textDecorationColor: "rgba(246,229,59,0.2)",
              }}
            >
              Contáctanos
            </span>
          </p>
        </div>
      </div>
    </>
  );
}

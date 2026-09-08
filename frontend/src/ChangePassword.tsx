import { useState, useEffect } from "react";
import evaliaLogo from "@/imports/Disen_o_sin_ti_tulo.png";
import { changePassword } from "./auth/api";

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

/* ── Password strength ───────────────────────────────────────── */
function calcStrength(pass: string): { score: number; label: string; color: string } {
  if (!pass) return { score: 0, label: "", color: "" };
  const met = [
    pass.length >= 8,
    /[A-Z]/.test(pass),
    /[0-9]/.test(pass),
    /[^A-Za-z0-9]/.test(pass),
    pass.length >= 12,
  ].filter(Boolean).length;

  if (met <= 1) return { score: 1, label: "Débil",      color: "#EF4444" };
  if (met === 2) return { score: 2, label: "Regular",    color: "#F59E0B" };
  if (met === 3) return { score: 3, label: "Fuerte",     color: "#22c55e" };
  return          { score: 4, label: "Muy Fuerte",   color: "#3BF6E5" };
}

/* ── Icons ───────────────────────────────────────────────────── */
function EyeOpenIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke="currentColor" strokeWidth="1.75" />
      <circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="1.75" />
    </svg>
  );
}

function EyeOffIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none">
      <path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
      <path d="M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
      <path d="M14.12 14.12a3 3 0 01-4.24-4.24" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
      <line x1="1" y1="1" x2="23" y2="23" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function CheckSmall({ color }: { color: string }) {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" style={{ flexShrink: 0 }}>
      <path d="M20 6L9 17l-5-5" stroke={color} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function XSmall({ color }: { color: string }) {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" style={{ flexShrink: 0 }}>
      <path d="M18 6L6 18M6 6l12 12" stroke={color} strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  );
}

function BackArrow() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
      <path d="M19 12H5M5 12l7 7M5 12l7-7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* ── Shield header icon ──────────────────────────────────────── */
function ShieldIcon({ size = 64 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" fill="none">
      <defs>
        <linearGradient id="cpG1" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#E53BF6" />
          <stop offset="100%" stopColor="#3BF6E5" />
        </linearGradient>
        <linearGradient id="cpGFill" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="rgba(229,59,246,0.14)" />
          <stop offset="100%" stopColor="rgba(59,246,229,0.07)" />
        </linearGradient>
        <filter id="cpGlow" x="-35%" y="-35%" width="170%" height="170%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="3.5" result="blur" />
          <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        <filter id="cpHalo" x="-70%" y="-70%" width="240%" height="240%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="10" />
        </filter>
      </defs>

      {/* Outer halo */}
      <path d="M32 5L8 15v14c0 15.4 11.2 26 24 30 12.8-4 24-14.6 24-30V15L32 5z"
        fill="#a000c0" filter="url(#cpHalo)" opacity="0.55" />

      {/* Shield body */}
      <path d="M32 5L8 15v14c0 15.4 11.2 26 24 30 12.8-4 24-14.6 24-30V15L32 5z"
        fill="url(#cpGFill)" stroke="url(#cpG1)" strokeWidth="1.5" filter="url(#cpGlow)" />

      {/* Inner specular stripe */}
      <path d="M32 7L10 16.5v13c0 3 .8 6 2.2 8.8C18.4 26 29 20 40 21.5c1.4-2.1 2.2-4.4 2.4-6.8L32 7z"
        fill="rgba(255,255,255,0.05)" />

      {/* Check mark */}
      <path d="M20 33 l9 9 15-17"
        stroke="url(#cpG1)" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"
        filter="url(#cpGlow)" />
    </svg>
  );
}

/* ── Success shield icon ─────────────────────────────────────── */
function SuccessShield({ size = 80 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 80 80" fill="none">
      <defs>
        <linearGradient id="cpSG" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#E53BF6" />
          <stop offset="100%" stopColor="#3BF6E5" />
        </linearGradient>
        <filter id="cpSGlow" x="-35%" y="-35%" width="170%" height="170%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="4" result="blur" />
          <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        <filter id="cpSHalo" x="-60%" y="-60%" width="220%" height="220%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="12" />
        </filter>
      </defs>
      <path d="M40 6L10 19v17.5c0 19.2 14 32.5 30 37.5 16-5 30-18.3 30-37.5V19L40 6z"
        fill="#7000a0" filter="url(#cpSHalo)" opacity="0.55" />
      <path d="M40 6L10 19v17.5c0 19.2 14 32.5 30 37.5 16-5 30-18.3 30-37.5V19L40 6z"
        fill="rgba(229,59,246,0.09)" stroke="url(#cpSG)" strokeWidth="1.75" filter="url(#cpSGlow)" />
      <path d="M25 40l12 12 18-20"
        stroke="url(#cpSG)" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"
        filter="url(#cpSGlow)" />
    </svg>
  );
}

/* ── Password input with eye toggle ─────────────────────────── */
function PasswordInput({
  id, label, value, onChange, show, onToggle, disabled, valid, invalid,
}: {
  id: string; label: string; value: string;
  onChange: (v: string) => void;
  show: boolean; onToggle: () => void;
  disabled?: boolean;
  valid?: boolean; invalid?: boolean;
}) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;

  const borderColor = invalid
    ? "rgba(239,68,68,0.65)"
    : valid
    ? "rgba(34,197,94,0.6)"
    : focused
    ? "rgba(229,59,246,0.6)"
    : "rgba(255,255,255,0.1)";

  const bgColor = invalid
    ? "rgba(239,68,68,0.05)"
    : valid
    ? "rgba(34,197,94,0.05)"
    : focused
    ? "rgba(229,59,246,0.06)"
    : "rgba(255,255,255,0.04)";

  const shadowColor = invalid
    ? "rgba(239,68,68,0.12)"
    : valid
    ? "rgba(34,197,94,0.12)"
    : focused
    ? "rgba(229,59,246,0.12)"
    : "transparent";

  const labelColor = invalid
    ? "rgba(239,68,68,0.9)"
    : valid
    ? "rgba(34,197,94,0.9)"
    : active
    ? "rgba(229,59,246,0.9)"
    : "rgba(148,163,184,0.8)";

  return (
    <div style={{ position: "relative" }}>
      <input
        id={id}
        type={show ? "text" : "password"}
        value={value}
        disabled={disabled}
        autoComplete={id === "new-pass" ? "new-password" : "new-password"}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          background: bgColor,
          borderTop: `1px solid ${borderColor}`,
          borderRight: `1px solid ${borderColor}`,
          borderBottom: `1px solid ${borderColor}`,
          borderLeft: `1px solid ${borderColor}`,
          boxShadow: focused || invalid || valid ? `0 0 0 3px ${shadowColor}` : "none",
          color: "#f1f5f9",
          fontFamily: "Poppins, sans-serif",
          fontSize: "0.9375rem",
          padding: "1.25rem 3rem 0.5rem 1.125rem",
          borderRadius: 12,
          outline: "none",
          transition: "all 0.22s ease",
          boxSizing: "border-box",
          opacity: disabled ? 0.6 : 1,
        }}
      />
      <label
        htmlFor={id}
        style={{
          position: "absolute",
          left: "1.125rem",
          top: active ? "0.6rem" : "50%",
          transform: active ? "none" : "translateY(-50%)",
          fontSize: active ? "0.6875rem" : "0.9375rem",
          fontWeight: active ? 500 : 400,
          color: labelColor,
          letterSpacing: active ? "0.04em" : "normal",
          pointerEvents: "none",
          transition: "all 0.2s ease",
          fontFamily: "Poppins, sans-serif",
        }}
      >
        {label}
      </label>

      {/* Eye toggle */}
      <button
        type="button"
        onClick={onToggle}
        disabled={disabled}
        style={{
          position: "absolute",
          right: "0.9rem",
          top: "50%",
          transform: "translateY(-50%)",
          background: "none",
          border: "none",
          cursor: "pointer",
          color: "rgba(148,163,184,0.5)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          padding: 4,
          transition: "color 0.2s ease",
        }}
      >
        {show ? <EyeOffIcon /> : <EyeOpenIcon />}
      </button>
    </div>
  );
}

/* ── Strength bar + requirements ─────────────────────────────── */
function StrengthMeter({ password }: { password: string }) {
  const { score, label, color } = calcStrength(password);
  if (!password) return null;

  const requirements = [
    { label: "Mínimo 8 caracteres", met: password.length >= 8 },
    { label: "Una letra mayúscula", met: /[A-Z]/.test(password) },
    { label: "Un número (0-9)", met: /[0-9]/.test(password) },
    { label: "Un carácter especial", met: /[^A-Za-z0-9]/.test(password) },
  ];

  return (
    <div className="animate-fade-in" style={{ marginTop: 10 }}>
      {/* Bar segments + label */}
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
        <div style={{ display: "flex", gap: 4, flex: 1 }}>
          {[1, 2, 3, 4].map((seg) => (
            <div
              key={seg}
              style={{
                flex: 1,
                height: 5,
                borderRadius: 99,
                background: seg <= score ? color : "rgba(255,255,255,0.09)",
                transition: "background 0.35s ease",
                boxShadow: seg <= score ? `0 0 8px ${color}88` : "none",
              }}
            />
          ))}
        </div>
        <span
          style={{
            fontSize: "0.68rem",
            fontWeight: 700,
            color,
            fontFamily: "Poppins, sans-serif",
            minWidth: 68,
            textAlign: "right",
            letterSpacing: "0.02em",
            transition: "color 0.3s ease",
            textShadow: `0 0 10px ${color}88`,
          }}
        >
          {label}
        </span>
      </div>

      {/* Requirements 2-column grid */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "5px 16px" }}>
        {requirements.map((req) => (
          <div
            key={req.label}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              transition: "opacity 0.25s ease",
            }}
          >
            {req.met
              ? <CheckSmall color="#22c55e" />
              : <div style={{ width: 12, height: 12, borderRadius: "50%", border: "1.5px solid rgba(148,163,184,0.28)", flexShrink: 0 }} />
            }
            <span
              style={{
                fontSize: "0.62rem",
                color: req.met ? "rgba(34,197,94,0.85)" : "rgba(148,163,184,0.45)",
                fontFamily: "Poppins, sans-serif",
                fontWeight: req.met ? 500 : 400,
                transition: "color 0.25s ease",
                whiteSpace: "nowrap",
                overflow: "hidden",
                textOverflow: "ellipsis",
              }}
            >
              {req.label}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

/* ── Main screen ─────────────────────────────────────────────── */
export default function ChangePassword({
  onBack,
  email,
  recoveryCode,
}: {
  onBack: () => void;
  email: string;
  recoveryCode: string;
}) {
  const width    = useWidth();
  const isMobile = width < 768;

  const [newPass,      setNewPass]      = useState("");
  const [confirmPass,  setConfirmPass]  = useState("");
  const [showNew,      setShowNew]      = useState(false);
  const [showConfirm,  setShowConfirm]  = useState(false);
  const [status,       setStatus]       = useState<"idle" | "loading" | "success">("idle");
  const [error,        setError]        = useState("");

  const strength     = calcStrength(newPass);
  const isMatch      = newPass.length > 0 && confirmPass.length > 0 && newPass === confirmPass;
  const isMismatch   = confirmPass.length > 0 && newPass !== confirmPass;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    if (!newPass) { setError("Ingresa tu nueva contraseña."); return; }
    if (strength.score < 2) { setError("La contraseña es demasiado débil. Mejora su seguridad."); return; }
    if (!confirmPass) { setError("Confirma tu nueva contraseña."); return; }
    if (isMismatch) { setError("Las contraseñas no coinciden."); return; }
    setStatus("loading");
    try {
      await changePassword(email, recoveryCode, newPass);
      setStatus("success");
    } catch (changeError) {
      setStatus("idle");
      setError(changeError instanceof Error ? changeError.message : "No fue posible cambiar la contraseña.");
    }
  };

  const cardPad   = isMobile ? "28px 20px 32px" : "40px 36px 40px";
  const logoH     = isMobile ? 44 : 52;
  const shieldSz  = isMobile ? 58 : 66;
  const headingFs = isMobile ? "1.3rem" : "1.55rem";

  return (
    <div
      className="mesh-bg"
      style={{
        minHeight: "100svh",
        display: "flex",
        alignItems: isMobile ? "flex-start" : "center",
        justifyContent: "center",
        padding: isMobile ? "0" : "28px 20px",
        position: "relative",
        overflow: isMobile ? "auto" : "hidden",
      }}
    >
      {/* Mesh orbs */}
      <div className="mesh-orb" style={{ width: isMobile ? 340 : 480, height: isMobile ? 340 : 480, background: "rgba(229,59,246,0.46)", top: "-100px", left: "-80px" }} />
      <div className="mesh-orb mesh-orb-2" style={{ width: isMobile ? 300 : 400, height: isMobile ? 300 : 400, background: "rgba(59,246,229,0.34)", bottom: "-80px", right: "-60px" }} />
      <div className="mesh-orb mesh-orb-3" style={{ width: isMobile ? 220 : 270, height: isMobile ? 220 : 270, background: "rgba(99,59,246,0.28)", top: "40%", left: "40%" }} />

      {/* Grain */}
      <div
        className="absolute inset-0 opacity-[0.05]"
        style={{
          backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`,
          backgroundSize: "180px",
          pointerEvents: "none",
        }}
      />

      {/* Card */}
      <div
        className="glass-card animate-fade-in"
        style={{
          position: "relative",
          zIndex: 1,
          width: "100%",
          maxWidth: 464,
          minHeight: isMobile ? "100svh" : "auto",
          borderRadius: isMobile ? 0 : 28,
          padding: cardPad,
          display: "flex",
          flexDirection: "column",
          justifyContent: isMobile ? "center" : "flex-start",
          ...(isMobile ? { borderTop: "none", borderBottom: "none", borderLeft: "none", borderRight: "none" } : {}),
        }}
      >
        {/* Logo */}
        <div className="animate-fade-in-up" style={{ display: "flex", justifyContent: "center", marginBottom: 24 }}>
          <img src={evaliaLogo} alt="Evalia" style={{ height: logoH, width: "auto" }} />
        </div>

        {status === "success" ? (
          /* ── Success state ── */
          <div className="animate-fade-in" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center" }}>
            {/* Shield icon with ambient glow */}
            <div style={{ position: "relative", display: "inline-flex", alignItems: "center", justifyContent: "center", marginBottom: 20 }}>
              <div
                style={{
                  position: "absolute",
                  inset: -22,
                  borderRadius: "50%",
                  background: "radial-gradient(ellipse, rgba(229,59,246,0.22) 0%, rgba(59,246,229,0.12) 50%, transparent 70%)",
                  filter: "blur(14px)",
                  animation: "shimmer 3s ease-in-out infinite",
                }}
              />
              <SuccessShield size={isMobile ? 76 : 88} />
            </div>

            <h2
              style={{
                fontSize: headingFs,
                fontWeight: 800,
                color: "#f1f5f9",
                fontFamily: "Poppins, sans-serif",
                letterSpacing: "-0.02em",
                marginBottom: 6,
                background: "linear-gradient(135deg, #E53BF6, #3BF6E5)",
                WebkitBackgroundClip: "text",
                WebkitTextFillColor: "transparent",
                backgroundClip: "text",
              }}
            >
              ¡Contraseña Actualizada!
            </h2>
            <p
              style={{
                fontSize: "0.83rem",
                color: "rgba(148,163,184,0.78)",
                fontFamily: "Poppins, sans-serif",
                fontWeight: 300,
                lineHeight: 1.6,
                marginBottom: 10,
                maxWidth: 300,
              }}
            >
              Tu contraseña ha sido actualizada exitosamente. Ahora puedes iniciar sesión con tu nueva contraseña.
            </p>

            {/* Security note */}
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                padding: "10px 16px",
                borderRadius: 12,
                background: "rgba(34,197,94,0.06)",
                borderTop: "1px solid rgba(34,197,94,0.2)",
                borderRight: "1px solid rgba(34,197,94,0.2)",
                borderBottom: "1px solid rgba(34,197,94,0.2)",
                borderLeft: "1px solid rgba(34,197,94,0.2)",
                margin: "12px 0 28px",
                width: "100%",
              }}
            >
              <CheckSmall color="#22c55e" />
              <span style={{ fontSize: "0.72rem", color: "rgba(34,197,94,0.85)", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>
                Todas las sesiones anteriores han sido cerradas por seguridad.
              </span>
            </div>

            <button
              onClick={onBack}
              className="gradient-btn"
              style={{
                width: "100%",
                padding: "14px 0",
                borderRadius: 14,
                color: "white",
                fontWeight: 700,
                fontSize: "0.93rem",
                fontFamily: "Poppins, sans-serif",
                border: "none",
                cursor: "pointer",
                letterSpacing: "0.04em",
              }}
            >
              Ir al Inicio de Sesión
            </button>
          </div>
        ) : (
          /* ── Form state ── */
          <>
            {/* Shield icon + heading */}
            <div className="animate-fade-in-up delay-100" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", marginBottom: 24 }}>
              <div
                style={{
                  position: "relative",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  marginBottom: 16,
                }}
              >
                <div
                  style={{
                    position: "absolute",
                    inset: -18,
                    borderRadius: "50%",
                    background: "radial-gradient(ellipse, rgba(229,59,246,0.22) 0%, rgba(59,246,229,0.1) 55%, transparent 70%)",
                    filter: "blur(12px)",
                    animation: "shimmer 3s ease-in-out infinite",
                  }}
                />
                <div
                  style={{
                    position: "absolute",
                    inset: -5,
                    borderRadius: "50%",
                    borderTop: "1px solid rgba(229,59,246,0.2)",
                    borderRight: "1px solid rgba(59,246,229,0.2)",
                    borderBottom: "1px solid rgba(229,59,246,0.2)",
                    borderLeft: "1px solid rgba(59,246,229,0.2)",
                    animation: "glowPulse 3s ease-in-out infinite",
                  }}
                />
                <ShieldIcon size={shieldSz} />
              </div>

              <h2
                style={{
                  fontSize: headingFs,
                  fontWeight: 800,
                  color: "#f1f5f9",
                  fontFamily: "Poppins, sans-serif",
                  letterSpacing: "-0.02em",
                  marginBottom: 6,
                }}
              >
                Crear Nueva Contraseña
              </h2>
              <p
                style={{
                  fontSize: "0.78rem",
                  color: "rgba(148,163,184,0.68)",
                  fontFamily: "Poppins, sans-serif",
                  fontWeight: 300,
                  lineHeight: 1.5,
                  maxWidth: 290,
                }}
              >
                Elige una contraseña segura para proteger tu cuenta.
              </p>
            </div>

            {/* Form */}
            <form
              onSubmit={handleSubmit}
              className="animate-fade-in-up delay-200"
              style={{ display: "flex", flexDirection: "column", gap: 0 }}
            >
              {/* New password field */}
              <PasswordInput
                id="new-pass"
                label="Nueva Contraseña"
                value={newPass}
                onChange={(v) => { setNewPass(v); setError(""); }}
                show={showNew}
                onToggle={() => setShowNew(!showNew)}
                disabled={status === "loading"}
              />

              {/* Strength meter */}
              <div style={{ marginBottom: 16 }}>
                <StrengthMeter password={newPass} />
              </div>

              {/* Confirm password field */}
              <div style={{ position: "relative" }}>
                <PasswordInput
                  id="confirm-pass"
                  label="Confirmar Contraseña"
                  value={confirmPass}
                  onChange={(v) => { setConfirmPass(v); setError(""); }}
                  show={showConfirm}
                  onToggle={() => setShowConfirm(!showConfirm)}
                  disabled={status === "loading"}
                  valid={isMatch}
                  invalid={isMismatch}
                />
                {/* Match/mismatch indicator badge */}
                {confirmPass.length > 0 && (
                  <div
                    className="animate-fade-in"
                    style={{
                      position: "absolute",
                      right: "2.8rem",
                      top: "50%",
                      transform: "translateY(-50%)",
                      display: "flex",
                      alignItems: "center",
                    }}
                  >
                    {isMatch
                      ? <CheckSmall color="#22c55e" />
                      : <XSmall color="#EF4444" />
                    }
                  </div>
                )}
              </div>

              {/* Mismatch hint */}
              {isMismatch && (
                <p
                  className="animate-fade-in"
                  style={{
                    fontSize: "0.68rem",
                    color: "#EF4444",
                    fontFamily: "Poppins, sans-serif",
                    marginTop: 6,
                    marginLeft: 4,
                    fontWeight: 500,
                  }}
                >
                  Las contraseñas no coinciden
                </p>
              )}

              {/* Spacer */}
              <div style={{ height: 16 }} />

              {/* Error message */}
              {error && (
                <div
                  className="animate-fade-in"
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 7,
                    padding: "10px 14px",
                    borderRadius: 10,
                    background: "rgba(229,59,246,0.07)",
                    borderTop: "1px solid rgba(229,59,246,0.22)",
                    borderRight: "1px solid rgba(229,59,246,0.22)",
                    borderBottom: "1px solid rgba(229,59,246,0.22)",
                    borderLeft: "1px solid rgba(229,59,246,0.22)",
                    marginBottom: 14,
                  }}
                >
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
                    <path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="#E53BF6" strokeWidth="2" strokeLinecap="round" />
                  </svg>
                  <span style={{ fontSize: "0.74rem", color: "#E53BF6", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>{error}</span>
                </div>
              )}

              {/* Submit button */}
              <button
                type="submit"
                disabled={status === "loading"}
                className="gradient-btn"
                style={{
                  width: "100%",
                  padding: isMobile ? "14px 0" : "15px 0",
                  borderRadius: 14,
                  color: "white",
                  fontWeight: 700,
                  fontSize: "0.93rem",
                  fontFamily: "Poppins, sans-serif",
                  border: "none",
                  cursor: status === "loading" ? "not-allowed" : "pointer",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  gap: 8,
                  letterSpacing: "0.04em",
                  opacity: status === "loading" ? 0.8 : 1,
                }}
              >
                {status === "loading" ? (
                  <>
                    <span
                      style={{
                        display: "inline-block",
                        width: 16,
                        height: 16,
                        borderRadius: "50%",
                        borderTop: "2px solid white",
                        borderRight: "2px solid rgba(255,255,255,0.3)",
                        borderBottom: "2px solid rgba(255,255,255,0.3)",
                        borderLeft: "2px solid rgba(255,255,255,0.3)",
                        animation: "spin 0.7s linear infinite",
                      }}
                    />
                    Guardando…
                  </>
                ) : (
                  <>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                      <path d="M19 21H5a2 2 0 01-2-2V5a2 2 0 012-2h11l5 5v11a2 2 0 01-2 2z" stroke="white" strokeWidth="1.75" />
                      <path d="M17 21v-8H7v8M7 3v5h8" stroke="white" strokeWidth="1.75" strokeLinecap="round" />
                    </svg>
                    Guardar Cambios
                  </>
                )}
              </button>
            </form>

            {/* Back link */}
            <button
              type="button"
              onClick={onBack}
              className="animate-fade-in-up delay-300"
              style={{
                marginTop: 18,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                gap: 7,
                background: "none",
                border: "none",
                cursor: "pointer",
                color: "rgba(148,163,184,0.55)",
                fontSize: "0.79rem",
                fontFamily: "Poppins, sans-serif",
                fontWeight: 400,
                padding: "4px 0",
                width: "100%",
                transition: "color 0.2s ease",
              }}
            >
              <BackArrow />
              Volver al Inicio de Sesión
            </button>
          </>
        )}
      </div>
    </div>
  );
}

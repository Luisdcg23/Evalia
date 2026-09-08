import { useState, useEffect, useRef, useCallback } from "react";
import evaliaLogo from "@/imports/Disen_o_sin_ti_tulo.png";
import { requestPasswordRecovery } from "./auth/api";

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

/* ── Floating email input ────────────────────────────────────── */
function EmailInput({ value, onChange, disabled }: { value: string; onChange: (v: string) => void; disabled?: boolean }) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;
  return (
    <div style={{ position: "relative" }}>
      <input
        id="fp-email"
        type="email"
        value={value}
        disabled={disabled}
        autoComplete="email"
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          background: focused ? "rgba(59,246,229,0.05)" : "rgba(255,255,255,0.04)",
          borderTop: focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.1)",
          borderRight: focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.1)",
          borderBottom: focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.1)",
          borderLeft: focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.1)",
          boxShadow: focused ? "0 0 0 3px rgba(59,246,229,0.1)" : "none",
          color: "#f1f5f9",
          fontFamily: "Poppins, sans-serif",
          fontSize: "0.9375rem",
          padding: "1.25rem 1.125rem 0.5rem",
          borderRadius: 12,
          outline: "none",
          transition: "all 0.22s ease",
          boxSizing: "border-box",
          opacity: disabled ? 0.6 : 1,
        }}
      />
      <label
        htmlFor="fp-email"
        style={{
          position: "absolute",
          left: "1.125rem",
          top: active ? "0.6rem" : "50%",
          transform: active ? "none" : "translateY(-50%)",
          fontSize: active ? "0.6875rem" : "0.9375rem",
          fontWeight: active ? 500 : 400,
          color: active ? "rgba(59,246,229,0.9)" : "rgba(148,163,184,0.8)",
          letterSpacing: active ? "0.04em" : "normal",
          pointerEvents: "none",
          transition: "all 0.2s ease",
          fontFamily: "Poppins, sans-serif",
        }}
      >
        Correo Electrónico
      </label>
    </div>
  );
}

/* ── Lock icon SVG ───────────────────────────────────────────── */
function LockIcon({ size = 80 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 80 80" fill="none" style={{ display: "block" }}>
      <defs>
        <linearGradient id="fpBodyGrad" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="#f472ff" />
          <stop offset="45%" stopColor="#E53BF6" />
          <stop offset="100%" stopColor="#8b00a0" />
        </linearGradient>
        <linearGradient id="fpShackleGrad" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="#f472ff" />
          <stop offset="100%" stopColor="#E53BF6" />
        </linearGradient>
        <linearGradient id="fpShineGrad" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="rgba(255,255,255,0.28)" />
          <stop offset="100%" stopColor="rgba(255,255,255,0)" />
        </linearGradient>
        <radialGradient id="fpEdgeGrad" cx="30%" cy="20%" r="70%">
          <stop offset="0%" stopColor="rgba(255,255,255,0.12)" />
          <stop offset="100%" stopColor="rgba(0,0,0,0)" />
        </radialGradient>
        <filter id="fpGlow" x="-35%" y="-35%" width="170%" height="170%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="4" result="blur" />
          <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        <filter id="fpHalo" x="-60%" y="-60%" width="220%" height="220%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="9" />
        </filter>
      </defs>
      <rect x="10" y="38" width="60" height="38" rx="12" fill="#E53BF6" filter="url(#fpHalo)" opacity="0.55" />
      <path d="M24 38 L24 22 A16 16 0 0 1 56 22 L56 38" stroke="#E53BF6" strokeWidth="7" strokeLinecap="round" fill="none" filter="url(#fpHalo)" opacity="0.4" />
      <path d="M24 38 L24 22 A16 16 0 0 1 56 22 L56 38" stroke="url(#fpShackleGrad)" strokeWidth="6.5" strokeLinecap="round" fill="none" filter="url(#fpGlow)" />
      <rect x="10" y="38" width="60" height="38" rx="12" fill="url(#fpBodyGrad)" filter="url(#fpGlow)" />
      <rect x="10" y="38" width="60" height="38" rx="12" fill="url(#fpEdgeGrad)" />
      <rect x="10" y="38" width="60" height="18" rx="12" fill="url(#fpShineGrad)" />
      <rect x="10" y="50" width="60" height="6" fill="url(#fpShineGrad)" />
      <circle cx="40" cy="53" r="6.5" fill="rgba(12,20,40,0.78)" />
      <path d="M37 56 L37 65 Q37 67 40 67 Q43 67 43 65 L43 56 Z" fill="rgba(12,20,40,0.78)" />
      <circle cx="38" cy="51" r="2" fill="rgba(255,255,255,0.09)" />
    </svg>
  );
}

/* ── Email sent icon ─────────────────────────────────────────── */
function EmailSentIcon({ size = 72 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 72 72" fill="none">
      <defs>
        <linearGradient id="esG" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#3BF6E5" />
          <stop offset="100%" stopColor="#06b6d4" />
        </linearGradient>
        <filter id="esGlow" x="-35%" y="-35%" width="170%" height="170%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="3.5" result="b" />
          <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        <filter id="esHalo" x="-55%" y="-55%" width="210%" height="210%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="10" />
        </filter>
      </defs>
      <circle cx="36" cy="36" r="32" fill="#3BF6E5" filter="url(#esHalo)" opacity="0.35" />
      <circle cx="36" cy="36" r="30" stroke="url(#esG)" strokeWidth="1.5" fill="rgba(59,246,229,0.07)" filter="url(#esGlow)" />
      <rect x="16" y="24" width="40" height="28" rx="4" stroke="url(#esG)" strokeWidth="1.75" fill="none" filter="url(#esGlow)" />
      <path d="M16 28l20 14 20-14" stroke="url(#esG)" strokeWidth="1.75" strokeLinecap="round" fill="none" filter="url(#esGlow)" />
    </svg>
  );
}

/* ── Back arrow ──────────────────────────────────────────────── */
function BackArrow() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
      <path d="M19 12H5M5 12l7 7M5 12l7-7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* ── Main screen ─────────────────────────────────────────────── */
export default function ForgotPassword({
  onBack,
  onChangePassword,
}: {
  onBack: () => void;
  onChangePassword: (email: string, recoveryCode: string) => void;
}) {
  const width    = useWidth();
  const isMobile = width < 768;

  const [email,  setEmail]  = useState("");
  const [status, setStatus] = useState<"idle" | "sending" | "code" | "verifying">("idle");
  const [error,  setError]  = useState("");
  const [issuedCode, setIssuedCode] = useState<string | null>(null);

  /* OTP for code step */
  const [otp,   setOtp]   = useState(["", "", "", ""]);
  const [shake, setShake] = useState(false);
  const cellRefs = useRef<(HTMLInputElement | null)[]>(Array(4).fill(null));

  /* Resend countdown */
  const [countdown, setCountdown] = useState(0);
  useEffect(() => {
    if (countdown <= 0) return;
    const t = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(t);
  }, [countdown]);

  /* Auto-verify when all 4 cells filled */
  const otpRef = useRef(otp);
  otpRef.current = otp;

  const doVerify = useCallback(() => {
    const code = otpRef.current.join("");
    if (issuedCode !== null && code === issuedCode) {
      setStatus("verifying");
      onChangePassword(email.trim().toLowerCase(), code);
    } else {
      setShake(true);
      setError("Código incorrecto. Inténtalo de nuevo.");
      setOtp(["", "", "", ""]);
      setTimeout(() => {
        setShake(false);
        cellRefs.current[0]?.focus();
      }, 480);
    }
  }, [email, issuedCode, onChangePassword]);

  useEffect(() => {
    if (status !== "code" || shake || !otp.every((d) => d !== "")) return;
    const t = setTimeout(doVerify, 340);
    return () => clearTimeout(t);
  }, [otp, status, shake, doVerify]);

  /* Email submit */
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) { setError("Ingresa tu correo electrónico."); return; }
    if (!email.includes("@") || !email.includes(".")) { setError("El correo no es válido."); return; }
    setError("");
    setStatus("sending");
    try {
      const recoveryCode = await requestPasswordRecovery(email);
      setIssuedCode(recoveryCode);
      setStatus("code");
      setCountdown(30);
      setTimeout(() => cellRefs.current[0]?.focus(), 80);
      if (recoveryCode === null) {
        setError("Si la cuenta existe, el código fue generado por el canal configurado.");
      }
    } catch (recoveryError) {
      setStatus("idle");
      setError(recoveryError instanceof Error ? recoveryError.message : "No fue posible generar el código.");
    }
  };

  /* OTP cell handlers */
  const handleCellChange = (i: number, value: string) => {
    const digit = value.replace(/\D/g, "").slice(-1);
    setError("");
    const next = [...otp];
    next[i] = digit;
    setOtp(next);
    if (digit && i < 3) cellRefs.current[i + 1]?.focus();
  };

  const handleCellKeyDown = (i: number, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Backspace") {
      if (otp[i]) {
        const next = [...otp]; next[i] = ""; setOtp(next);
      } else if (i > 0) {
        cellRefs.current[i - 1]?.focus();
      }
    }
    if (e.key === "ArrowLeft" && i > 0) cellRefs.current[i - 1]?.focus();
    if (e.key === "ArrowRight" && i < 3) cellRefs.current[i + 1]?.focus();
  };

  const handlePaste = (e: React.ClipboardEvent) => {
    e.preventDefault();
    const digits = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 4).split("");
    const next = ["", "", "", ""];
    digits.forEach((d, i) => { next[i] = d; });
    setOtp(next);
    const lastFilled = Math.min(digits.length, 3);
    cellRefs.current[lastFilled]?.focus();
  };

  const allFilled = otp.every((d) => d !== "");

  const cardPad   = isMobile ? "32px 22px 28px" : "44px 40px 40px";
  const logoH     = isMobile ? 44 : 52;
  const headingFs = isMobile ? "1.35rem" : "1.6rem";
  const lockSize  = isMobile ? 72 : 84;

  return (
    <>
      <style>{`
        @keyframes fpShake {
          0%,100% { transform: translateX(0); }
          15%     { transform: translateX(-8px); }
          30%     { transform: translateX(8px); }
          45%     { transform: translateX(-6px); }
          60%     { transform: translateX(6px); }
          75%     { transform: translateX(-3px); }
          90%     { transform: translateX(3px); }
        }
      `}</style>

      <div
        className="mesh-bg"
        style={{
          minHeight: "100svh",
          display: "flex",
          alignItems: isMobile ? "flex-start" : "center",
          justifyContent: "center",
          padding: isMobile ? 0 : "32px 20px",
          position: "relative",
          overflow: "hidden",
        }}
      >
        {/* Mesh orbs */}
        <div className="mesh-orb"        style={{ width: isMobile ? 340 : 480, height: isMobile ? 340 : 480, background: "rgba(229,59,246,0.48)", top: "-100px", left: "-80px" }} />
        <div className="mesh-orb mesh-orb-2" style={{ width: isMobile ? 300 : 400, height: isMobile ? 300 : 400, background: "rgba(59,246,229,0.35)", bottom: "-80px", right: "-60px" }} />
        <div className="mesh-orb mesh-orb-3" style={{ width: isMobile ? 220 : 280, height: isMobile ? 220 : 280, background: "rgba(140,59,246,0.28)", top: "50%", left: "38%" }} />

        {/* Grain */}
        <div className="absolute inset-0 opacity-[0.05]" style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`, backgroundSize: "180px", pointerEvents: "none" }} />

        {/* Card */}
        <div
          className="glass-card animate-fade-in"
          style={{
            position: "relative",
            zIndex: 1,
            width: "100%",
            maxWidth: 460,
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
          <div className="animate-fade-in-up" style={{ display: "flex", justifyContent: "center", marginBottom: 28 }}>
            <img src={evaliaLogo} alt="Evalia" style={{ height: logoH, width: "auto" }} />
          </div>

          {/* ══ PASO 1: EMAIL ══ */}
          {(status === "idle" || status === "sending") && (
            <>
              <div className="animate-fade-in-up delay-100" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", marginBottom: 28 }}>
                <div style={{ position: "relative", display: "inline-flex", alignItems: "center", justifyContent: "center", marginBottom: 20 }}>
                  <div style={{ position: "absolute", inset: -22, borderRadius: "50%", background: "radial-gradient(ellipse, rgba(229,59,246,0.28) 0%, transparent 65%)", filter: "blur(14px)", animation: "shimmer 3s ease-in-out infinite" }} />
                  <div style={{ position: "absolute", inset: -6, borderRadius: "50%", borderTop: "1px solid rgba(229,59,246,0.25)", borderRight: "1px solid rgba(229,59,246,0.25)", borderBottom: "1px solid rgba(229,59,246,0.25)", borderLeft: "1px solid rgba(229,59,246,0.25)", animation: "glowPulse 2.8s ease-in-out infinite" }} />
                  <LockIcon size={lockSize} />
                </div>
                <h2 style={{ fontSize: headingFs, fontWeight: 800, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", letterSpacing: "-0.02em", marginBottom: 8 }}>
                  Recuperar Contraseña
                </h2>
                <p style={{ fontSize: "0.82rem", color: "rgba(148,163,184,0.72)", fontFamily: "Poppins, sans-serif", fontWeight: 300, lineHeight: 1.55, maxWidth: 300 }}>
                  Ingresa tu correo y te enviaremos un código de verificación para restablecer tu contraseña.
                </p>
              </div>

              <form onSubmit={handleSubmit} className="animate-fade-in-up delay-200" style={{ display: "flex", flexDirection: "column", gap: 14 }}>
                <EmailInput value={email} onChange={(v) => { setEmail(v); setError(""); }} disabled={status === "sending"} />

                {error && (
                  <div style={{ display: "flex", alignItems: "center", gap: 7, padding: "10px 14px", borderRadius: 10, background: "rgba(229,59,246,0.07)", borderTop: "1px solid rgba(229,59,246,0.22)", borderRight: "1px solid rgba(229,59,246,0.22)", borderBottom: "1px solid rgba(229,59,246,0.22)", borderLeft: "1px solid rgba(229,59,246,0.22)" }}>
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="#E53BF6" strokeWidth="2" strokeLinecap="round" /></svg>
                    <span style={{ fontSize: "0.75rem", color: "#E53BF6", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>{error}</span>
                  </div>
                )}

                <button
                  type="submit"
                  disabled={status === "sending"}
                  style={{
                    width: "100%", padding: isMobile ? "14px 0" : "15px 0", borderRadius: 14,
                    background: status === "sending" ? "rgba(59,246,229,0.5)" : "linear-gradient(135deg, #3BF6E5 0%, #22c7e5 100%)",
                    border: "none", cursor: status === "sending" ? "not-allowed" : "pointer",
                    display: "flex", alignItems: "center", justifyContent: "center", gap: 8,
                    color: "#0F172A", fontWeight: 700, fontSize: "0.93rem",
                    fontFamily: "Poppins, sans-serif", letterSpacing: "0.04em", marginTop: 4,
                    boxShadow: status === "sending" ? "none" : "0 0 28px 6px rgba(59,246,229,0.38), 0 2px 16px rgba(0,0,0,0.25)",
                    transition: "all 0.25s ease",
                  }}
                >
                  {status === "sending" ? (
                    <>
                      <span style={{ display: "inline-block", width: 16, height: 16, borderRadius: "50%", borderTop: "2px solid #0F172A", borderRight: "2px solid rgba(15,23,42,0.3)", borderBottom: "2px solid rgba(15,23,42,0.3)", borderLeft: "2px solid rgba(15,23,42,0.3)", animation: "spin 0.7s linear infinite" }} />
                      Enviando…
                    </>
                  ) : (
                    <>
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M22 2L11 13" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /><path d="M22 2L15 22l-4-9-9-4 20-7z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>
                      Enviar Código de Verificación
                    </>
                  )}
                </button>
              </form>

              <button
                type="button"
                onClick={onBack}
                className="animate-fade-in-up delay-400"
                style={{ marginTop: 20, display: "flex", alignItems: "center", justifyContent: "center", gap: 7, background: "none", border: "none", cursor: "pointer", color: "rgba(148,163,184,0.6)", fontSize: "0.8rem", fontFamily: "Poppins, sans-serif", fontWeight: 400, padding: "6px 0", width: "100%" }}
              >
                <BackArrow />
                Volver al Inicio de Sesión
              </button>
            </>
          )}

          {/* ══ PASO 2: CÓDIGO ══ */}
          {(status === "code" || status === "verifying") && (
            <div className="animate-fade-in">
              {/* Header */}
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", marginBottom: 28 }}>
                <div style={{ position: "relative", display: "inline-flex", alignItems: "center", justifyContent: "center", marginBottom: 20 }}>
                  <div style={{ position: "absolute", inset: -20, borderRadius: "50%", background: "radial-gradient(ellipse, rgba(59,246,229,0.22) 0%, transparent 70%)", filter: "blur(12px)", animation: "shimmer 3s ease-in-out infinite" }} />
                  <EmailSentIcon size={isMobile ? 64 : 76} />
                </div>
                <h2 style={{ fontSize: headingFs, fontWeight: 800, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", letterSpacing: "-0.02em", marginBottom: 8 }}>
                  Código Enviado
                </h2>
                <p style={{ fontSize: "0.82rem", color: "rgba(148,163,184,0.72)", fontFamily: "Poppins, sans-serif", fontWeight: 300, lineHeight: 1.55, maxWidth: 300 }}>
                  Ingresa el código de 4 dígitos generado para{" "}
                  <span style={{ color: "#3BF6E5", fontWeight: 600 }}>{email}</span>
                </p>
                {issuedCode && (
                  <p style={{ marginTop: 10, color: "#F6E53B", fontSize: "0.78rem", fontFamily: "Poppins, sans-serif" }}>
                    Código local: <strong style={{ letterSpacing: ".18em" }}>{issuedCode}</strong>
                  </p>
                )}
              </div>

              {/* OTP cells */}
              <div
                style={{
                  display: "flex",
                  gap: isMobile ? 10 : 14,
                  justifyContent: "center",
                  marginBottom: 18,
                  animation: shake ? "fpShake 0.48s ease-in-out" : "none",
                }}
              >
                {otp.map((digit, i) => (
                  <input
                    key={i}
                    ref={(el) => { cellRefs.current[i] = el; }}
                    type="text"
                    inputMode="numeric"
                    maxLength={1}
                    value={digit}
                    disabled={status === "verifying"}
                    onChange={(e) => handleCellChange(i, e.target.value)}
                    onKeyDown={(e) => handleCellKeyDown(i, e)}
                    onPaste={handlePaste}
                    style={{
                      width: isMobile ? 62 : 72,
                      height: isMobile ? 72 : 84,
                      textAlign: "center",
                      fontSize: "1.75rem",
                      fontWeight: 700,
                      fontFamily: "Poppins, sans-serif",
                      color: digit ? "#3BF6E5" : "#94a3b8",
                      background: digit ? "rgba(59,246,229,0.07)" : "rgba(255,255,255,0.04)",
                      borderTop:    digit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)",
                      borderRight:  digit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)",
                      borderBottom: digit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)",
                      borderLeft:   digit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)",
                      borderRadius: 14,
                      outline: "none",
                      boxShadow: digit ? "0 0 18px rgba(59,246,229,0.22), inset 0 0 0 1px rgba(59,246,229,0.1)" : "none",
                      transition: "all 0.18s ease",
                      cursor: "text",
                      opacity: status === "verifying" ? 0.7 : 1,
                    }}
                    onFocus={(e) => {
                      e.target.style.borderTop = "2px solid rgba(59,246,229,0.9)";
                      e.target.style.borderRight = "2px solid rgba(59,246,229,0.9)";
                      e.target.style.borderBottom = "2px solid rgba(59,246,229,0.9)";
                      e.target.style.borderLeft = "2px solid rgba(59,246,229,0.9)";
                      e.target.style.boxShadow = "0 0 0 3px rgba(59,246,229,0.15), 0 0 24px rgba(59,246,229,0.3)";
                    }}
                    onBlur={(e) => {
                      const hasDigit = e.target.value !== "";
                      e.target.style.borderTop    = hasDigit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)";
                      e.target.style.borderRight  = hasDigit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)";
                      e.target.style.borderBottom = hasDigit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)";
                      e.target.style.borderLeft   = hasDigit ? "2px solid rgba(59,246,229,0.7)" : "1.5px solid rgba(255,255,255,0.12)";
                      e.target.style.boxShadow    = hasDigit ? "0 0 18px rgba(59,246,229,0.22), inset 0 0 0 1px rgba(59,246,229,0.1)" : "none";
                    }}
                  />
                ))}
              </div>

              {/* Error */}
              {error && (
                <div className="animate-fade-in" style={{ display: "flex", alignItems: "center", gap: 7, padding: "10px 14px", borderRadius: 10, background: "rgba(229,59,246,0.07)", borderTop: "1px solid rgba(229,59,246,0.22)", borderRight: "1px solid rgba(229,59,246,0.22)", borderBottom: "1px solid rgba(229,59,246,0.22)", borderLeft: "1px solid rgba(229,59,246,0.22)", marginBottom: 14 }}>
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="#E53BF6" strokeWidth="2" strokeLinecap="round" /></svg>
                  <span style={{ fontSize: "0.75rem", color: "#E53BF6", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>{error}</span>
                </div>
              )}

              {/* Verify button */}
              <button
                type="button"
                onClick={doVerify}
                disabled={!allFilled || status === "verifying"}
                style={{
                  width: "100%", padding: isMobile ? "14px 0" : "15px 0", borderRadius: 14,
                  background: allFilled && status !== "verifying"
                    ? "linear-gradient(135deg, #3BF6E5 0%, #22c7e5 60%, #06b6d4 100%)"
                    : "rgba(255,255,255,0.06)",
                  border: "none",
                  cursor: allFilled && status !== "verifying" ? "pointer" : "not-allowed",
                  display: "flex", alignItems: "center", justifyContent: "center", gap: 8,
                  color: allFilled && status !== "verifying" ? "#0F172A" : "rgba(148,163,184,0.4)",
                  fontWeight: 700, fontSize: "0.93rem", fontFamily: "Poppins, sans-serif",
                  letterSpacing: "0.04em",
                  boxShadow: allFilled && status !== "verifying" ? "0 0 28px 6px rgba(59,246,229,0.35), 0 2px 16px rgba(0,0,0,0.25)" : "none",
                  transition: "all 0.25s ease",
                  marginBottom: 12,
                }}
              >
                {status === "verifying" ? (
                  <>
                    <span style={{ display: "inline-block", width: 16, height: 16, borderRadius: "50%", borderTop: "2px solid rgba(148,163,184,0.5)", borderRight: "2px solid rgba(255,255,255,0.08)", borderBottom: "2px solid rgba(255,255,255,0.08)", borderLeft: "2px solid rgba(255,255,255,0.08)", animation: "spin 0.7s linear infinite" }} />
                    Verificando…
                  </>
                ) : "Verificar Código"}
              </button>

              {/* Resend + back */}
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <button
                  type="button"
                  onClick={onBack}
                  style={{ background: "none", border: "none", cursor: "pointer", color: "rgba(148,163,184,0.45)", fontSize: "0.75rem", fontFamily: "Poppins, sans-serif", display: "flex", alignItems: "center", gap: 5, padding: "6px 0" }}
                >
                  <BackArrow />
                  Volver
                </button>
                {countdown > 0 ? (
                  <span style={{ fontSize: "0.72rem", color: "rgba(148,163,184,0.4)", fontFamily: "Poppins, sans-serif" }}>
                    Reenviar en <span style={{ color: "#3BF6E5", fontWeight: 600 }}>{countdown}s</span>
                  </span>
                ) : (
                  <button
                    type="button"
                    onClick={async () => {
                      setCountdown(30);
                      setError("");
                      setOtp(["", "", "", ""]);
                      setIssuedCode(await requestPasswordRecovery(email));
                      setTimeout(() => cellRefs.current[0]?.focus(), 60);
                    }}
                    style={{ background: "none", border: "none", cursor: "pointer", fontSize: "0.75rem", fontFamily: "Poppins, sans-serif", fontWeight: 600, background: "linear-gradient(135deg, #E53BF6, #3BF6E5)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text" }}
                  >
                    Reenviar código
                  </button>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

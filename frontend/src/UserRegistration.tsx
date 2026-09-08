import { useState, useRef, useEffect } from "react";
import evaliaLogo from "@/imports/Disen_o_sin_ti_tulo.png";
import { register } from "./auth/api";

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

function formatSize(bytes: number) {
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/* ── Floating label input ────────────────────────────────────── */
function FloatInput({
  id, label, type = "text", value, onChange, disabled,
}: {
  id: string; label: string; type?: string; value: string;
  onChange: (v: string) => void; disabled?: boolean;
}) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;

  return (
    <div style={{ position: "relative" }}>
      <input
        id={id}
        type={type}
        value={value}
        disabled={disabled}
        autoComplete={type === "email" ? "email" : type === "tel" ? "tel" : "off"}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          background: focused ? "rgba(229,59,246,0.06)" : "rgba(255,255,255,0.04)",
          borderTop:    `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          borderRight:  `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          borderBottom: `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          borderLeft:   `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          boxShadow: focused ? "0 0 0 3px rgba(229,59,246,0.1)" : "none",
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
        htmlFor={id}
        style={{
          position: "absolute",
          left: "1.125rem",
          top: active ? "0.6rem" : "50%",
          transform: active ? "none" : "translateY(-50%)",
          fontSize: active ? "0.6875rem" : "0.9375rem",
          fontWeight: active ? 500 : 400,
          color: active ? "rgba(229,59,246,0.9)" : "rgba(148,163,184,0.8)",
          letterSpacing: active ? "0.04em" : "normal",
          pointerEvents: "none",
          transition: "all 0.2s ease",
          fontFamily: "Poppins, sans-serif",
        }}
      >
        {label}
      </label>
    </div>
  );
}

/* ── Password input with eye toggle ─────────────────────────── */
function EyeOpen() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke="currentColor" strokeWidth="1.75" />
      <circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="1.75" />
    </svg>
  );
}
function EyeOff() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
      <path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
      <path d="M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
      <path d="M14.12 14.12a3 3 0 01-4.24-4.24" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
      <line x1="1" y1="1" x2="23" y2="23" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function PasswordInput({
  value, onChange, show, onToggle, disabled,
}: {
  value: string; onChange: (v: string) => void;
  show: boolean; onToggle: () => void; disabled?: boolean;
}) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;

  return (
    <div style={{ position: "relative" }}>
      <input
        id="reg-pass"
        type={show ? "text" : "password"}
        value={value}
        disabled={disabled}
        autoComplete="new-password"
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          background: focused ? "rgba(229,59,246,0.06)" : "rgba(255,255,255,0.04)",
          borderTop:    `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          borderRight:  `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          borderBottom: `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          borderLeft:   `1px solid ${focused ? "rgba(229,59,246,0.6)" : "rgba(255,255,255,0.1)"}`,
          boxShadow: focused ? "0 0 0 3px rgba(229,59,246,0.1)" : "none",
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
        htmlFor="reg-pass"
        style={{
          position: "absolute",
          left: "1.125rem",
          top: active ? "0.6rem" : "50%",
          transform: active ? "none" : "translateY(-50%)",
          fontSize: active ? "0.6875rem" : "0.9375rem",
          fontWeight: active ? 500 : 400,
          color: active ? "rgba(229,59,246,0.9)" : "rgba(148,163,184,0.8)",
          letterSpacing: active ? "0.04em" : "normal",
          pointerEvents: "none",
          transition: "all 0.2s ease",
          fontFamily: "Poppins, sans-serif",
        }}
      >
        Contraseña
      </label>
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
          color: "rgba(148,163,184,0.45)",
          display: "flex",
          alignItems: "center",
          padding: 4,
        }}
      >
        {show ? <EyeOff /> : <EyeOpen />}
      </button>
    </div>
  );
}

/* ── Cloud upload icon ───────────────────────────────────────── */
function CloudUpIcon({ color, size = 40 }: { color: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 48 48" fill="none">
      <path
        d="M36 34a9 9 0 000-18h-1.5C32.8 9.6 28.7 7 24 7c-7.2 0-13 5.8-13 13A9 9 0 0011 34"
        stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
      />
      <path d="M18 28l6-7 6 7M24 21v16" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* ── PDF file icon ───────────────────────────────────────────── */
function PdfIcon() {
  return (
    <svg width="34" height="38" viewBox="0 0 34 38" fill="none">
      <rect x="1" y="1" width="32" height="36" rx="4"
        fill="rgba(229,59,246,0.08)" stroke="rgba(229,59,246,0.35)" strokeWidth="1.5" />
      <path d="M20 1 L33 14 L20 14 Z" fill="rgba(229,59,246,0.18)" />
      <path d="M20 1 L33 14" stroke="rgba(229,59,246,0.4)" strokeWidth="1.5" strokeLinecap="round" />
      <text x="17" y="30" textAnchor="middle" fill="#E53BF6" fontSize="8" fontWeight="800"
        fontFamily="Poppins, sans-serif" letterSpacing="0.5">PDF</text>
    </svg>
  );
}

/* ── Success icon ────────────────────────────────────────────── */
function SuccessIcon({ size = 84 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 84 84" fill="none">
      <defs>
        <linearGradient id="regSG" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#E53BF6" />
          <stop offset="100%" stopColor="#3BF6E5" />
        </linearGradient>
        <filter id="regGlow" x="-35%" y="-35%" width="170%" height="170%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="4" result="b" />
          <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        <filter id="regHalo" x="-55%" y="-55%" width="210%" height="210%">
          <feGaussianBlur in="SourceGraphic" stdDeviation="12" />
        </filter>
      </defs>
      <circle cx="42" cy="42" r="38" fill="#7000a0" filter="url(#regHalo)" opacity="0.5" />
      <circle cx="42" cy="42" r="38" stroke="url(#regSG)" strokeWidth="2"
        fill="rgba(229,59,246,0.07)" filter="url(#regGlow)" />
      <circle cx="42" cy="42" r="30" stroke="rgba(229,59,246,0.18)"
        strokeWidth="0.75" strokeDasharray="4 8" fill="none" />
      <path d="M25 42 l13 14 21-24"
        stroke="url(#regSG)" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round"
        filter="url(#regGlow)" />
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

/* ── Validation ──────────────────────────────────────────────── */
function validate(nombre: string, cedula: string, email: string, telefono: string, password: string, file: { name: string } | null) {
  if (!nombre.trim())           return "El nombre completo es requerido.";
  if (!cedula.trim())           return "La cédula o pasaporte es requerida.";
  if (!email.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email))
                                return "Ingresa un correo electrónico válido.";
  if (!telefono.trim())         return "El teléfono es requerido.";
  if (!password || password.length < 12) return "La contraseña debe tener mínimo 12 caracteres.";
  if (!/[a-z]/.test(password) || !/[A-Z]/.test(password) || !/\d/.test(password) || !/[^A-Za-z0-9]/.test(password))
                                return "La contraseña debe incluir mayúscula, minúscula, número y símbolo.";
  if (!file)                    return "Debes adjuntar la Carta de Autorización en PDF.";
  return "";
}

/* ── Main screen ─────────────────────────────────────────────── */
export default function UserRegistration({ onBack, onSuccess }: { onBack: () => void; onSuccess?: () => void }) {
  const width    = useWidth();
  const isMobile = width < 768;

  const [role,     setRole]     = useState<"admin" | "delegado">("admin");
  const [nombre,   setNombre]   = useState("");
  const [cedula,   setCedula]   = useState("");
  const [email,    setEmail]    = useState("");
  const [telefono, setTelefono] = useState("");
  const [password, setPassword] = useState("");
  const [showPass, setShowPass] = useState(false);

  const [file,       setFile]       = useState<{ name: string; size: number } | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [status, setStatus] = useState<"idle" | "loading" | "success">("idle");
  const [error,  setError]  = useState("");

  /* ── Drag & Drop ── */
  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };
  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };
  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const f = e.dataTransfer.files[0];
    if (f && f.type === "application/pdf") {
      setFile({ name: f.name, size: f.size });
      setError("");
    } else if (f) {
      setError("Solo se aceptan archivos PDF.");
    }
  };
  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (f) { setFile({ name: f.name, size: f.size }); setError(""); }
    e.target.value = "";
  };

  /* ── Submit ── */
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const err = validate(nombre, cedula, email, telefono, password, file);
    if (err) { setError(err); return; }
    setError("");
    setStatus("loading");
    try {
      await register({
        fullName: nombre,
        documentNumber: cedula,
        phoneNumber: telefono,
        email,
        password,
        requestedRole: role === "admin" ? "ADMINISTRADOR_EMPRESA" : "USUARIO_DELEGADO",
      });
      if (onSuccess) onSuccess();
      else setStatus("success");
    } catch (registrationError) {
      setStatus("idle");
      setError(registrationError instanceof Error
        ? registrationError.message
        : "No fue posible enviar la solicitud.");
    }
  };

  const cardPad   = isMobile ? "28px 20px 44px" : "40px 36px 40px";
  const logoH     = isMobile ? 42 : 50;
  const headingFs = isMobile ? "1.28rem" : "1.5rem";
  const fieldGap  = isMobile ? 12 : 14;

  return (
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
      {/* Mesh orbs */}
      <div className="mesh-orb"       style={{ width: isMobile ? 340 : 480, height: isMobile ? 340 : 480, background: "rgba(229,59,246,0.44)", top: "-100px", left: "-80px" }} />
      <div className="mesh-orb mesh-orb-2" style={{ width: isMobile ? 280 : 380, height: isMobile ? 280 : 380, background: "rgba(59,246,229,0.3)",  bottom: "-80px", right: "-60px" }} />
      <div className="mesh-orb mesh-orb-3" style={{ width: isMobile ? 200 : 260, height: isMobile ? 200 : 260, background: "rgba(99,59,246,0.26)", top: "40%", left: "38%" }} />

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
          maxWidth: 480,
          minHeight: isMobile ? "100svh" : "auto",
          borderRadius: isMobile ? 0 : 28,
          padding: cardPad,
          display: "flex",
          flexDirection: "column",
          ...(isMobile ? { borderTop: "none", borderRight: "none", borderBottom: "none", borderLeft: "none" } : {}),
        }}
      >
        {/* Logo */}
        <div className="animate-fade-in-up" style={{ display: "flex", justifyContent: "center", marginBottom: 20 }}>
          <img src={evaliaLogo} alt="Evalia" style={{ height: logoH, width: "auto" }} />
        </div>

        {status === "success" ? (
          /* ─── SUCCESS STATE ─── */
          <div className="animate-fade-in" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", flex: 1, justifyContent: "center" }}>
            <div style={{ position: "relative", display: "inline-flex", marginBottom: 22 }}>
              <div
                style={{
                  position: "absolute", inset: -22, borderRadius: "50%",
                  background: "radial-gradient(ellipse, rgba(229,59,246,0.22) 0%, rgba(59,246,229,0.1) 55%, transparent 72%)",
                  filter: "blur(14px)", animation: "shimmer 3s ease-in-out infinite",
                }}
              />
              <SuccessIcon size={isMobile ? 80 : 92} />
            </div>

            <h2
              style={{
                fontSize: headingFs, fontWeight: 800, fontFamily: "Poppins, sans-serif",
                letterSpacing: "-0.02em", marginBottom: 8,
                background: "linear-gradient(135deg, #E53BF6, #3BF6E5)",
                WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text",
              }}
            >
              ¡Solicitud Enviada!
            </h2>
            <p style={{ fontSize: "0.82rem", color: "rgba(148,163,184,0.75)", fontFamily: "Poppins, sans-serif", fontWeight: 300, lineHeight: 1.65, maxWidth: 310, marginBottom: 12 }}>
              Tu solicitud de registro ha sido recibida. El administrador revisará tu información y te enviará la confirmación en un plazo de{" "}
              <span style={{ color: "#3BF6E5", fontWeight: 500 }}>24 a 48 horas</span>.
            </p>

            {/* Info card */}
            <div
              style={{
                width: "100%", padding: "14px 16px", borderRadius: 14, marginBottom: 28,
                background: "rgba(229,59,246,0.06)",
                borderTop: "1px solid rgba(229,59,246,0.2)", borderRight: "1px solid rgba(229,59,246,0.2)",
                borderBottom: "1px solid rgba(229,59,246,0.2)", borderLeft: "1px solid rgba(229,59,246,0.2)",
              }}
            >
              {[
                { icon: "📧", text: "Recibirás un correo de confirmación." },
                { icon: "🔒", text: "Tu carta de autorización fue adjuntada." },
                { icon: "👤", text: `Rol solicitado: ${role === "admin" ? "Admin. de Empresa" : "Usuario Delegado"}` },
              ].map(({ icon, text }) => (
                <div key={text} style={{ display: "flex", alignItems: "flex-start", gap: 10, marginBottom: 8, "&:last-child": { marginBottom: 0 } }}>
                  <span style={{ fontSize: "0.75rem" }}>{icon}</span>
                  <span style={{ fontSize: "0.73rem", color: "rgba(148,163,184,0.7)", fontFamily: "Poppins, sans-serif", lineHeight: 1.5 }}>{text}</span>
                </div>
              ))}
            </div>

            <button
              onClick={onBack}
              className="gradient-btn"
              style={{
                width: "100%", padding: "14px 0", borderRadius: 14,
                color: "white", fontWeight: 700, fontSize: "0.93rem",
                fontFamily: "Poppins, sans-serif", border: "none", cursor: "pointer",
                letterSpacing: "0.04em",
              }}
            >
              Volver al Inicio de Sesión
            </button>
          </div>
        ) : (
          /* ─── FORM STATE ─── */
          <>
            {/* Heading */}
            <div className="animate-fade-in-up delay-100" style={{ textAlign: "center", marginBottom: 20 }}>
              <h2
                style={{
                  fontSize: headingFs, fontWeight: 800, color: "#f1f5f9",
                  fontFamily: "Poppins, sans-serif", letterSpacing: "-0.02em", marginBottom: 5,
                }}
              >
                Crear Cuenta
              </h2>
              <p style={{ fontSize: "0.77rem", color: "rgba(148,163,184,0.62)", fontFamily: "Poppins, sans-serif", fontWeight: 300, lineHeight: 1.5 }}>
                Completa el formulario para solicitar acceso a Evalia.
              </p>
            </div>

            {/* Role segmented control */}
            <div
              className="animate-fade-in-up delay-200"
              style={{
                display: "flex", gap: 3, padding: 3, borderRadius: 13,
                background: "rgba(255,255,255,0.04)",
                borderTop: "1px solid rgba(255,255,255,0.08)", borderRight: "1px solid rgba(255,255,255,0.08)",
                borderBottom: "1px solid rgba(255,255,255,0.08)", borderLeft: "1px solid rgba(255,255,255,0.08)",
                marginBottom: 20,
              }}
            >
              {([
                { key: "admin",    label: "Admin. de Empresa", short: "Admin." },
                { key: "delegado", label: "Usuario Delegado",  short: "Delegado" },
              ] as const).map(({ key, label, short }) => {
                const active = role === key;
                return (
                  <button
                    key={key}
                    type="button"
                    onClick={() => setRole(key)}
                    style={{
                      flex: 1,
                      padding: isMobile ? "9px 6px" : "9px 0",
                      borderRadius: 10,
                      border: "none",
                      cursor: "pointer",
                      fontFamily: "Poppins, sans-serif",
                      fontSize: isMobile ? "0.72rem" : "0.77rem",
                      fontWeight: active ? 700 : 400,
                      color: active ? "#E53BF6" : "rgba(148,163,184,0.5)",
                      background: active ? "rgba(229,59,246,0.12)" : "transparent",
                      boxShadow: active ? "inset 0 0 0 1.5px rgba(229,59,246,0.45), 0 0 16px rgba(229,59,246,0.12)" : "none",
                      transition: "all 0.22s ease",
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {isMobile ? short : label}
                  </button>
                );
              })}
            </div>

            {/* Section label */}
            <p
              className="animate-fade-in-up delay-200"
              style={{
                fontSize: "0.63rem", fontWeight: 700, letterSpacing: "0.1em",
                color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif",
                textTransform: "uppercase", marginBottom: 10,
              }}
            >
              Datos personales
            </p>

            {/* Form fields */}
            <form
              onSubmit={handleSubmit}
              className="animate-fade-in-up delay-300"
              style={{ display: "flex", flexDirection: "column", gap: fieldGap }}
            >
              <FloatInput id="nombre"   label="Nombre completo"    value={nombre}   onChange={(v) => { setNombre(v);   setError(""); }} disabled={status === "loading"} />
              <FloatInput id="cedula"   label="Cédula / Pasaporte" value={cedula}   onChange={(v) => { setCedula(v);   setError(""); }} disabled={status === "loading"} />
              <FloatInput id="email"    label="Correo electrónico"  type="email" value={email}    onChange={(v) => { setEmail(v);    setError(""); }} disabled={status === "loading"} />

              {/* Phone with country badge */}
              <div style={{ position: "relative" }}>
                <FloatInput id="telefono" label="Teléfono"            type="tel"  value={telefono} onChange={(v) => { setTelefono(v); setError(""); }} disabled={status === "loading"} />
                {/* Country code badge */}
                <div
                  style={{
                    position: "absolute",
                    right: "0.85rem",
                    top: "50%",
                    transform: "translateY(-50%)",
                    padding: "3px 8px",
                    borderRadius: 6,
                    background: "rgba(255,255,255,0.06)",
                    borderTop: "1px solid rgba(255,255,255,0.1)", borderRight: "1px solid rgba(255,255,255,0.1)",
                    borderBottom: "1px solid rgba(255,255,255,0.1)", borderLeft: "1px solid rgba(255,255,255,0.1)",
                    pointerEvents: "none",
                  }}
                >
                  <span style={{ fontSize: "0.65rem", color: "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>🌎 +57</span>
                </div>
              </div>

              <PasswordInput
                value={password}
                onChange={(v) => { setPassword(v); setError(""); }}
                show={showPass}
                onToggle={() => setShowPass(!showPass)}
                disabled={status === "loading"}
              />

              {/* Section label */}
              <p
                style={{
                  fontSize: "0.63rem", fontWeight: 700, letterSpacing: "0.1em",
                  color: "rgba(148,163,184,0.35)", fontFamily: "Poppins, sans-serif",
                  textTransform: "uppercase", marginTop: 4, marginBottom: -2,
                }}
              >
                Documentación
              </p>

              {/* Drag & Drop zone */}
              <input
                ref={fileInputRef}
                type="file"
                accept=".pdf,application/pdf"
                style={{ display: "none" }}
                onChange={handleFileChange}
              />

              <div
                onClick={() => !file && fileInputRef.current?.click()}
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                style={{
                  borderRadius: 16,
                  padding: file ? "14px 16px" : "22px 16px",
                  cursor: file ? "default" : "pointer",
                  transition: "all 0.25s ease",
                  position: "relative",
                  overflow: "hidden",

                  /* Conditional border + bg */
                  borderTop:    `2px ${isDragging ? "solid" : "dashed"} ${isDragging ? "rgba(59,246,229,0.9)" : file ? "rgba(59,246,229,0.4)" : "rgba(59,246,229,0.45)"}`,
                  borderRight:  `2px ${isDragging ? "solid" : "dashed"} ${isDragging ? "rgba(59,246,229,0.9)" : file ? "rgba(59,246,229,0.4)" : "rgba(59,246,229,0.45)"}`,
                  borderBottom: `2px ${isDragging ? "solid" : "dashed"} ${isDragging ? "rgba(59,246,229,0.9)" : file ? "rgba(59,246,229,0.4)" : "rgba(59,246,229,0.45)"}`,
                  borderLeft:   `2px ${isDragging ? "solid" : "dashed"} ${isDragging ? "rgba(59,246,229,0.9)" : file ? "rgba(59,246,229,0.4)" : "rgba(59,246,229,0.45)"}`,

                  background: isDragging
                    ? "rgba(59,246,229,0.09)"
                    : file
                    ? "rgba(59,246,229,0.04)"
                    : "rgba(255,255,255,0.025)",

                  boxShadow: isDragging
                    ? "0 0 0 4px rgba(59,246,229,0.12), 0 0 24px rgba(59,246,229,0.2)"
                    : "none",
                }}
              >
                {/* Dragging overlay shimmer */}
                {isDragging && (
                  <div
                    style={{
                      position: "absolute", inset: 0, borderRadius: 14,
                      background: "linear-gradient(135deg, rgba(59,246,229,0.06) 0%, rgba(59,246,229,0.02) 100%)",
                      animation: "shimmer 1.4s ease-in-out infinite",
                      pointerEvents: "none",
                    }}
                  />
                )}

                {file ? (
                  /* ─ File selected state ─ */
                  <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                    <PdfIcon />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <p
                        style={{
                          fontSize: "0.8rem", fontWeight: 600, color: "#f1f5f9",
                          fontFamily: "Poppins, sans-serif", whiteSpace: "nowrap",
                          overflow: "hidden", textOverflow: "ellipsis", marginBottom: 2,
                        }}
                      >
                        {file.name}
                      </p>
                      <p style={{ fontSize: "0.67rem", color: "rgba(59,246,229,0.7)", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>
                        {formatSize(file.size)} · PDF adjunto
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={(e) => { e.stopPropagation(); setFile(null); }}
                      style={{
                        flexShrink: 0, width: 28, height: 28, borderRadius: "50%",
                        border: "none", cursor: "pointer",
                        background: "rgba(239,68,68,0.12)",
                        color: "rgba(239,68,68,0.7)",
                        display: "flex", alignItems: "center", justifyContent: "center",
                        transition: "all 0.2s ease",
                      }}
                    >
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none">
                        <path d="M18 6L6 18M6 6l12 12" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
                      </svg>
                    </button>
                  </div>
                ) : (
                  /* ─ Empty / dragging state ─ */
                  <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 8, textAlign: "center" }}>
                    <div
                      style={{
                        transition: "transform 0.2s ease",
                        transform: isDragging ? "scale(1.12) translateY(-3px)" : "scale(1)",
                        opacity: isDragging ? 1 : 0.75,
                      }}
                    >
                      <CloudUpIcon color={isDragging ? "#3BF6E5" : "rgba(59,246,229,0.8)"} size={isMobile ? 36 : 40} />
                    </div>
                    <div>
                      <p
                        style={{
                          fontSize: "0.8rem", fontWeight: 600, fontFamily: "Poppins, sans-serif",
                          color: isDragging ? "#3BF6E5" : "rgba(148,163,184,0.75)", marginBottom: 3,
                          transition: "color 0.2s ease",
                        }}
                      >
                        {isDragging ? "¡Suelta el archivo aquí!" : "Subir Carta de Autorización (PDF)"}
                      </p>
                      {!isDragging && (
                        <p style={{ fontSize: "0.67rem", color: "rgba(148,163,184,0.38)", fontFamily: "Poppins, sans-serif" }}>
                          Arrastra el archivo o haz clic para seleccionar
                        </p>
                      )}
                    </div>
                  </div>
                )}
              </div>

              {/* Error message */}
              {error && (
                <div
                  className="animate-fade-in"
                  style={{
                    display: "flex", alignItems: "center", gap: 7,
                    padding: "10px 14px", borderRadius: 10,
                    background: "rgba(229,59,246,0.07)",
                    borderTop: "1px solid rgba(229,59,246,0.22)", borderRight: "1px solid rgba(229,59,246,0.22)",
                    borderBottom: "1px solid rgba(229,59,246,0.22)", borderLeft: "1px solid rgba(229,59,246,0.22)",
                  }}
                >
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
                    <path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="#E53BF6" strokeWidth="2" strokeLinecap="round" />
                  </svg>
                  <span style={{ fontSize: "0.74rem", color: "#E53BF6", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>{error}</span>
                </div>
              )}

              {/* Submit */}
              <button
                type="submit"
                disabled={status === "loading"}
                className="gradient-btn"
                style={{
                  width: "100%", padding: isMobile ? "14px 0" : "15px 0",
                  borderRadius: 14, color: "white", fontWeight: 700, fontSize: "0.93rem",
                  fontFamily: "Poppins, sans-serif", border: "none",
                  cursor: status === "loading" ? "not-allowed" : "pointer",
                  display: "flex", alignItems: "center", justifyContent: "center",
                  gap: 8, letterSpacing: "0.04em",
                  opacity: status === "loading" ? 0.8 : 1, marginTop: 4,
                }}
              >
                {status === "loading" ? (
                  <>
                    <span
                      style={{
                        display: "inline-block", width: 16, height: 16, borderRadius: "50%",
                        borderTop: "2px solid white", borderRight: "2px solid rgba(255,255,255,0.3)",
                        borderBottom: "2px solid rgba(255,255,255,0.3)", borderLeft: "2px solid rgba(255,255,255,0.3)",
                        animation: "spin 0.7s linear infinite",
                      }}
                    />
                    Registrando…
                  </>
                ) : (
                  <>
                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none">
                      <path d="M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2" stroke="white" strokeWidth="1.75" strokeLinecap="round" />
                      <circle cx="9" cy="7" r="4" stroke="white" strokeWidth="1.75" />
                      <path d="M19 8v6M22 11h-6" stroke="white" strokeWidth="1.75" strokeLinecap="round" />
                    </svg>
                    Registrar Usuario
                  </>
                )}
              </button>
            </form>

            {/* Back link */}
            <button
              type="button"
              onClick={onBack}
              style={{
                marginTop: 18, display: "flex", alignItems: "center", justifyContent: "center",
                gap: 7, background: "none", border: "none", cursor: "pointer",
                color: "rgba(148,163,184,0.45)", fontSize: "0.77rem",
                fontFamily: "Poppins, sans-serif", fontWeight: 400, width: "100%", padding: "4px 0",
              }}
            >
              <BackArrow />
              Volver al Inicio de Sesión
            </button>

            {/* Terms note */}
            <p style={{ marginTop: 12, fontSize: "0.62rem", color: "rgba(148,163,184,0.25)", textAlign: "center", fontFamily: "Poppins, sans-serif", lineHeight: 1.6 }}>
              Al registrarte aceptas nuestros{" "}
              <span style={{ color: "rgba(229,59,246,0.4)", fontWeight: 500 }}>Términos de Servicio</span>{" "}
              y{" "}
              <span style={{ color: "rgba(229,59,246,0.4)", fontWeight: 500 }}>Política de Privacidad</span>.
            </p>
          </>
        )}
      </div>
    </div>
  );
}

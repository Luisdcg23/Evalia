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

/* ── Floating label input ────────────────────────────────────── */
function FloatInput({
  id, label, type = "text", value, onChange, hint,
}: {
  id: string; label: string; type?: string;
  value: string; onChange: (v: string) => void; hint?: string;
}) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;

  return (
    <div style={{ position: "relative" }}>
      <input
        id={id}
        type={type}
        value={value}
        autoComplete="off"
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          background: focused ? "rgba(59,246,229,0.05)" : "rgba(255,255,255,0.035)",
          borderTop:    focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.09)",
          borderRight:  focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.09)",
          borderBottom: focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.09)",
          borderLeft:   focused ? "1px solid rgba(59,246,229,0.55)" : "1px solid rgba(255,255,255,0.09)",
          boxShadow: focused ? "0 0 0 3px rgba(59,246,229,0.09), 0 0 16px rgba(59,246,229,0.08)" : "none",
          color: "#f1f5f9",
          fontFamily: "Poppins, sans-serif",
          fontSize: "0.875rem",
          padding: "1.2rem 1rem 0.45rem",
          borderRadius: 11,
          outline: "none",
          transition: "all 0.2s ease",
          boxSizing: "border-box",
        }}
      />
      <label
        htmlFor={id}
        style={{
          position: "absolute",
          left: "1rem",
          top: active ? "0.52rem" : "50%",
          transform: active ? "none" : "translateY(-50%)",
          fontSize: active ? "0.6rem" : "0.84rem",
          fontWeight: active ? 600 : 400,
          color: active
            ? "rgba(59,246,229,0.85)"
            : "rgba(148,163,184,0.65)",
          letterSpacing: active ? "0.07em" : "normal",
          textTransform: active ? "uppercase" : "none",
          pointerEvents: "none",
          transition: "all 0.18s ease",
          fontFamily: "Poppins, sans-serif",
        }}
      >
        {label}
      </label>
      {hint && !focused && !value && (
        <span style={{
          position: "absolute", right: "0.9rem", top: "50%",
          transform: "translateY(-50%)",
          fontSize: "0.6rem", color: "rgba(148,163,184,0.28)",
          fontFamily: "Poppins, sans-serif", pointerEvents: "none",
        }}>
          {hint}
        </span>
      )}
    </div>
  );
}

/* ── Card section header ─────────────────────────────────────── */
function CardHeader({
  icon, title, subtitle, accentColor,
}: {
  icon: React.ReactNode; title: string; subtitle: string; accentColor: string;
}) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 20 }}>
      <div style={{
        width: 38, height: 38, borderRadius: 11, flexShrink: 0,
        background: `${accentColor}18`,
        borderTop:    `1px solid ${accentColor}44`,
        borderRight:  `1px solid ${accentColor}44`,
        borderBottom: `1px solid ${accentColor}44`,
        borderLeft:   `1px solid ${accentColor}44`,
        display: "flex", alignItems: "center", justifyContent: "center",
        boxShadow: `0 0 16px ${accentColor}20`,
      }}>
        {icon}
      </div>
      <div>
        <h3 style={{
          fontSize: "0.97rem", fontWeight: 700,
          color: "#f1f5f9", fontFamily: "Poppins, sans-serif",
          letterSpacing: "-0.01em", marginBottom: 2,
        }}>
          {title}
        </h3>
        <p style={{
          fontSize: "0.58rem", fontWeight: 600,
          color: `${accentColor}99`,
          fontFamily: "Poppins, sans-serif",
          letterSpacing: "0.1em", textTransform: "uppercase",
        }}>
          {subtitle}
        </p>
      </div>
    </div>
  );
}

/* ── Divider with label ──────────────────────────────────────── */
function FieldDivider({ label }: { label: string }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 10, margin: "4px 0 10px" }}>
      <div style={{ flex: 1, height: 1, background: "rgba(255,255,255,0.05)" }} />
      <span style={{
        fontSize: "0.55rem", fontWeight: 700, letterSpacing: "0.1em",
        color: "rgba(148,163,184,0.28)", fontFamily: "Poppins, sans-serif",
        textTransform: "uppercase",
      }}>
        {label}
      </span>
      <div style={{ flex: 1, height: 1, background: "rgba(255,255,255,0.05)" }} />
    </div>
  );
}

/* ── Representative mini-card ────────────────────────────────── */
function RepCard({
  role, accent, icon, namePH, fieldLabel, fieldType = "text", fieldPH,
  name, onName, field, onField,
}: {
  role: string; accent: string; icon: React.ReactNode;
  namePH?: string; fieldLabel: string; fieldType?: string; fieldPH?: string;
  name: string; onName: (v: string) => void;
  field: string; onField: (v: string) => void;
}) {
  return (
    <div style={{
      borderRadius: 13, padding: "14px 14px 12px",
      background: `${accent}07`,
      borderTop:    `1px solid ${accent}28`,
      borderRight:  `1px solid ${accent}18`,
      borderBottom: `1px solid ${accent}18`,
      borderLeft:   `3px solid ${accent}55`,
    }}>
      {/* Role label */}
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
        <div style={{
          width: 26, height: 26, borderRadius: 8,
          background: `${accent}18`,
          borderTop:    `1px solid ${accent}44`,
          borderRight:  `1px solid ${accent}44`,
          borderBottom: `1px solid ${accent}44`,
          borderLeft:   `1px solid ${accent}44`,
          display: "flex", alignItems: "center", justifyContent: "center",
          flexShrink: 0,
        }}>
          {icon}
        </div>
        <span style={{
          fontSize: "0.65rem", fontWeight: 700, letterSpacing: "0.08em",
          color: accent, fontFamily: "Poppins, sans-serif", textTransform: "uppercase",
        }}>
          {role}
        </span>
      </div>

      {/* Fields */}
      <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
        <FloatInput id={`rep-name-${role}`} label="Nombre completo" value={name} onChange={onName} hint={namePH} />
        <FloatInput id={`rep-field-${role}`} label={fieldLabel} type={fieldType} value={field} onChange={onField} hint={fieldPH} />
      </div>
    </div>
  );
}

/* ── Icons ───────────────────────────────────────────────────── */
function BuildingIcon({ color }: { color: string }) {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
      <path d="M3 21h18M3 7l9-4 9 4M4 7v14M20 7v14M9 21v-4a3 3 0 016 0v4"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function UsersIcon({ color }: { color: string }) {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
      <path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2M9 11a4 4 0 100-8 4 4 0 000 8zM23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function GavelIcon({ color }: { color: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
      <path d="M14 12L3 21l-1-1 9-11M8 8l4-4 9 9-4 4L8 8zM15 5l4 4"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function StarIcon({ color }: { color: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
      <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function PhoneIcon({ color }: { color: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none">
      <path d="M22 16.92v3a2 2 0 01-2.18 2 19.79 19.79 0 01-8.63-3.07A19.5 19.5 0 013.07 10.8a19.79 19.79 0 01-3.07-8.7A2 2 0 012 0h3a2 2 0 012 1.72c.127.96.361 1.903.7 2.81a2 2 0 01-.45 2.11L6.09 7.91a16 16 0 006 6l1.27-1.27a2 2 0 012.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0122 14.92z"
        stroke={color} strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function SaveIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none">
      <path d="M19 21H5a2 2 0 01-2-2V5a2 2 0 012-2h11l5 5v11a2 2 0 01-2 2z"
        stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M17 21v-8H7v8M7 3v5h8"
        stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
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

/* ── Main component ──────────────────────────────────────────── */
export default function CompanyForm({ onBack, onSaved }: { onBack: () => void; onSaved?: () => void }) {
  const width    = useWidth();
  const isMobile = width < 768;

  /* Datos del Establecimiento */
  const [razonSocial,    setRazonSocial]    = useState("");
  const [rnc,            setRnc]            = useState("");
  const [nombreComercial, setNombreComercial] = useState("");
  const [direccion,      setDireccion]      = useState("");
  const [municipio,      setMunicipio]      = useState("");
  const [provincia,      setProvincia]      = useState("");
  const [telefono,       setTelefono]       = useState("");
  const [correo,         setCorreo]         = useState("");
  const [actividad,      setActividad]      = useState("");

  /* Representantes */
  const [legalNombre, setLegalNombre] = useState("");
  const [legalCedula, setLegalCedula] = useState("");

  const [calidadNombre, setCalidadNombre] = useState("");
  const [calidadTel,    setCalidadTel]    = useState("");

  const [contactoNombre, setContactoNombre] = useState("");
  const [contactoCorreo, setContactoCorreo] = useState("");

  const [saving,  setSaving]  = useState(false);
  const [saved,   setSaved]   = useState(false);

  const handleSave = () => {
    setSaving(true);
    setTimeout(() => {
      setSaving(false);
      setSaved(true);
      if (onSaved) {
        setTimeout(onSaved, 900);
      } else {
        setTimeout(() => setSaved(false), 3500);
      }
    }, 1600);
  };

  const CYAN    = "#3BF6E5";
  const MAGENTA = "#E53BF6";
  const YELLOW  = "#F6E53B";

  const STICKY_H = 80;

  return (
    <>
      <style>{`
        @keyframes cfSaveGlow {
          0%,100% { box-shadow: 0 0 20px rgba(59,246,229,0.4), 0 0 44px rgba(229,59,246,0.25); }
          50%      { box-shadow: 0 0 36px rgba(59,246,229,0.65), 0 0 70px rgba(229,59,246,0.42); }
        }
        @keyframes cfSavedPop {
          0%   { transform: scale(0.94); opacity: 0; }
          60%  { transform: scale(1.03); }
          100% { transform: scale(1);   opacity: 1; }
        }
        .cf-saved { animation: cfSavedPop 0.35s cubic-bezier(.22,1,.36,1) both; }
      `}</style>

      <div
        className="mesh-bg"
        style={{
          minHeight: "100svh",
          background: "#0F172A",
          position: "relative",
          overflow: "hidden",
        }}
      >
        {/* Mesh orbs */}
        <div className="mesh-orb"        style={{ width: 420, height: 420, background: "rgba(59,246,229,0.35)",  top: "-120px", right: "-80px" }} />
        <div className="mesh-orb mesh-orb-2" style={{ width: 360, height: 360, background: "rgba(229,59,246,0.32)", bottom: "80px",  left:  "-70px" }} />
        <div className="mesh-orb mesh-orb-3" style={{ width: 240, height: 240, background: "rgba(246,229,59,0.12)", top: "42%",   left:  "55%"   }} />

        {/* Grain */}
        <div style={{ position: "absolute", inset: 0, opacity: 0.045, backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`, backgroundSize: "180px", pointerEvents: "none", zIndex: 0 }} />

        {/* Scrollable content */}
        <div
          style={{
            position: "relative", zIndex: 1,
            maxWidth: isMobile ? "100%" : 520,
            margin: "0 auto",
            padding: isMobile
              ? `0 0 ${STICKY_H + 16}px`
              : `32px 20px ${STICKY_H + 24}px`,
          }}
        >
          {/* ── Top bar ── */}
          <div style={{
            display: "flex", alignItems: "center", gap: 12,
            padding: isMobile ? "18px 18px 14px" : "0 0 20px",
          }}>
            <button
              type="button" onClick={onBack}
              style={{
                width: 36, height: 36, borderRadius: 10,
                background: "rgba(255,255,255,0.06)",
                borderTop:    "1px solid rgba(255,255,255,0.12)",
                borderRight:  "1px solid rgba(255,255,255,0.12)",
                borderBottom: "1px solid rgba(255,255,255,0.12)",
                borderLeft:   "1px solid rgba(255,255,255,0.12)",
                color: "rgba(148,163,184,0.7)",
                display: "flex", alignItems: "center", justifyContent: "center",
                cursor: "pointer", flexShrink: 0,
              }}
            >
              <BackArrow />
            </button>
            <div>
              <h1 style={{ fontSize: isMobile ? "1.05rem" : "1.2rem", fontWeight: 800, color: "#f1f5f9", fontFamily: "Poppins, sans-serif", letterSpacing: "-0.02em" }}>
                Formulario de Empresa
              </h1>
              <p style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif", fontWeight: 500, letterSpacing: "0.04em" }}>
                Completa la información del establecimiento
              </p>
            </div>
          </div>

          {/* ═══ CARD 1: Datos del Establecimiento ═══ */}
          <div
            className="animate-fade-in"
            style={{
              margin: isMobile ? "0 12px 14px" : "0 0 14px",
              borderRadius: isMobile ? 20 : 22,
              padding: "20px 18px 18px",
              background: "rgba(12,20,40,0.82)",
              backdropFilter: "blur(32px) saturate(160%)",
              WebkitBackdropFilter: "blur(32px) saturate(160%)",
              borderTop:    "1px solid rgba(59,246,229,0.14)",
              borderRight:  "1px solid rgba(255,255,255,0.07)",
              borderBottom: "1px solid rgba(255,255,255,0.06)",
              borderLeft:   "1px solid rgba(59,246,229,0.14)",
              boxShadow: "0 20px 60px rgba(0,0,0,0.55), 0 0 0 1px rgba(59,246,229,0.04) inset",
            }}
          >
            {/* Accent stripe */}
            <div style={{ height: 2, borderRadius: 2, background: `linear-gradient(90deg, ${CYAN}, rgba(59,246,229,0.1))`, marginBottom: 16 }} />

            <CardHeader
              icon={<BuildingIcon color={CYAN} />}
              title="Datos del Establecimiento"
              subtitle="Información legal y comercial"
              accentColor={CYAN}
            />

            <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
              {/* Row: Razón Social */}
              <FloatInput id="razon" label="Razón Social" value={razonSocial} onChange={setRazonSocial} />

              {/* Row: RNC + Nombre Comercial inline on desktop */}
              <div style={{ display: "flex", gap: 9 }}>
                <div style={{ flex: "0 0 42%" }}>
                  <FloatInput id="rnc" label="RNC" value={rnc} onChange={setRnc} hint="000-00000-0" />
                </div>
                <div style={{ flex: 1 }}>
                  <FloatInput id="ncomer" label="Nombre Comercial" value={nombreComercial} onChange={setNombreComercial} />
                </div>
              </div>

              <FieldDivider label="Ubicación" />

              <FloatInput id="dir" label="Dirección" value={direccion} onChange={setDireccion} />

              <div style={{ display: "flex", gap: 9 }}>
                <div style={{ flex: 1 }}>
                  <FloatInput id="mun" label="Municipio" value={municipio} onChange={setMunicipio} />
                </div>
                <div style={{ flex: 1 }}>
                  <FloatInput id="prov" label="Provincia" value={provincia} onChange={setProvincia} />
                </div>
              </div>

              <FieldDivider label="Contacto" />

              <div style={{ display: "flex", gap: 9 }}>
                <div style={{ flex: "0 0 42%" }}>
                  <FloatInput id="tel" label="Teléfono" type="tel" value={telefono} onChange={setTelefono} hint="+1 (809)" />
                </div>
                <div style={{ flex: 1 }}>
                  <FloatInput id="email" label="Correo" type="email" value={correo} onChange={setCorreo} />
                </div>
              </div>

              <FieldDivider label="Actividad" />

              <FloatInput id="act" label="Actividad Económica" value={actividad} onChange={setActividad} />
            </div>

            {/* Field count badge */}
            <div style={{ marginTop: 14, display: "flex", justifyContent: "flex-end" }}>
              <span style={{ fontSize: "0.56rem", color: "rgba(59,246,229,0.35)", fontFamily: "Poppins, sans-serif", fontWeight: 600, letterSpacing: "0.08em" }}>
                9 CAMPOS · CIFRADO AES-256
              </span>
            </div>
          </div>

          {/* ═══ CARD 2: Representantes ═══ */}
          <div
            className="animate-fade-in"
            style={{
              margin: isMobile ? "0 12px 14px" : "0 0 14px",
              borderRadius: isMobile ? 20 : 22,
              padding: "20px 18px 18px",
              background: "rgba(12,20,40,0.82)",
              backdropFilter: "blur(32px) saturate(160%)",
              WebkitBackdropFilter: "blur(32px) saturate(160%)",
              borderTop:    "1px solid rgba(229,59,246,0.14)",
              borderRight:  "1px solid rgba(255,255,255,0.07)",
              borderBottom: "1px solid rgba(255,255,255,0.06)",
              borderLeft:   "1px solid rgba(229,59,246,0.14)",
              boxShadow: "0 20px 60px rgba(0,0,0,0.55), 0 0 0 1px rgba(229,59,246,0.04) inset",
            }}
          >
            {/* Accent stripe */}
            <div style={{ height: 2, borderRadius: 2, background: `linear-gradient(90deg, ${MAGENTA}, rgba(229,59,246,0.1))`, marginBottom: 16 }} />

            <CardHeader
              icon={<UsersIcon color={MAGENTA} />}
              title="Representantes"
              subtitle="Responsables del establecimiento"
              accentColor={MAGENTA}
            />

            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {/* Legal */}
              <RepCard
                role="Representante Legal"
                accent={CYAN}
                icon={<GavelIcon color={CYAN} />}
                fieldLabel="Cédula / Pasaporte"
                fieldPH="000-0000000-0"
                name={legalNombre}
                onName={setLegalNombre}
                field={legalCedula}
                onField={setLegalCedula}
              />

              {/* Calidad */}
              <RepCard
                role="Responsable de Calidad"
                accent={YELLOW}
                icon={<StarIcon color={YELLOW} />}
                fieldLabel="Teléfono directo"
                fieldType="tel"
                name={calidadNombre}
                onName={setCalidadNombre}
                field={calidadTel}
                onField={setCalidadTel}
              />

              {/* Contacto Principal */}
              <RepCard
                role="Contacto Principal"
                accent={MAGENTA}
                icon={<PhoneIcon color={MAGENTA} />}
                fieldLabel="Correo electrónico"
                fieldType="email"
                name={contactoNombre}
                onName={setContactoNombre}
                field={contactoCorreo}
                onField={setContactoCorreo}
              />
            </div>

            <div style={{ marginTop: 14, display: "flex", justifyContent: "flex-end" }}>
              <span style={{ fontSize: "0.56rem", color: "rgba(229,59,246,0.35)", fontFamily: "Poppins, sans-serif", fontWeight: 600, letterSpacing: "0.08em" }}>
                3 REPRESENTANTES · DATOS PROTEGIDOS
              </span>
            </div>
          </div>
        </div>

        {/* ═══ STICKY SAVE BUTTON ═══ */}
        <div
          style={{
            position: "fixed",
            bottom: 0,
            left: 0, right: 0,
            zIndex: 50,
            padding: isMobile ? "12px 16px 20px" : "12px 20px 20px",
            background: "linear-gradient(to top, rgba(10,16,30,0.98) 0%, rgba(10,16,30,0.9) 60%, transparent 100%)",
            backdropFilter: "blur(12px)",
            WebkitBackdropFilter: "blur(12px)",
          }}
        >
          <div style={{ maxWidth: isMobile ? "100%" : 520, margin: "0 auto" }}>
            {saved ? (
              <div
                className="cf-saved"
                style={{
                  display: "flex", alignItems: "center", justifyContent: "center",
                  gap: 10, padding: "16px 0", borderRadius: 16,
                  background: "rgba(59,246,229,0.1)",
                  borderTop:    "1px solid rgba(59,246,229,0.4)",
                  borderRight:  "1px solid rgba(59,246,229,0.4)",
                  borderBottom: "1px solid rgba(59,246,229,0.4)",
                  borderLeft:   "1px solid rgba(59,246,229,0.4)",
                }}
              >
                <svg width="17" height="17" viewBox="0 0 24 24" fill="none">
                  <path d="M20 6L9 17l-5-5" stroke="#3BF6E5" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                <span style={{ fontSize: "0.9rem", fontWeight: 700, color: "#3BF6E5", fontFamily: "Poppins, sans-serif", letterSpacing: "0.04em" }}>
                  ¡Información Guardada!
                </span>
              </div>
            ) : (
              <button
                type="button"
                onClick={handleSave}
                disabled={saving}
                style={{
                  width: "100%", padding: "16px 0", borderRadius: 16,
                  background: saving
                    ? "rgba(255,255,255,0.06)"
                    : "linear-gradient(135deg, #3BF6E5 0%, #8b5cf6 50%, #E53BF6 100%)",
                  border: "none",
                  cursor: saving ? "not-allowed" : "pointer",
                  display: "flex", alignItems: "center", justifyContent: "center", gap: 9,
                  color: saving ? "rgba(148,163,184,0.4)" : "#0F172A",
                  fontWeight: 800, fontSize: "0.95rem",
                  fontFamily: "Poppins, sans-serif",
                  letterSpacing: "0.04em",
                  boxShadow: saving ? "none" : undefined,
                  animation: saving ? "none" : "cfSaveGlow 3s ease-in-out infinite",
                  transition: "all 0.25s ease",
                  opacity: saving ? 0.7 : 1,
                }}
              >
                {saving ? (
                  <>
                    <span style={{ display: "inline-block", width: 16, height: 16, borderRadius: "50%", borderTop: "2.5px solid rgba(148,163,184,0.5)", borderRight: "2.5px solid rgba(255,255,255,0.08)", borderBottom: "2.5px solid rgba(255,255,255,0.08)", borderLeft: "2.5px solid rgba(255,255,255,0.08)", animation: "spin 0.7s linear infinite" }} />
                    Guardando…
                  </>
                ) : (
                  <>
                    <SaveIcon />
                    Guardar Información
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      </div>
    </>
  );
}

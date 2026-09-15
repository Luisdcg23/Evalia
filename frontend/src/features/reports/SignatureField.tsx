import { useId } from "react";
import signatureFontUrl from "@/assets/fonts/AlexBrush-Regular.ttf?url";

/**
 * Campo de firma escrita, con la rúbrica previsualizada en la misma fuente cursiva que estampa el PDF
 * oficial (Alex Brush, SIL OFL 1.1 — ver AlexBrush-OFL.txt). Lo usan las dos firmas del informe: la
 * del técnico al emitirlo y la del coordinador al aprobarlo.
 *
 * La rúbrica no identifica a nadie por sí sola: debajo va siempre el nombre registrado del usuario
 * con sesión, que es lo que el documento imprime como aclaración y lo único que el backend toma como
 * autoría. El campo solo deja constancia de un acto deliberado de firma.
 */
export const SIGNATURE_MAX_LENGTH = 80;
const SIGNATURE_CSS_FAMILY = "Alex Brush";
const FONT = "Poppins, sans-serif";

export default function SignatureField({ value, onChange, signerFullName, role, label, disabled }: {
  value: string;
  onChange: (value: string) => void;
  /** Nombre registrado de quien firma; se imprime como aclaración bajo la rúbrica. */
  signerFullName: string;
  /** Cargo con el que firma, tal como aparecerá en el documento. */
  role: string;
  label: string;
  disabled?: boolean;
}) {
  const inputId = useId();
  const rubric = value.trim();

  return (
    <div style={{ display: "grid", gap: 8 }}>
      <style>{`@font-face { font-family: "${SIGNATURE_CSS_FAMILY}"; src: url("${signatureFontUrl}") format("truetype"); font-display: block; }`}</style>

      <label htmlFor={inputId} style={{ display: "grid", gap: 6, fontSize: "0.76rem", color: "rgba(148,163,184,0.75)", fontFamily: FONT }}>
        {label}
        <input
          id={inputId}
          value={value}
          onChange={event => onChange(event.target.value)}
          placeholder="Nombre y apellido"
          maxLength={SIGNATURE_MAX_LENGTH}
          disabled={disabled}
          autoComplete="off"
          style={{
            width: "100%", boxSizing: "border-box", padding: "0.7rem 0.9rem", borderRadius: 11,
            outline: "none", background: "rgba(255,255,255,0.04)", color: "#f1f5f9",
            border: "1px solid rgba(255,255,255,0.09)", borderTop: "1px solid rgba(59,246,229,0.25)",
            fontFamily: FONT, fontSize: "0.88rem", opacity: disabled ? 0.6 : 1,
          }}
        />
      </label>

      {/* Vista previa de cómo quedará la firma en el documento. */}
      <div style={{ borderRadius: 12, padding: "14px 16px 10px", background: "#f8fafc", border: "1px solid rgba(203,213,225,0.9)" }}>
        <p style={{ fontSize: "0.5rem", fontWeight: 700, color: "#64748b", letterSpacing: "0.12em", fontFamily: FONT, marginBottom: 4 }}>
          ASÍ SE ESTAMPARÁ EN EL INFORME
        </p>
        <div style={{ minHeight: 44, display: "flex", alignItems: "flex-end", justifyContent: "center", borderBottom: "1px solid #0f172a", paddingBottom: 2 }}>
          <span style={{ fontFamily: `"${SIGNATURE_CSS_FAMILY}", cursive`, fontSize: "2rem", lineHeight: 1, color: rubric ? "#1e293b" : "rgba(100,116,139,0.35)" }}>
            {rubric || "Tu firma"}
          </span>
        </div>
        <p style={{ textAlign: "center", fontSize: "0.6rem", fontWeight: 700, color: "#0f172a", fontFamily: FONT, marginTop: 5 }}>{signerFullName}</p>
        <p style={{ textAlign: "center", fontSize: "0.52rem", color: "#64748b", fontFamily: FONT }}>{role}</p>
      </div>
    </div>
  );
}

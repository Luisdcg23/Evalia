import { useEffect, useMemo, useRef, useState } from "react";
import signatureFontDataUri from "@/assets/fonts/MrDafoe-Regular.ttf?inline";
import { buildReportPdf, formatSignedAt, toRawBase64, type ReportData, type ReportSignature } from "./report-pdf";

const FONT = "Poppins, sans-serif";
const SIGNATURE_CSS_FAMILY = "Mr Dafoe";

function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

const btnBase: React.CSSProperties = { padding: "10px 16px", borderRadius: 11, cursor: "pointer", fontFamily: FONT, fontWeight: 700, fontSize: "0.75rem", display: "inline-flex", alignItems: "center", gap: 7, whiteSpace: "nowrap" };
const ghostBtn = (color: string): React.CSSProperties => ({ ...btnBase, background: `${color}14`, border: `1px solid ${color}55`, color });
const solidBtn: React.CSSProperties = { ...btnBase, border: "none", background: "linear-gradient(135deg,#3BF6E5,#06b6d4)", color: "#0F172A", boxShadow: "0 6px 20px rgba(59,246,229,0.3)" };

/* ── Visor de informe (modal) ───────────────────────────── */
export default function ReportViewer({ report, signature, canSign, onSign, onClose, onToast }: {
  report: ReportData;
  signature: ReportSignature | null;
  /** Solo el técnico con sesión activa puede firmar; coordinador y empresa solo descargan. */
  canSign: boolean;
  onSign?: (signature: ReportSignature) => void;
  onClose: () => void;
  onToast?: (message: string) => void;
}) {
  const width = useWidth();
  const isMobile = width < 768;
  const iframeRef = useRef<HTMLIFrameElement>(null);

  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [asking, setAsking] = useState(false);
  const [name, setName] = useState("");
  const [error, setError] = useState("");

  const signatureFont = useMemo(() => ({ base64: toRawBase64(signatureFontDataUri) }), []);
  const doc = useMemo(() => buildReportPdf(report, signature, signatureFont), [report, signature, signatureFont]);

  useEffect(() => {
    const url = URL.createObjectURL(doc.output("blob"));
    setPdfUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [doc]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") { if (asking) setAsking(false); else onClose(); } };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [asking, onClose]);

  const openSignDialog = () => { setName(""); setError(""); setAsking(true); };

  const confirmSignature = () => {
    const signerName = name.trim();
    if (!signerName) { setError("Escribe el nombre con el que vas a firmar."); return; }
    onSign?.({ signerName, signedAt: formatSignedAt(new Date()) });
    setAsking(false);
    onToast?.("Informe firmado · " + report.id);
  };

  const download = () => {
    doc.save(`${report.id}${signature ? "-firmado" : ""}.pdf`);
    onToast?.("PDF descargado · " + report.id);
  };

  const print = () => {
    const win = iframeRef.current?.contentWindow;
    if (win) { win.focus(); win.print(); }
    else if (pdfUrl) window.open(pdfUrl, "_blank");
  };

  return (
    <div onClick={onClose} style={{ position: "fixed", inset: 0, zIndex: 100, background: "rgba(0,0,0,0.7)", backdropFilter: "blur(6px)", display: "flex", alignItems: "center", justifyContent: "center", padding: isMobile ? 0 : 24 }}>
      <style>{`@font-face { font-family: "${SIGNATURE_CSS_FAMILY}"; src: url("${signatureFontDataUri}") format("truetype"); font-display: block; }`}</style>

      <div onClick={e => e.stopPropagation()} role="dialog" aria-label={`Informe ${report.id}`} style={{ width: "100%", maxWidth: 960, height: isMobile ? "100%" : "min(92vh, 900px)", display: "flex", flexDirection: "column", borderRadius: isMobile ? 0 : 20, overflow: "hidden", background: "rgba(8,14,28,0.98)", borderTop: "1px solid rgba(59,246,229,0.3)", borderRight: "1px solid rgba(255,255,255,0.07)", borderBottom: "1px solid rgba(255,255,255,0.07)", borderLeft: "1px solid rgba(255,255,255,0.07)", boxShadow: "0 30px 80px rgba(0,0,0,0.6)" }}>

        {/* Cabecera */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, padding: isMobile ? "14px 16px" : "16px 22px", borderBottom: "1px solid rgba(255,255,255,0.06)", flexShrink: 0 }}>
          <div style={{ minWidth: 0 }}>
            <p style={{ fontSize: "0.56rem", fontWeight: 700, color: "rgba(59,246,229,0.5)", letterSpacing: "0.14em", textTransform: "uppercase", fontFamily: FONT, marginBottom: 2 }}>EVALIA · INFORME DE EVALUACIÓN</p>
            <p style={{ fontSize: "0.95rem", fontWeight: 800, color: "#f1f5f9", fontFamily: FONT, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{report.id} · {report.empresa}</p>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 8, flexShrink: 0 }}>
            {signature
              ? <span style={{ display: "inline-flex", alignItems: "center", gap: 5, padding: "3px 10px", borderRadius: 99, background: "rgba(34,197,94,0.1)", border: "1px solid rgba(34,197,94,0.35)", fontSize: "0.58rem", fontWeight: 700, color: "#22c55e", fontFamily: FONT }}>
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#22c55e" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"/></svg>Firmado
                </span>
              : <span style={{ padding: "3px 10px", borderRadius: 99, background: "rgba(246,229,59,0.08)", border: "1px solid rgba(246,229,59,0.3)", fontSize: "0.58rem", fontWeight: 700, color: "#F6E53B", fontFamily: FONT }}>Sin firmar</span>}
            <button onClick={onClose} title="Cerrar" style={{ width: 34, height: 34, borderRadius: 10, border: "1px solid rgba(255,255,255,0.09)", background: "rgba(255,255,255,0.04)", color: "rgba(148,163,184,0.7)", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M18 6L6 18M6 6l12 12" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"/></svg>
            </button>
          </div>
        </div>

        {/* Vista previa */}
        <div style={{ flex: 1, minHeight: 0, background: "#1e293b", position: "relative" }}>
          {pdfUrl
            ? <iframe ref={iframeRef} title={`Vista previa ${report.id}`} src={pdfUrl} style={{ width: "100%", height: "100%", border: "none", display: "block" }} />
            : <div style={{ position: "absolute", inset: 0, display: "flex", alignItems: "center", justifyContent: "center", color: "rgba(148,163,184,0.5)", fontFamily: FONT, fontSize: "0.8rem" }}>Generando vista previa…</div>}
        </div>

        {/* Acciones */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 10, padding: isMobile ? "12px 14px" : "14px 22px", borderTop: "1px solid rgba(255,255,255,0.06)", flexShrink: 0, flexWrap: "wrap" }}>
          <p style={{ fontSize: "0.62rem", color: "rgba(148,163,184,0.4)", fontFamily: FONT, flex: 1, minWidth: 160 }}>
            {signature
              ? <>Firmado por <span style={{ color: "#f1f5f9", fontWeight: 600 }}>{signature.signerName}</span> · {signature.signedAt}</>
              : canSign ? "El informe queda pendiente de firma hasta que lo firmes." : "Documento firmado por el técnico evaluador. Solo lectura."}
          </p>
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            {pdfUrl && <a href={pdfUrl} target="_blank" rel="noreferrer" style={{ ...ghostBtn("#94a3b8"), textDecoration: "none" }}>Abrir en pestaña</a>}
            <button onClick={print} style={ghostBtn("#94a3b8")}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M6 9V2h12v7M6 18H4a2 2 0 01-2-2v-5a2 2 0 012-2h16a2 2 0 012 2v5a2 2 0 01-2 2h-2M6 14h12v8H6z" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"/></svg>Imprimir
            </button>
            <button onClick={download} style={ghostBtn("#22c55e")}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5 5 5-5M12 15V3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>Descargar PDF
            </button>
            {canSign && !signature && (
              <button onClick={openSignDialog} style={solidBtn}>
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M12 19l7-7 3 3-7 7-3-3z" stroke="currentColor" strokeWidth="2" strokeLinejoin="round"/><path d="M18 13l-1.5-7.5L2 2l3.5 14.5L13 18l5-5zM2 2l7.586 7.586" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>Firmar este informe
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Diálogo de firma */}
      {asking && (
        <div onClick={() => setAsking(false)} style={{ position: "fixed", inset: 0, zIndex: 110, background: "rgba(0,0,0,0.55)", display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}>
          <div onClick={e => e.stopPropagation()} role="dialog" aria-label="Firmar este informe" style={{ width: "100%", maxWidth: 440, borderRadius: 18, padding: "22px 22px 20px", background: "rgba(10,18,36,0.98)", borderTop: "1px solid rgba(59,246,229,0.4)", borderRight: "1px solid rgba(255,255,255,0.08)", borderBottom: "1px solid rgba(255,255,255,0.08)", borderLeft: "1px solid rgba(255,255,255,0.08)", boxShadow: "0 24px 60px rgba(0,0,0,0.6)" }}>
            <p style={{ fontSize: "0.56rem", fontWeight: 700, color: "rgba(59,246,229,0.5)", letterSpacing: "0.14em", textTransform: "uppercase", fontFamily: FONT, marginBottom: 4 }}>FIRMA DEL TÉCNICO</p>
            <h3 style={{ fontSize: "1.05rem", fontWeight: 800, color: "#f1f5f9", fontFamily: FONT, letterSpacing: "-0.02em", marginBottom: 6 }}>¿Firmar este informe?</h3>
            <p style={{ fontSize: "0.72rem", color: "rgba(148,163,184,0.55)", fontFamily: FONT, lineHeight: 1.5, marginBottom: 16 }}>
              Escribe el nombre con el que firmas. Se estampará en cursiva en el recuadro «Firma del técnico» del informe <span style={{ color: "#f1f5f9" }}>{report.id}</span>, junto con tu usuario y la fecha.
            </p>

            <div style={{ position: "relative", marginBottom: 10 }}>
              <input
                autoFocus
                value={name}
                onChange={e => { setName(e.target.value); if (error) setError(""); }}
                onKeyDown={e => { if (e.key === "Enter") confirmSignature(); }}
                placeholder="Nombre y apellido"
                maxLength={80}
                style={{ width: "100%", padding: "1.2rem 1rem 0.55rem", borderRadius: 11, outline: "none", boxSizing: "border-box", background: "rgba(255,255,255,0.04)", borderTop: `1px solid ${error ? "rgba(239,68,68,0.6)" : "rgba(59,246,229,0.25)"}`, borderRight: "1px solid rgba(255,255,255,0.09)", borderBottom: "1px solid rgba(255,255,255,0.09)", borderLeft: "1px solid rgba(255,255,255,0.09)", color: "#f1f5f9", fontFamily: FONT, fontSize: "0.9rem" }}
              />
              <label style={{ position: "absolute", top: "0.5rem", left: "1rem", fontSize: "0.55rem", fontWeight: 700, color: "rgba(59,246,229,0.6)", textTransform: "uppercase", letterSpacing: "0.08em", fontFamily: FONT, pointerEvents: "none" }}>Nombre para la firma</label>
            </div>
            {error && <p style={{ fontSize: "0.65rem", color: "#ef4444", fontFamily: FONT, marginBottom: 8 }}>{error}</p>}

            {/* Vista previa de la firma en cursiva */}
            <div style={{ borderRadius: 12, padding: "14px 16px 10px", background: "#f8fafc", border: "1px solid rgba(203,213,225,0.9)", marginBottom: 16 }}>
              <p style={{ fontSize: "0.5rem", fontWeight: 700, color: "#64748b", letterSpacing: "0.12em", fontFamily: FONT, marginBottom: 4 }}>FIRMA DEL TÉCNICO</p>
              <div style={{ minHeight: 44, display: "flex", alignItems: "flex-end", justifyContent: "center", borderBottom: "1px solid #0f172a", paddingBottom: 2 }}>
                <span style={{ fontFamily: `"${SIGNATURE_CSS_FAMILY}", cursive`, fontSize: "2rem", lineHeight: 1, color: name.trim() ? "#1e293b" : "rgba(100,116,139,0.35)" }}>{name.trim() || "Tu firma"}</span>
              </div>
              <p style={{ textAlign: "center", fontSize: "0.6rem", fontWeight: 700, color: "#0f172a", fontFamily: FONT, marginTop: 5 }}>{report.tecnico}</p>
              <p style={{ textAlign: "center", fontSize: "0.52rem", color: "#64748b", fontFamily: FONT }}>Técnico Evaluador</p>
            </div>

            <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
              <button onClick={() => setAsking(false)} style={ghostBtn("#94a3b8")}>Cancelar</button>
              <button onClick={confirmSignature} style={solidBtn}>Firmar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

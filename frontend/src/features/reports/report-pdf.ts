import { jsPDF } from "jspdf";

/* ── Types ──────────────────────────────────────────────── */
export type ReportRisk = "Bajo" | "Moderado" | "Alto" | "Crítico";

export interface ReportData {
  id: string;
  empresa: string;
  tipo: string;
  riesgo: ReportRisk;
  fecha: string;
  /** Nombre completo del técnico evaluador responsable (nunca un texto fijo). */
  tecnico: string;
  tecnicoEmail?: string;
  municipio?: string;
  direccion?: string;
  contacto?: string;
}

export interface ReportSignature {
  /** Nombre tal como lo escribió quien firma; se estampa en cursiva. */
  signerName: string;
  /** Etiqueta legible de la fecha y hora de la firma. */
  signedAt: string;
}

/** Informe ya firmado por el técnico; coordinador y empresa lo reciben en solo lectura. */
export interface SignedReport { report: ReportData; signature: ReportSignature }

export interface SignatureFont {
  /** Contenido TTF en base64 (sin prefijo data:). */
  base64: string;
}

/* ── Constantes de maquetado (mm, formato Carta) ────────── */
const MARGIN = 18;
const SIGNATURE_FONT_NAME = "MrDafoe";
const INK = { dark: [15, 23, 42], muted: [71, 85, 105], line: [203, 213, 225], soft: [241, 245, 249] } as const;
const RISK_COLOR: Record<ReportRisk, [number, number, number]> = {
  Bajo: [22, 163, 74], Moderado: [217, 119, 6], Alto: [192, 38, 211], Crítico: [220, 38, 38],
};

interface Finding { area: string; hallazgo: string; criticidad: "Alta" | "Media" | "Baja"; estado: "Cumple" | "Parcial" | "Incumple" }

/* Hallazgos de muestra: el informe todavía no se alimenta de la evaluación real,
   así que el nivel de riesgo decide cuántos puntos quedan en incumplimiento. */
export function mockFindings(riesgo: ReportRisk): Finding[] {
  const base: Omit<Finding, "estado">[] = [
    { area: "Instalaciones", hallazgo: "Superficies de contacto con alimentos limpias y en buen estado.", criticidad: "Alta" },
    { area: "Personal",      hallazgo: "Registros de capacitación en higiene del personal manipulador.", criticidad: "Media" },
    { area: "Control de plagas", hallazgo: "Programa documentado de control de plagas con evidencias.", criticidad: "Alta" },
    { area: "Almacenamiento", hallazgo: "Rotación de inventario (PEPS) y control de temperaturas.", criticidad: "Media" },
    { area: "Documentación", hallazgo: "Procedimientos operativos estandarizados de saneamiento (POES).", criticidad: "Baja" },
    { area: "Trazabilidad",  hallazgo: "Identificación de lotes y registros de proveedores.", criticidad: "Media" },
  ];
  const failing = { Bajo: 0, Moderado: 1, Alto: 2, Crítico: 3 }[riesgo];
  const partial = { Bajo: 1, Moderado: 2, Alto: 2, Crítico: 2 }[riesgo];
  return base.map((item, index) => ({
    ...item,
    estado: index < failing ? "Incumple" : index < failing + partial ? "Parcial" : "Cumple",
  }));
}

export function complianceScore(findings: Finding[]): number {
  const points = findings.reduce((sum, f) => sum + (f.estado === "Cumple" ? 1 : f.estado === "Parcial" ? 0.5 : 0), 0);
  return Math.round((points / findings.length) * 100);
}

export function formatSignedAt(date: Date): string {
  return date.toLocaleString("es-DO", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

/** Quita el prefijo `data:...;base64,` si el asset llega como data URI. */
export function toRawBase64(value: string): string {
  const comma = value.indexOf(",");
  return value.startsWith("data:") && comma >= 0 ? value.slice(comma + 1) : value;
}

/* ── Generador ──────────────────────────────────────────── */
export function buildReportPdf(report: ReportData, signature: ReportSignature | null, font?: SignatureFont): jsPDF {
  const doc = new jsPDF({ unit: "mm", format: "letter" });
  const pageW = doc.internal.pageSize.getWidth();
  const pageH = doc.internal.pageSize.getHeight();
  const contentW = pageW - MARGIN * 2;
  let hasSignatureFont = false;

  if (font) {
    doc.addFileToVFS(`${SIGNATURE_FONT_NAME}.ttf`, toRawBase64(font.base64));
    doc.addFont(`${SIGNATURE_FONT_NAME}.ttf`, SIGNATURE_FONT_NAME, "normal");
    hasSignatureFont = true;
  }

  const text = (value: string, x: number, y: number, opts: { size?: number; bold?: boolean; color?: readonly number[]; align?: "left" | "right" | "center" } = {}) => {
    doc.setFont("helvetica", opts.bold ? "bold" : "normal");
    doc.setFontSize(opts.size ?? 10);
    const [r, g, b] = opts.color ?? INK.dark;
    doc.setTextColor(r, g, b);
    doc.text(value, x, y, { align: opts.align ?? "left" });
  };

  /* Cabecera */
  doc.setFillColor(15, 23, 42);
  doc.rect(0, 0, pageW, 28, "F");
  text("EVALIA", MARGIN, 12, { size: 18, bold: true, color: [59, 246, 229] });
  text("Sistema de Evaluación Basada en Riesgo · BPM", MARGIN, 18, { size: 8.5, color: [148, 163, 184] });
  text("INFORME DE EVALUACIÓN", pageW - MARGIN, 12, { size: 11, bold: true, color: [255, 255, 255], align: "right" });
  text(report.id, pageW - MARGIN, 18, { size: 9, color: [148, 163, 184], align: "right" });
  text(`Emitido: ${new Date().toLocaleDateString("es-DO", { day: "2-digit", month: "long", year: "numeric" })}`, pageW - MARGIN, 24, { size: 8, color: [148, 163, 184], align: "right" });

  /* Datos generales */
  let y = 40;
  text("1. Datos generales", MARGIN, y, { size: 12, bold: true });
  y += 3;
  doc.setDrawColor(...INK.dark); doc.setLineWidth(0.5); doc.line(MARGIN, y, pageW - MARGIN, y);
  y += 6;

  const rows: [string, string][] = [
    ["Empresa evaluada", report.empresa],
    ["Tipo de evaluación", report.tipo],
    ["Fecha de inspección", report.fecha],
    ["Nivel de riesgo", report.riesgo],
    ["Técnico evaluador", report.tecnico + (report.tecnicoEmail ? ` · ${report.tecnicoEmail}` : "")],
  ];
  if (report.direccion || report.municipio) rows.splice(1, 0, ["Dirección", [report.direccion, report.municipio].filter(Boolean).join(", ")]);
  if (report.contacto) rows.push(["Contacto en sitio", report.contacto]);

  const labelW = 46;
  rows.forEach(([label, value], index) => {
    const rowH = 7;
    if (index % 2 === 0) { doc.setFillColor(...INK.soft); doc.rect(MARGIN, y - 5, contentW, rowH, "F"); }
    text(label, MARGIN + 3, y, { size: 9, bold: true, color: INK.muted });
    if (label === "Nivel de riesgo") {
      const [r, g, b] = RISK_COLOR[report.riesgo];
      doc.setFillColor(r, g, b); doc.circle(MARGIN + labelW + 1.5, y - 1.2, 1.5, "F");
      text(value, MARGIN + labelW + 5, y, { size: 9, bold: true, color: [r, g, b] });
    } else {
      text(value, MARGIN + labelW, y, { size: 9 });
    }
    y += rowH;
  });

  /* Resumen */
  y += 5;
  text("2. Resumen ejecutivo", MARGIN, y, { size: 12, bold: true });
  y += 3; doc.line(MARGIN, y, pageW - MARGIN, y); y += 6;
  const findings = mockFindings(report.riesgo);
  const score = complianceScore(findings);
  const summary = `Se realizó la ${report.tipo.toLowerCase()} en las instalaciones de ${report.empresa}, verificando el cumplimiento de las Buenas Prácticas de Manufactura conforme a la plantilla vigente. ` +
    `El establecimiento obtuvo un ${score}% de cumplimiento; el nivel de riesgo asignado es ${report.riesgo.toUpperCase()}. ` +
    `Los puntos en incumplimiento requieren plan de acción correctiva en los plazos indicados por el organismo regulador.`;
  doc.setFont("helvetica", "normal"); doc.setFontSize(9.5); doc.setTextColor(...INK.dark);
  const lines = doc.splitTextToSize(summary, contentW) as string[];
  doc.text(lines, MARGIN, y, { lineHeightFactor: 1.4 });
  y += lines.length * 9.5 * 0.3528 * 1.4 + 3;

  /* Hallazgos */
  text("3. Hallazgos de la inspección", MARGIN, y, { size: 12, bold: true });
  y += 3; doc.line(MARGIN, y, pageW - MARGIN, y); y += 6;

  const cols = [
    { title: "#", w: 8 }, { title: "Área", w: 32 }, { title: "Criterio evaluado", w: contentW - 8 - 32 - 22 - 24 }, { title: "Criticidad", w: 22 }, { title: "Resultado", w: 24 },
  ];
  doc.setFillColor(...INK.dark); doc.rect(MARGIN, y - 4.5, contentW, 7, "F");
  let x = MARGIN;
  cols.forEach(col => { text(col.title, x + 2, y, { size: 8, bold: true, color: [255, 255, 255] }); x += col.w; });
  y += 7;

  findings.forEach((f, index) => {
    doc.setFont("helvetica", "normal"); doc.setFontSize(8.5);
    const wrapped = doc.splitTextToSize(f.hallazgo, cols[2].w - 4) as string[];
    const rowH = Math.max(6.5, wrapped.length * 3.8 + 2.5);
    if (index % 2 === 1) { doc.setFillColor(...INK.soft); doc.rect(MARGIN, y - 4.5, contentW, rowH, "F"); }
    doc.setDrawColor(...INK.line); doc.setLineWidth(0.2); doc.line(MARGIN, y - 4.5 + rowH, pageW - MARGIN, y - 4.5 + rowH);
    x = MARGIN;
    text(String(index + 1), x + 2, y, { size: 8.5, color: INK.muted }); x += cols[0].w;
    text(f.area, x + 2, y, { size: 8.5, bold: true }); x += cols[1].w;
    doc.setFont("helvetica", "normal"); doc.setFontSize(8.5); doc.setTextColor(...INK.dark);
    doc.text(wrapped, x + 2, y); x += cols[2].w;
    text(f.criticidad, x + 2, y, { size: 8.5, color: INK.muted }); x += cols[3].w;
    const resultColor: [number, number, number] = f.estado === "Cumple" ? [22, 163, 74] : f.estado === "Parcial" ? [217, 119, 6] : [220, 38, 38];
    text(f.estado, x + 2, y, { size: 8.5, bold: true, color: resultColor });
    y += rowH;
  });

  /* Resultado */
  y += 6;
  doc.setFillColor(...INK.soft); doc.setDrawColor(...INK.line); doc.setLineWidth(0.3);
  doc.roundedRect(MARGIN, y - 5, contentW, 14, 2, 2, "FD");
  text("RESULTADO GLOBAL", MARGIN + 4, y, { size: 7.5, bold: true, color: INK.muted });
  text(`${score}% de cumplimiento`, MARGIN + 4, y + 6, { size: 12, bold: true });
  const [rr, rg, rb] = RISK_COLOR[report.riesgo];
  text(`Riesgo ${report.riesgo}`, pageW - MARGIN - 4, y + 6, { size: 11, bold: true, color: [rr, rg, rb], align: "right" });

  /* Pie: recuadro de firma */
  const boxH = 38;
  const boxY = pageH - MARGIN - boxH - 6;
  doc.setDrawColor(...INK.dark); doc.setLineWidth(0.5);
  doc.roundedRect(MARGIN, boxY, contentW, boxH, 2, 2, "S");
  text("FIRMA DEL TÉCNICO", MARGIN + 4, boxY + 6, { size: 7.5, bold: true, color: INK.muted });

  const lineY = boxY + 25;
  const sigX = MARGIN + contentW / 2;
  doc.setDrawColor(...INK.dark); doc.setLineWidth(0.4);
  doc.line(sigX - 40, lineY, sigX + 40, lineY);

  if (signature) {
    if (hasSignatureFont) doc.setFont(SIGNATURE_FONT_NAME, "normal"); else doc.setFont("times", "italic");
    doc.setFontSize(hasSignatureFont ? 26 : 20);
    doc.setTextColor(30, 41, 59);
    doc.text(signature.signerName, sigX, lineY - 3, { align: "center" });
    text(report.tecnico, sigX, lineY + 5, { size: 8.5, bold: true, align: "center" });
    text(`Técnico Evaluador · Firmado el ${signature.signedAt}`, sigX, lineY + 9.5, { size: 7.5, color: INK.muted, align: "center" });
  } else {
    text("Pendiente de firma", sigX, lineY - 3, { size: 9, color: [148, 163, 184], align: "center" });
    text(report.tecnico, sigX, lineY + 5, { size: 8.5, bold: true, align: "center" });
    text("Técnico Evaluador", sigX, lineY + 9.5, { size: 7.5, color: INK.muted, align: "center" });
  }

  /* Pie de página */
  doc.setDrawColor(...INK.line); doc.setLineWidth(0.2);
  doc.line(MARGIN, pageH - 14, pageW - MARGIN, pageH - 14);
  text(`Evalia · ${report.id} · Documento generado electrónicamente`, MARGIN, pageH - 9, { size: 7, color: INK.muted });
  text("Página 1 de 1", pageW - MARGIN, pageH - 9, { size: 7, color: INK.muted, align: "right" });

  return doc;
}

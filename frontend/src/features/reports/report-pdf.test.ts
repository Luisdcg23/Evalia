import { describe, expect, it } from "vitest";
import { buildReportPdf, complianceScore, mockFindings, toRawBase64, type ReportData } from "./report-pdf";

const report: ReportData = {
  id: "INF-0875", empresa: "Laboratorio Diagnostics RD", tipo: "Inspección BPM", riesgo: "Crítico",
  fecha: "Hace 3 días", tecnico: "Tecnico Evaluador", tecnicoEmail: "tecnico@ebr.local",
};

describe("buildReportPdf", () => {
  it("stamps the logged-in technician name, never a fixed string", () => {
    const pdf = buildReportPdf({ ...report, tecnico: "Ana Perez" }, null).output();
    expect(pdf).toContain("Ana Perez");
    expect(pdf).toContain("Pendiente de firma");
    expect(pdf).not.toContain("Carlos M");
  });

  it("renders the typed name in the signature box once signed", () => {
    const pdf = buildReportPdf(report, { signerName: "A. Perez", signedAt: "14 sep 2026, 15:40" }).output();
    expect(pdf).toContain("A. Perez");
    expect(pdf).toContain("Firmado el 14 sep 2026, 15:40");
    expect(pdf).not.toContain("Pendiente de firma");
  });

  it("produces a single-page Letter document", () => {
    const doc = buildReportPdf(report, null);
    expect(doc.getNumberOfPages()).toBe(1);
    expect(Math.round(doc.internal.pageSize.getWidth())).toBe(216);
  });
});

describe("mock findings", () => {
  it("fails more criteria as the risk level rises", () => {
    const scoreFor = (riesgo: ReportData["riesgo"]) => complianceScore(mockFindings(riesgo));
    expect(scoreFor("Bajo")).toBeGreaterThan(scoreFor("Moderado"));
    expect(scoreFor("Moderado")).toBeGreaterThan(scoreFor("Alto"));
    expect(scoreFor("Alto")).toBeGreaterThan(scoreFor("Crítico"));
  });
});

describe("toRawBase64", () => {
  it("strips a data URI prefix and leaves raw base64 untouched", () => {
    expect(toRawBase64("data:font/ttf;base64,AAEAAA==")).toBe("AAEAAA==");
    expect(toRawBase64("AAEAAA==")).toBe("AAEAAA==");
  });
});

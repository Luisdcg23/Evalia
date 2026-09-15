import { afterEach, expect, it, vi } from "vitest";
import {
  closeCase, downloadOfficialReport, generateOfficialReport, getCurrentReport, getEvaluation,
  getEvaluationResult, issueReport, listEvidence, listReportReviews, reviewReport,
  saveEvaluationResponse, startEvaluation, submitEvaluation, uploadEvidence,
} from "./api";

afterEach(() => vi.unstubAllGlobals());

it("starts an evaluation with a bearer token and no body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 5, caseId: 22, status: "IN_PROGRESS" }) });
  vi.stubGlobal("fetch", fetchMock);

  const instance = await startEvaluation("token", 22);

  expect(instance.id).toBe(5);
  expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5080/api/cases/22/evaluations",
    expect.objectContaining({ method: "POST", headers: { Authorization: "Bearer token" } }),
  );
});

it("maps a 409 on start to a state-conflict message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({ message: "No se permite iniciar." }) }));
  await expect(startEvaluation("token", 22)).rejects.toThrow("No se permite iniciar.");
});

it("maps a 403 on start to an assignment message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 403, json: async () => ({}) }));
  await expect(startEvaluation("token", 22)).rejects.toThrow(/técnico asignado vigente/i);
});

it("reads the progress of an evaluation", async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ id: 5, totalQuestions: 45, answeredQuestions: 10, progressPercentage: 22.22 }),
  });
  vi.stubGlobal("fetch", fetchMock);

  const progress = await getEvaluation("token", 5);

  expect(progress.answeredQuestions).toBe(10);
  expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluations/5", { headers: { Authorization: "Bearer token" } });
});

it("saves a response with the exact optionCode/observations/comments body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, templateItemId: 30, optionCode: "C" }) });
  vi.stubGlobal("fetch", fetchMock);

  await saveEvaluationResponse("token", 5, 30, { optionCode: "c", observations: "  ok  ", comments: "" });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/evaluations/5/responses/30");
  expect(call[1]).toMatchObject({ method: "PUT" });
  expect(JSON.parse(call[1].body as string)).toEqual({ optionCode: "c", observations: "  ok  ", comments: "" });
});

it("maps a 409 on saving a response to the locked-instance message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
    ok: false, status: 409, json: async () => ({ message: "La evaluación ya fue enviada y sus respuestas están bloqueadas." }),
  }));
  await expect(saveEvaluationResponse("token", 5, 30, { optionCode: "C" }))
    .rejects.toThrow(/ya fue enviada/i);
});

it("submits the evaluation with POST and no body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 5, status: "SUBMITTED" }) });
  vi.stubGlobal("fetch", fetchMock);

  const result = await submitEvaluation("token", 5);

  expect(result.status).toBe("SUBMITTED");
  expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5080/api/evaluations/5/submit",
    expect.objectContaining({ method: "POST" }),
  );
});

it("reads the calculated result", async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ bpmPercentage: 86.59, qualificationCode: "CAL-4", nonConformities: [] }),
  });
  vi.stubGlobal("fetch", fetchMock);

  const result = await getEvaluationResult("token", 13);

  expect(result.bpmPercentage).toBeCloseTo(86.59);
  expect(String(fetchMock.mock.calls[0][0])).toBe("http://localhost:5080/api/evaluations/13/result");
});

it("maps a 404 on result to a not-calculated-yet message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404, json: async () => ({}) }));
  await expect(getEvaluationResult("token", 13)).rejects.toThrow(/todavía no tiene un resultado/i);
});

it("uploads evidence as multipart/form-data with the templateItemId and description fields", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, fileName: "foto.jpg" }) });
  vi.stubGlobal("fetch", fetchMock);
  const file = new File([new Uint8Array([1, 2, 3])], "foto.jpg", { type: "image/jpeg" });

  await uploadEvidence("token", 5, { file, templateItemId: 30, description: "  Cámara fría  " });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/evaluations/5/evidence");
  expect(call[1].method).toBe("POST");
  // Sin Content-Type explícito: fetch/el navegador arma el boundary multipart.
  expect(call[1].headers).toEqual({ Authorization: "Bearer token" });
  const body = call[1].body as FormData;
  expect(body).toBeInstanceOf(FormData);
  expect(body.get("file")).toBe(file);
  expect(body.get("templateItemId")).toBe("30");
  expect(body.get("description")).toBe("Cámara fría");
});

it("omits optional multipart fields when not provided", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1 }) });
  vi.stubGlobal("fetch", fetchMock);
  const file = new File([new Uint8Array([1])], "doc.pdf", { type: "application/pdf" });

  await uploadEvidence("token", 5, { file });

  const body = fetchMock.mock.calls[0][1].body as FormData;
  expect(body.get("templateItemId")).toBeNull();
  expect(body.get("description")).toBeNull();
});

it("maps a 409 on evidence upload to the submitted-instance message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
    ok: false, status: 409, json: async () => ({ message: "La evaluación ya fue enviada y no admite nuevas evidencias." }),
  }));
  const file = new File([new Uint8Array([1])], "x.png", { type: "image/png" });
  await expect(uploadEvidence("token", 5, { file })).rejects.toThrow(/no admite nuevas evidencias/i);
});

it("maps a 400 on evidence upload to a policy message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 400, json: async () => ({}) }));
  const file = new File([new Uint8Array([1])], "x.exe", { type: "application/x-msdownload" });
  await expect(uploadEvidence("token", 5, { file })).rejects.toThrow(/JPEG\/PNG\/WEBP\/PDF/i);
});

it("lists evidence metadata", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 1, fileName: "a.jpg" }] });
  vi.stubGlobal("fetch", fetchMock);
  const items = await listEvidence("token", 5);
  expect(items).toHaveLength(1);
  expect(String(fetchMock.mock.calls[0][0])).toBe("http://localhost:5080/api/evaluations/5/evidence");
});

it("issues a report with trimmed fields", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, version: 1 }) });
  vi.stubGlobal("fetch", fetchMock);

  await issueReport("token", 13, { executiveSummary: "  resumen  ", findings: "  hallazgos ", recommendations: " recos " });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/evaluations/13/report");
  expect(JSON.parse(call[1].body as string)).toEqual({
    executiveSummary: "resumen", findings: "hallazgos", recommendations: "recos",
  });
});

it("maps a 409 on issuing a report to a state-conflict message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(issueReport("token", 13, { executiveSummary: "a", findings: "b", recommendations: "c" }))
    .rejects.toThrow(/no admite una nueva versión/i);
});

it("returns null when there is no report yet (404)", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404 }));
  expect(await getCurrentReport("token", 13)).toBeNull();
});

it("lists report reviews in order", async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => [{ id: 1, decision: "CORRECTION_REQUESTED", observations: "Falta evidencia" }],
  });
  vi.stubGlobal("fetch", fetchMock);
  const reviews = await listReportReviews("token", 13);
  expect(reviews[0].decision).toBe("CORRECTION_REQUESTED");
  expect(String(fetchMock.mock.calls[0][0])).toBe("http://localhost:5080/api/evaluations/13/report/reviews");
});

it("reviews a report with the exact decision/observations body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, decision: "APPROVED" }) });
  vi.stubGlobal("fetch", fetchMock);

  await reviewReport("token", 13, { decision: "APPROVED" });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/evaluations/13/report/review");
  expect(call[1]).toMatchObject({ method: "POST" });
  expect(JSON.parse(call[1].body as string)).toEqual({ decision: "APPROVED", observations: "" });
});

it("trims observations when returning or requesting correction", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, decision: "RETURNED" }) });
  vi.stubGlobal("fetch", fetchMock);
  await reviewReport("token", 13, { decision: "RETURNED", observations: "  falta firma  " });
  expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ decision: "RETURNED", observations: "falta firma" });
});

it("maps a 400 on review to the missing-observations message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 400, json: async () => ({ message: "Devolver el informe exige observaciones que indiquen qué corregir." }) }));
  await expect(reviewReport("token", 13, { decision: "RETURNED" })).rejects.toThrow(/observaciones/i);
});

it("maps a 409 on review to a not-in-review message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(reviewReport("token", 13, { decision: "APPROVED" })).rejects.toThrow(/no está en revisión/i);
});

it("generates the official report with a POST and no body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, fileName: "informe-evaluacion-13-v1.pdf" }) });
  vi.stubGlobal("fetch", fetchMock);
  const official = await generateOfficialReport("token", 13);
  expect(official.fileName).toBe("informe-evaluacion-13-v1.pdf");
  expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5080/api/evaluations/13/report/official",
    expect.objectContaining({ method: "POST", headers: { Authorization: "Bearer token" } }),
  );
});

it("maps a 409 on generating the official report to the approval-required message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(generateOfficialReport("token", 13)).rejects.toThrow(/aprobada antes de generar/i);
});

it("downloads the official report as a blob with the suggested filename from Content-Disposition", async () => {
  const blob = new Blob([new Uint8Array([1, 2, 3])], { type: "application/pdf" });
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    headers: new Headers({ "content-disposition": 'attachment; filename="informe-evaluacion-13-v1.pdf"' }),
    blob: async () => blob,
  });
  vi.stubGlobal("fetch", fetchMock);

  const result = await downloadOfficialReport("token", 13);

  expect(result.fileName).toBe("informe-evaluacion-13-v1.pdf");
  expect(result.blob).toBe(blob);
  expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5080/api/evaluations/13/report/official/content",
    { headers: { Authorization: "Bearer token" } },
  );
});

it("falls back to a generic filename when Content-Disposition is missing", async () => {
  const blob = new Blob([new Uint8Array([1])], { type: "application/pdf" });
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, headers: new Headers(), blob: async () => blob }));
  const result = await downloadOfficialReport("token", 13);
  expect(result.fileName).toBe("informe-evaluacion-13.pdf");
});

it("maps a 404 on downloading the official report to a not-generated message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404 }));
  await expect(downloadOfficialReport("token", 13)).rejects.toThrow(/todavía no ha sido generado/i);
});

it("closes a case with the exact result body", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 1, caseId: 22, status: "CLOSED" }) });
  vi.stubGlobal("fetch", fetchMock);

  await closeCase("token", 22, { result: "  Cumple satisfactoriamente  " });

  const call = fetchMock.mock.calls[0];
  expect(String(call[0])).toBe("http://localhost:5080/api/cases/22/close");
  expect(JSON.parse(call[1].body as string)).toEqual({ result: "Cumple satisfactoriamente" });
});

it("maps a 409 on closing a case to the report/pdf-required message", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));
  await expect(closeCase("token", 22, { result: "x" })).rejects.toThrow(/PDF oficial/i);
});

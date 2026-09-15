import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import SignatureField from "../reports/SignatureField";
import type { MyCaseRow } from "../operations/api";
import { buildItemTree, flattenTree } from "../templates/hierarchy";
import {
  BPM_OPTION_CODES, BPM_OPTION_LABEL, getCurrentReport, getEvaluation, getEvaluationResult, issueReport,
  listEvidence, listReportReviews, listTemplateItems, saveEvaluationResponse, startEvaluation, submitEvaluation,
  uploadEvidence,
  type EvaluationEvidence, type EvaluationProgress, type EvaluationReport, type EvaluationReportReview,
  type EvaluationResult, type EvaluationTemplateItem, type SaveResponseInput,
} from "./api";
import { PerItemDebouncer } from "./autosave";

/* ── Tokens visuales (mismos que TemplateAdmin/ResponsiveDashboard) ────────── */
const T = {
  surface: "rgba(255,255,255,0.04)",
  surfaceHi: "rgba(255,255,255,0.07)",
  border: "rgba(255,255,255,0.08)",
  cyan: "#3BF6E5",
  magenta: "#E53BF6",
  yellow: "#F6E53B",
  green: "#4ade80",
  red: "#fda4af",
  txt: "#f1f5f9",
  txtMuted: "rgba(148,163,184,0.65)",
  txtFaint: "rgba(148,163,184,0.38)",
};

const panelStyle: React.CSSProperties = {
  padding: 18, borderRadius: 16, background: T.surface, border: `1px solid ${T.border}`,
  display: "flex", flexDirection: "column", gap: 12,
};
const fieldInput: React.CSSProperties = {
  minHeight: 38, borderRadius: 9, border: `1px solid ${T.border}`, background: "rgba(2,6,23,0.55)",
  color: T.txt, padding: "8px 11px", fontSize: "0.82rem", fontFamily: "Poppins, sans-serif", width: "100%", boxSizing: "border-box",
};
const primaryButton: React.CSSProperties = {
  minHeight: 40, borderRadius: 10, padding: "9px 18px", fontWeight: 700, cursor: "pointer", border: "none",
  background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, color: "#0F172A", fontFamily: "Poppins, sans-serif", fontSize: "0.82rem",
};
const secondaryButton: React.CSSProperties = {
  minHeight: 36, borderRadius: 9, padding: "8px 14px", fontWeight: 700, cursor: "pointer",
  border: `1px solid ${T.cyan}66`, background: "rgba(59,246,229,0.08)", color: T.cyan, fontFamily: "Poppins, sans-serif", fontSize: "0.76rem",
};
const errorBanner: React.CSSProperties = {
  padding: "10px 14px", borderRadius: 10, background: "rgba(244,63,94,0.1)", border: "1px solid rgba(244,63,94,0.35)",
  color: T.red, fontSize: "0.78rem",
};

function message(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

/* ── Cache local de respuestas (best-effort, RETOMAR) ───────────────────────
   No existe un endpoint GET que liste las respuestas ya guardadas de una
   instancia (solo el progreso agregado en GET /api/evaluations/{id}); por eso
   se guarda una copia local para que, al retomar en el mismo navegador, el
   técnico vea lo que ya había capturado. Es una ayuda de UX, no la fuente de
   verdad — el servidor sigue siendo quien decide el resultado. */
interface CachedAnswer { optionCode: string; observations: string; comments: string }
function cacheKey(evaluationId: number) { return `field-evaluation:responses:${evaluationId}`; }
function readCache(evaluationId: number): Record<number, CachedAnswer> {
  try {
    const raw = window.localStorage.getItem(cacheKey(evaluationId));
    return raw ? (JSON.parse(raw) as Record<number, CachedAnswer>) : {};
  } catch {
    return {};
  }
}
function writeCacheEntry(evaluationId: number, itemId: number, answer: CachedAnswer) {
  try {
    const all = readCache(evaluationId);
    all[itemId] = answer;
    window.localStorage.setItem(cacheKey(evaluationId), JSON.stringify(all));
  } catch {
    // almacenamiento no disponible (modo privado, cuota llena): el autosave remoto ya se hizo.
  }
}

interface AnswerState {
  optionCode: string;
  observations: string;
  comments: string;
  saving: boolean;
  error: string;
  syncedAt: string | null;
}

type Phase = "loading" | "start" | "capture" | "result" | "error";

export default function FieldEvaluationScreen({
  accessToken, caseRow, userName, onExit, onCaseChanged,
}: {
  accessToken: string;
  caseRow: MyCaseRow;
  /** Nombre registrado del técnico con sesión; acompaña su rúbrica como aclaración en el informe. */
  userName: string;
  onExit: () => void;
  onCaseChanged: () => void | Promise<void>;
}) {
  const [phase, setPhase] = useState<Phase>("loading");
  const [errorMessage, setErrorMessage] = useState("");
  const [starting, setStarting] = useState(false);

  const [instance, setInstance] = useState<EvaluationProgress | null>(null);
  const [items, setItems] = useState<EvaluationTemplateItem[]>([]);
  const [answers, setAnswers] = useState<Record<number, AnswerState>>({});
  const [evidenceCounts, setEvidenceCounts] = useState<Record<number, number>>({});
  const [evidenceBusy, setEvidenceBusy] = useState<Record<number, boolean>>({});
  const [evidenceErrors, setEvidenceErrors] = useState<Record<number, string>>({});

  const [submitBusy, setSubmitBusy] = useState(false);
  const [confirmingSubmit, setConfirmingSubmit] = useState(false);

  const [result, setResult] = useState<EvaluationResult | null>(null);
  const [report, setReport] = useState<EvaluationReport | null>(null);
  const [reviews, setReviews] = useState<EvaluationReportReview[]>([]);
  const [reportForm, setReportForm] = useState({ executiveSummary: "", findings: "", recommendations: "", signatureName: "" });
  const [reportBusy, setReportBusy] = useState(false);
  const [reportError, setReportError] = useState("");
  const [reportSuccess, setReportSuccess] = useState("");

  const debouncerRef = useRef<PerItemDebouncer<[SaveResponseInput]> | null>(null);

  const persist = useCallback(async (itemId: number, input: SaveResponseInput, evaluationId: number) => {
    setAnswers(prev => ({ ...prev, [itemId]: { ...prev[itemId], saving: true, error: "" } }));
    try {
      await saveEvaluationResponse(accessToken, evaluationId, itemId, input);
      writeCacheEntry(evaluationId, itemId, {
        optionCode: input.optionCode, observations: input.observations ?? "", comments: input.comments ?? "",
      });
      setAnswers(prev => ({ ...prev, [itemId]: { ...prev[itemId], saving: false, error: "", syncedAt: new Date().toISOString() } }));
      setInstance(prev => {
        if (!prev) return prev;
        void getEvaluation(accessToken, evaluationId).then(setInstance).catch(() => {});
        return prev;
      });
    } catch (saveError) {
      setAnswers(prev => ({ ...prev, [itemId]: { ...prev[itemId], saving: false, error: message(saveError, "No fue posible guardar la respuesta.") } }));
    }
  }, [accessToken]);

  useEffect(() => {
    debouncerRef.current = new PerItemDebouncer<[SaveResponseInput]>(
      (itemId, input) => { if (instance) void persist(itemId, input, instance.id); },
      800,
    );
    return () => debouncerRef.current?.cancelAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [instance?.id, persist]);

  const loadCaptureData = useCallback(async (evaluationId: number, templateId: number) => {
    const [treeItems, evidences] = await Promise.all([
      listTemplateItems(accessToken, templateId),
      listEvidence(accessToken, evaluationId).catch(() => [] as EvaluationEvidence[]),
    ]);
    setItems(treeItems);

    const counts: Record<number, number> = {};
    for (const evidence of evidences) {
      if (evidence.templateItemId == null) continue;
      counts[evidence.templateItemId] = (counts[evidence.templateItemId] ?? 0) + 1;
    }
    setEvidenceCounts(counts);

    const cached = readCache(evaluationId);
    const hydrated: Record<number, AnswerState> = {};
    for (const item of treeItems) {
      if (item.itemType !== "QUESTION") continue;
      const cachedAnswer = cached[item.id];
      hydrated[item.id] = {
        optionCode: cachedAnswer?.optionCode ?? "",
        observations: cachedAnswer?.observations ?? "",
        comments: cachedAnswer?.comments ?? "",
        saving: false,
        error: "",
        syncedAt: cachedAnswer ? "cache" : null,
      };
    }
    setAnswers(hydrated);
  }, [accessToken]);

  const loadResultAndReport = useCallback(async (evaluationId: number) => {
    const [resultData, reportData, reviewData] = await Promise.all([
      getEvaluationResult(accessToken, evaluationId),
      getCurrentReport(accessToken, evaluationId),
      listReportReviews(accessToken, evaluationId),
    ]);
    setResult(resultData);
    setReport(reportData);
    setReviews(reviewData);
    setReportForm({
      executiveSummary: reportData?.executiveSummary ?? "",
      findings: reportData?.findings ?? "",
      recommendations: reportData?.recommendations ?? "",
      // La rúbrica no se precarga: cada versión se firma de nuevo, no se hereda la firma anterior.
      signatureName: "",
    });
  }, [accessToken]);

  const loadInstance = useCallback(async (evaluationId: number) => {
    const progress = await getEvaluation(accessToken, evaluationId);
    setInstance(progress);
    if (progress.status === "SUBMITTED") {
      await loadResultAndReport(evaluationId);
      setPhase("result");
    } else {
      await loadCaptureData(evaluationId, progress.templateId);
      setPhase("capture");
    }
  }, [accessToken, loadCaptureData, loadResultAndReport]);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setPhase("loading");
      setErrorMessage("");
      try {
        if (caseRow.evaluationInstanceId == null) {
          if (!cancelled) setPhase("start");
          return;
        }
        await loadInstance(caseRow.evaluationInstanceId);
      } catch (loadError) {
        if (!cancelled) { setErrorMessage(message(loadError, "No fue posible cargar la evaluación.")); setPhase("error"); }
      }
    }
    void load();
    return () => { cancelled = true; };
  }, [caseRow.id, caseRow.evaluationInstanceId, loadInstance]);

  async function handleStart() {
    setStarting(true);
    setErrorMessage("");
    try {
      const created = await startEvaluation(accessToken, caseRow.id);
      await onCaseChanged();
      await loadInstance(created.id);
    } catch (startError) {
      setErrorMessage(message(startError, "No fue posible iniciar la evaluación."));
    } finally {
      setStarting(false);
    }
  }

  function handleAnswerChange(itemId: number, patch: Partial<Pick<AnswerState, "optionCode" | "observations" | "comments">>) {
    setAnswers(prev => {
      const current: AnswerState = prev[itemId] ?? { optionCode: "", observations: "", comments: "", saving: false, error: "", syncedAt: null };
      const next = { ...current, ...patch };
      if (next.optionCode) {
        debouncerRef.current?.schedule(itemId, { optionCode: next.optionCode, observations: next.observations, comments: next.comments });
      }
      return { ...prev, [itemId]: next };
    });
  }

  async function handleEvidenceUpload(itemId: number, file: File) {
    if (!instance) return;
    setEvidenceBusy(prev => ({ ...prev, [itemId]: true }));
    setEvidenceErrors(prev => ({ ...prev, [itemId]: "" }));
    try {
      await uploadEvidence(accessToken, instance.id, { file, templateItemId: itemId });
      setEvidenceCounts(prev => ({ ...prev, [itemId]: (prev[itemId] ?? 0) + 1 }));
    } catch (uploadError) {
      setEvidenceErrors(prev => ({ ...prev, [itemId]: message(uploadError, "No fue posible subir la evidencia.") }));
    } finally {
      setEvidenceBusy(prev => ({ ...prev, [itemId]: false }));
    }
  }

  async function handleSubmit() {
    if (!instance) return;
    debouncerRef.current?.flushAll();
    setSubmitBusy(true);
    setErrorMessage("");
    try {
      await submitEvaluation(accessToken, instance.id);
      await onCaseChanged();
      await loadInstance(instance.id);
    } catch (submitError) {
      setErrorMessage(message(submitError, "No fue posible enviar la evaluación."));
    } finally {
      setSubmitBusy(false);
      setConfirmingSubmit(false);
    }
  }

  async function handleIssueReport() {
    if (!instance) return;
    setReportBusy(true);
    setReportError("");
    setReportSuccess("");
    try {
      const issued = await issueReport(accessToken, instance.id, reportForm);
      setReport(issued);
      const [reviewData] = await Promise.all([listReportReviews(accessToken, instance.id), onCaseChanged()]);
      setReviews(reviewData);
      setReportForm(prev => ({ ...prev, signatureName: "" }));
      setReportSuccess(`Informe versión ${issued.version} emitido y firmado como ${issued.signatureName}.`);
    } catch (issueError) {
      setReportError(message(issueError, "No fue posible emitir el informe."));
    } finally {
      setReportBusy(false);
    }
  }

  const tree = useMemo(() => buildItemTree(items.filter(item => item.isActive)), [items]);
  const flatNodes = useMemo(() => flattenTree(tree), [tree]);

  const lastReview = reviews.length > 0 ? reviews[reviews.length - 1] : null;
  const correctionRequested = lastReview?.decision === "CORRECTION_REQUESTED";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
        <div>
          <p style={{ fontSize: "0.56rem", fontWeight: 700, color: "rgba(59,246,229,0.5)", letterSpacing: "0.14em", textTransform: "uppercase", fontFamily: "Poppins, sans-serif" }}>
            CAS-{caseRow.id} · {caseRow.companyName}
          </p>
          <h2 style={{ fontSize: "1.05rem", fontWeight: 800, color: T.txt, fontFamily: "Poppins, sans-serif" }}>Evaluación en campo</h2>
        </div>
        <button onClick={onExit} style={secondaryButton}>← Volver a evaluaciones</button>
      </div>

      {phase === "loading" && <p style={{ color: T.txtMuted, fontSize: "0.8rem", fontFamily: "Poppins, sans-serif" }}>Cargando…</p>}

      {phase === "error" && (
        <div style={panelStyle}>
          <p role="alert" style={errorBanner}>{errorMessage}</p>
          <button onClick={onExit} style={secondaryButton}>Volver</button>
        </div>
      )}

      {phase === "start" && (
        <div style={panelStyle}>
          <p style={{ color: T.txt, fontSize: "0.88rem", fontFamily: "Poppins, sans-serif", fontWeight: 600 }}>
            Este expediente todavía no tiene una evaluación iniciada.
          </p>
          <p style={{ color: T.txtMuted, fontSize: "0.78rem", fontFamily: "Poppins, sans-serif" }}>
            Al iniciar se toma la plantilla BPM publicada vigente. Solo el técnico asignado actualmente al expediente puede hacerlo.
          </p>
          {errorMessage && <p role="alert" style={errorBanner}>{errorMessage}</p>}
          <button onClick={() => void handleStart()} disabled={starting} style={{ ...primaryButton, opacity: starting ? 0.6 : 1, alignSelf: "flex-start" }}>
            {starting ? "Iniciando…" : "Iniciar evaluación"}
          </button>
        </div>
      )}

      {phase === "capture" && instance && (
        <>
          <div style={panelStyle}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontSize: "0.72rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>
                {instance.answeredQuestions} de {instance.totalQuestions} preguntas respondidas
              </span>
              <span style={{ fontSize: "0.85rem", fontWeight: 800, color: T.cyan, fontFamily: "Poppins, sans-serif" }}>
                {instance.progressPercentage != null ? `${instance.progressPercentage}%` : "—"}
              </span>
            </div>
            <div style={{ height: 8, borderRadius: 99, background: "rgba(255,255,255,0.06)", overflow: "hidden" }}>
              <div style={{
                height: "100%", width: `${instance.progressPercentage ?? 0}%`,
                background: `linear-gradient(90deg, ${T.cyan}, ${T.magenta})`, transition: "width 0.3s ease",
              }} />
            </div>
          </div>

          {errorMessage && <p role="alert" style={errorBanner}>{errorMessage}</p>}

          {items.length === 0
            ? <p style={{ color: T.txtMuted, fontSize: "0.8rem", fontFamily: "Poppins, sans-serif" }}>La plantilla de esta evaluación no tiene preguntas activas.</p>
            : (
              <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                {flatNodes.map(({ item, depth }) => (
                  item.itemType === "QUESTION"
                    ? (
                      <QuestionCard
                        key={item.id}
                        item={item}
                        depth={depth}
                        answer={answers[item.id]}
                        evidenceCount={evidenceCounts[item.id] ?? 0}
                        evidenceBusy={evidenceBusy[item.id] ?? false}
                        evidenceError={evidenceErrors[item.id] ?? ""}
                        onChange={patch => handleAnswerChange(item.id, patch)}
                        onUploadEvidence={file => void handleEvidenceUpload(item.id, file)}
                      />
                    )
                    : (
                      <p key={item.id} style={{
                        marginTop: depth === 0 ? 14 : 8, marginLeft: depth * 14,
                        fontSize: depth === 0 ? "0.82rem" : "0.74rem", fontWeight: depth === 0 ? 800 : 700,
                        color: depth === 0 ? T.txt : T.txtMuted, fontFamily: "Poppins, sans-serif",
                      }}>
                        {item.code} · {item.description}
                      </p>
                    )
                ))}
              </div>
            )}

          <div style={{ ...panelStyle, position: "sticky", bottom: 0 }}>
            {confirmingSubmit
              ? (
                <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                  <p style={{ color: T.txt, fontSize: "0.82rem", fontFamily: "Poppins, sans-serif" }}>
                    Enviar la evaluación es irreversible: las respuestas quedarán bloqueadas y se calculará el resultado. ¿Confirmas el envío?
                  </p>
                  <div style={{ display: "flex", gap: 10 }}>
                    <button onClick={() => void handleSubmit()} disabled={submitBusy} style={{ ...primaryButton, opacity: submitBusy ? 0.6 : 1 }}>
                      {submitBusy ? "Enviando…" : "Sí, finalizar evaluación"}
                    </button>
                    <button onClick={() => setConfirmingSubmit(false)} disabled={submitBusy} style={secondaryButton}>Cancelar</button>
                  </div>
                </div>
              )
              : (
                <button
                  onClick={() => setConfirmingSubmit(true)}
                  disabled={instance.totalQuestions === 0 || instance.answeredQuestions < instance.totalQuestions}
                  style={{
                    ...primaryButton,
                    opacity: (instance.totalQuestions === 0 || instance.answeredQuestions < instance.totalQuestions) ? 0.4 : 1,
                    cursor: (instance.totalQuestions === 0 || instance.answeredQuestions < instance.totalQuestions) ? "not-allowed" : "pointer",
                  }}
                >
                  Finalizar evaluación
                </button>
              )}
            {instance.answeredQuestions < instance.totalQuestions && !confirmingSubmit && (
              <p style={{ color: T.txtFaint, fontSize: "0.7rem", fontFamily: "Poppins, sans-serif" }}>
                Faltan {instance.totalQuestions - instance.answeredQuestions} pregunta(s) por responder antes de poder finalizar.
              </p>
            )}
          </div>
        </>
      )}

      {phase === "result" && instance && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {result
            ? <ResultPanel result={result} />
            : <p style={{ color: T.txtMuted, fontSize: "0.8rem", fontFamily: "Poppins, sans-serif" }}>Esta evaluación no tiene un resultado calculado.</p>}

          <div style={panelStyle}>
            <p style={{ fontSize: "0.62rem", fontWeight: 700, color: "rgba(148,163,184,0.3)", letterSpacing: "0.12em", textTransform: "uppercase", fontFamily: "Poppins, sans-serif" }}>
              Informe de evaluación
            </p>

            {correctionRequested && lastReview && (
              <div style={{ padding: "12px 14px", borderRadius: 10, background: "rgba(229,59,246,0.08)", border: "1px solid rgba(229,59,246,0.3)" }}>
                <p style={{ fontSize: "0.72rem", fontWeight: 800, color: T.magenta, fontFamily: "Poppins, sans-serif", marginBottom: 4 }}>
                  El coordinador solicitó corrección al informe versión {lastReview.reportVersion}
                </p>
                <p style={{ fontSize: "0.78rem", color: T.txt, fontFamily: "Poppins, sans-serif", whiteSpace: "pre-wrap" }}>{lastReview.observations}</p>
                <p style={{ fontSize: "0.68rem", color: T.txtFaint, fontFamily: "Poppins, sans-serif", marginTop: 6 }}>
                  Emitir el informe abajo crea una versión nueva ({(report?.version ?? lastReview.reportVersion) + 1}) para volver a someterla a revisión.
                </p>
              </div>
            )}

            {report && !correctionRequested && (
              <p style={{ fontSize: "0.72rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>
                Versión vigente: {report.version} · estado {report.status}. Emitir de nuevo crea la versión {report.version + 1}.
              </p>
            )}

            <label style={{ display: "grid", gap: 6, fontSize: "0.76rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>
              Resumen ejecutivo
              <textarea
                value={reportForm.executiveSummary}
                onChange={event => setReportForm(prev => ({ ...prev, executiveSummary: event.target.value }))}
                rows={3}
                style={{ ...fieldInput, resize: "vertical" }}
              />
            </label>
            <label style={{ display: "grid", gap: 6, fontSize: "0.76rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>
              Hallazgos
              <textarea
                value={reportForm.findings}
                onChange={event => setReportForm(prev => ({ ...prev, findings: event.target.value }))}
                rows={3}
                style={{ ...fieldInput, resize: "vertical" }}
              />
            </label>
            <label style={{ display: "grid", gap: 6, fontSize: "0.76rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>
              Recomendaciones
              <textarea
                value={reportForm.recommendations}
                onChange={event => setReportForm(prev => ({ ...prev, recommendations: event.target.value }))}
                rows={3}
                style={{ ...fieldInput, resize: "vertical" }}
              />
            </label>

            <SignatureField
              label="Firma del técnico"
              value={reportForm.signatureName}
              onChange={signatureName => setReportForm(prev => ({ ...prev, signatureName }))}
              signerFullName={userName}
              role="Técnico Evaluador"
              disabled={reportBusy}
            />

            {reportError && <p role="alert" style={errorBanner}>{reportError}</p>}
            {reportSuccess && <p style={{ color: T.green, fontSize: "0.78rem", fontFamily: "Poppins, sans-serif" }}>{reportSuccess}</p>}

            <button
              onClick={() => void handleIssueReport()}
              disabled={reportBusy || !reportForm.executiveSummary.trim() || !reportForm.findings.trim()
                || !reportForm.recommendations.trim() || !reportForm.signatureName.trim()}
              style={{ ...primaryButton, alignSelf: "flex-start", opacity: reportBusy ? 0.6 : 1 }}
            >
              {reportBusy ? "Emitiendo…" : report ? "Firmar y emitir nueva versión" : "Firmar y emitir informe"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function QuestionCard({
  item, depth, answer, evidenceCount, evidenceBusy, evidenceError, onChange, onUploadEvidence,
}: {
  item: EvaluationTemplateItem;
  depth: number;
  answer?: AnswerState;
  evidenceCount: number;
  evidenceBusy: boolean;
  evidenceError: string;
  onChange: (patch: Partial<Pick<AnswerState, "optionCode" | "observations" | "comments">>) => void;
  onUploadEvidence: (file: File) => void;
}) {
  const state = answer ?? { optionCode: "", observations: "", comments: "", saving: false, error: "", syncedAt: null };
  return (
    <div style={{ ...panelStyle, marginLeft: depth * 14, gap: 8 }}>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 10, alignItems: "flex-start" }}>
        <p style={{ fontSize: "0.82rem", color: T.txt, fontFamily: "Poppins, sans-serif", fontWeight: 600 }}>
          <span style={{ color: T.txtFaint, fontFamily: "'Courier New',monospace", marginRight: 6 }}>{item.code}</span>
          {item.description}
          {item.isCritical && <span style={{ marginLeft: 8, fontSize: "0.6rem", fontWeight: 800, color: T.magenta }}>CRÍTICA</span>}
        </p>
        <span style={{ fontSize: "0.62rem", color: state.saving ? T.yellow : state.error ? T.red : state.syncedAt ? T.green : T.txtFaint, fontFamily: "Poppins, sans-serif", whiteSpace: "nowrap" }}>
          {state.saving ? "Guardando…" : state.error ? "Error al guardar" : state.syncedAt ? "Guardado" : "Sin responder"}
        </span>
      </div>

      <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
        {BPM_OPTION_CODES.filter(code => code !== "NA" || item.allowsNotApplicable).map(code => {
          const active = state.optionCode === code;
          return (
            <button
              key={code}
              onClick={() => onChange({ optionCode: code })}
              style={{
                padding: "6px 12px", borderRadius: 8, fontSize: "0.72rem", fontWeight: 700, cursor: "pointer",
                fontFamily: "Poppins, sans-serif", border: active ? `1px solid ${T.cyan}` : `1px solid ${T.border}`,
                background: active ? "rgba(59,246,229,0.16)" : "rgba(255,255,255,0.03)", color: active ? T.cyan : T.txtMuted,
              }}
            >
              {code} · {BPM_OPTION_LABEL[code]}
            </button>
          );
        })}
      </div>

      <textarea
        placeholder="Observaciones"
        value={state.observations}
        onChange={event => onChange({ observations: event.target.value })}
        rows={2}
        style={{ ...fieldInput, resize: "vertical" }}
      />
      <textarea
        placeholder="Comentarios"
        value={state.comments}
        onChange={event => onChange({ comments: event.target.value })}
        rows={2}
        style={{ ...fieldInput, resize: "vertical" }}
      />

      {state.error && <p role="alert" style={errorBanner}>{state.error}</p>}

      <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
        <label style={{
          ...secondaryButton, display: "inline-flex", alignItems: "center", gap: 6,
          opacity: !state.optionCode || evidenceBusy ? 0.5 : 1,
          pointerEvents: !state.optionCode || evidenceBusy ? "none" : "auto",
        }}>
          {evidenceBusy ? "Subiendo…" : "Adjuntar evidencia"}
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp,application/pdf"
            style={{ display: "none" }}
            onChange={event => {
              const file = event.target.files?.[0];
              if (file) onUploadEvidence(file);
              event.target.value = "";
            }}
          />
        </label>
        {evidenceCount > 0 && <span style={{ fontSize: "0.68rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>{evidenceCount} evidencia(s) adjunta(s)</span>}
        {!state.optionCode && <span style={{ fontSize: "0.65rem", color: T.txtFaint, fontFamily: "Poppins, sans-serif" }}>Responde la pregunta antes de adjuntar evidencia.</span>}
      </div>
      {evidenceError && <p role="alert" style={errorBanner}>{evidenceError}</p>}
    </div>
  );
}

const SEVERITY_LABEL: Record<string, string> = { CRITICAL: "Crítica", MAJOR: "Mayor", MINOR: "Menor" };
const SEVERITY_COLOR: Record<string, string> = { CRITICAL: "#f43f5e", MAJOR: T.yellow, MINOR: T.txtMuted };

function ResultPanel({ result }: { result: EvaluationResult }) {
  const grouped = useMemo(() => {
    const byServerity = new Map<string, typeof result.nonConformities>();
    for (const nc of result.nonConformities) {
      const list = byServerity.get(nc.severity) ?? [];
      list.push(nc);
      byServerity.set(nc.severity, list);
    }
    return byServerity;
  }, [result.nonConformities]);

  return (
    <div style={panelStyle}>
      <p style={{ fontSize: "0.62rem", fontWeight: 700, color: "rgba(148,163,184,0.3)", letterSpacing: "0.12em", textTransform: "uppercase", fontFamily: "Poppins, sans-serif" }}>
        Resultado calculado (RF-14)
      </p>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(130px, 1fr))", gap: 10 }}>
        {[
          { label: "% BPM", value: `${result.bpmPercentage}%`, color: T.cyan },
          { label: "Calificación", value: `${result.qualificationCode} · ${result.classification}`, color: T.magenta },
          { label: "Riesgo", value: result.riskLevel, color: T.yellow },
          { label: "Frecuencia", value: `${result.frequencyMonths} meses`, color: T.green },
        ].map(stat => (
          <div key={stat.label} style={{ padding: "12px 14px", borderRadius: 12, background: T.surfaceHi, border: `1px solid ${T.border}` }}>
            <p style={{ fontSize: "1.02rem", fontWeight: 900, color: stat.color, fontFamily: "Poppins, sans-serif" }}>{stat.value}</p>
            <p style={{ fontSize: "0.62rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif", marginTop: 3 }}>{stat.label}</p>
          </div>
        ))}
      </div>

      <div style={{ display: "flex", gap: 14, flexWrap: "wrap" }}>
        <span style={{ fontSize: "0.72rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>Críticas: <strong style={{ color: "#f43f5e" }}>{result.criticalCount}</strong></span>
        <span style={{ fontSize: "0.72rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>Mayores: <strong style={{ color: T.yellow }}>{result.majorCount}</strong></span>
        <span style={{ fontSize: "0.72rem", color: T.txtMuted, fontFamily: "Poppins, sans-serif" }}>Menores: <strong style={{ color: T.txtMuted }}>{result.minorCount}</strong></span>
      </div>

      {result.nonConformities.length === 0
        ? <p style={{ color: T.txtMuted, fontSize: "0.78rem", fontFamily: "Poppins, sans-serif" }}>No se registraron no conformidades.</p>
        : (
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {["CRITICAL", "MAJOR", "MINOR"].filter(severity => grouped.has(severity)).map(severity => (
              <div key={severity}>
                <p style={{ fontSize: "0.68rem", fontWeight: 800, color: SEVERITY_COLOR[severity], textTransform: "uppercase", letterSpacing: "0.08em", fontFamily: "Poppins, sans-serif", marginBottom: 6 }}>
                  {SEVERITY_LABEL[severity] ?? severity} ({grouped.get(severity)!.length})
                </p>
                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                  {grouped.get(severity)!.map(nc => (
                    <p key={nc.id} style={{ fontSize: "0.74rem", color: T.txt, fontFamily: "Poppins, sans-serif" }}>
                      {nc.itemCode} · criterio {nc.criterionCode}
                    </p>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
    </div>
  );
}

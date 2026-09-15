import { test, expect, type APIRequestContext } from "@playwright/test";
import {
  API_URL,
  DEMO_USERS,
  apiIsUp,
  apiLogin,
  bearer,
  catalogsAreSeeded,
  newApiContext,
  type ApiSession,
  uniqueSuffix,
} from "./helpers";

/**
 * Ciclo completo del expediente EBR/BPM, etapa por etapa.
 *
 * La interfaz aún no expone pantallas para la ejecución de la evaluación en campo ni para las
 * decisiones del informe (ver README, "Pendiente de implementación"). Mientras tanto, esta spec
 * recorre el ciclo completo por la API, que sí lo cubre de extremo a extremo, con un `test.step`
 * por cada etapa que pide el plan: registro, aprobación, empresa, solicitud, asignación,
 * programación, evaluación (con captura y sincronización), informe, corrección, aprobación y cierre.
 *
 * Requiere, además de API + PostgreSQL, que estén sembrados la plantilla BPM publicada y una
 * versión de reglas de riesgo (los seeders `BpmTemplateSeeder` / `RiskCatalogSeeder` no corren al
 * arrancar). Si no lo están, la spec se marca como omitida en lugar de fallar.
 */
// Los reintentos ante el límite de peticiones de `/api/auth/*` (20/min) esperan a que se abra la
// ventana; el ciclo completo también es largo. Se amplía el tiempo por prueba y por hook.
test.describe.configure({ timeout: 240_000 });

test.describe.serial("Ciclo completo del expediente (API)", () => {
  let api: APIRequestContext;
  let admin: ApiSession;
  let coordinador: ApiSession;
  let tecnico: ApiSession;
  let empresa: ApiSession;

  const suffix = uniqueSuffix();
  let companyId: number;
  let requestId: number;
  let caseId: number;
  let evaluationId: number;

  test.beforeAll(async () => {
    // El límite de `/api/auth/*` (20/min) puede forzar una espera de ~1 min a que se abra la ventana.
    test.setTimeout(240_000);
    api = await newApiContext();
    test.skip(!(await apiIsUp(api)), "La API no responde; arranca API + PostgreSQL.");

    admin = await apiLogin(api, DEMO_USERS.admin.email);
    test.skip(
      !(await catalogsAreSeeded(api, admin)),
      "Faltan la plantilla BPM publicada o la versión de reglas de riesgo. " +
        "Siembra los catálogos (ver README) para ejecutar el ciclo completo.",
    );

    coordinador = await apiLogin(api, DEMO_USERS.coordinador.email);
    tecnico = await apiLogin(api, DEMO_USERS.tecnico.email);
    empresa = await apiLogin(api, DEMO_USERS.empresa.email);
  });

  test.afterAll(async () => {
    await api?.dispose();
  });

  test("recorre registro → cierre", async () => {
    await test.step("registro de usuario con carta de autorización (RF-04/RF-05)", async () => {
      const email = `e2e.ciclo.${suffix}@ebr.local`;
      const register = await api.post(`${API_URL}/api/auth/register`, {
        data: {
          fullName: `Delegado Ciclo ${suffix}`,
          email,
          documentNumber: `CIC-${suffix}`,
          phoneNumber: "8090000001",
          password: "EbrLocal2026!",
          requestedRole: "USUARIO_DELEGADO",
          authorizationLetterFileName: "carta.pdf",
          authorizationLetterMimeType: "application/pdf",
          authorizationLetterSizeBytes: 1024,
          authorizationLetterHash: `sha256-${suffix}`,
          authorizationLetterStorageReference: `registro/${suffix}.pdf`,
        },
      });
      expect(register.ok(), `register => ${register.status()}`).toBeTruthy();

      await test.step("aprobación administrativa", async () => {
        const pending = await api.get(`${API_URL}/api/users/pending`, { headers: bearer(admin) });
        const list = (await pending.json()) as Array<{ id: string; email: string }>;
        const created = list.find((u) => u.email.toLowerCase() === email.toLowerCase());
        expect(created, "el registro debe aparecer como pendiente").toBeTruthy();
        const approve = await api.post(`${API_URL}/api/users/${created!.id}/approve`, { headers: bearer(admin) });
        expect(approve.ok(), `approve => ${approve.status()}`).toBeTruthy();
      });
    });

    await test.step("empresa: se resuelve la empresa autorizada del administrador de empresa", async () => {
      const me = await api.get(`${API_URL}/api/users/me`, { headers: bearer(empresa) });
      const profile = (await me.json()) as { authorizedCompanyIds: number[] };
      expect(profile.authorizedCompanyIds.length, "empresa@ebr.local debe tener una empresa").toBeGreaterThan(0);
      companyId = profile.authorizedCompanyIds[0];
    });

    await test.step("empresa: subcategorías de alimento y factores del establecimiento (prerrequisito del cálculo de riesgo)", async () => {
      // El cálculo de riesgo integrado necesita el riesgo del producto (subcategorías con peligro) y
      // un valor vigente para los cinco factores del establecimiento distintos del BPM. Sin esto el
      // envío de la evaluación se rechaza (RF-14).
      const subcategories = await api.get(`${API_URL}/api/subcategories`, { headers: bearer(admin) });
      const subs = (await subcategories.json()) as Array<{ id: number }>;
      expect(subs.length, "los catálogos sembrados incluyen subcategorías").toBeGreaterThan(0);
      const setSubs = await api.post(`${API_URL}/api/companies/${companyId}/subcategories`, {
        headers: bearer(admin),
        data: { subcategoryIds: subs.slice(0, 3).map((s) => s.id) },
      });
      expect(setSubs.ok(), `asignar subcategorías => ${setSubs.status()}`).toBeTruthy();

      const factorsResponse = await api.get(`${API_URL}/api/catalogs/structural-factors`, { headers: bearer(admin) });
      const factors = (await factorsResponse.json()) as Array<{
        id: number;
        code: string;
        options: Array<{ id: number; order: number }>;
      }>;
      // El cálculo exige la selección completa de los seis factores. El valor del factor BPM que se
      // fije aquí lo sustituye después el envío de la evaluación con la opción derivada de la ficha.
      const factorSelections = factors.map((f) => ({
        factorId: f.id,
        optionId: [...f.options].sort((a, b) => a.order - b.order)[0].id,
      }));
      expect(factorSelections.length, "seis factores del establecimiento").toBe(6);

      const calculate = await api.post(`${API_URL}/api/risk/calculate`, {
        headers: bearer(coordinador),
        data: { companyId, factorSelections },
      });
      expect(calculate.ok(), `calcular riesgo de la empresa => ${calculate.status()} ${await calculate.text()}`).toBeTruthy();
    });

    await test.step("solicitud BPM en borrador con documentación y envío", async () => {
      const create = await api.post(`${API_URL}/api/bpm-requests/`, {
        headers: bearer(empresa),
        data: {
          companyId,
          establishmentType: "Planta procesadora",
          reason: `Solicitud E2E ${suffix}`,
          observations: null,
        },
      });
      expect(create.ok(), `crear solicitud => ${create.status()}`).toBeTruthy();
      requestId = ((await create.json()) as { id: number }).id;

      const submitWithoutDocs = await api.post(`${API_URL}/api/bpm-requests/${requestId}/submit`, {
        headers: bearer(empresa),
      });
      expect(submitWithoutDocs.status(), "sin documentos el envío se rechaza (409)").toBe(409);

      const addDoc = await api.post(`${API_URL}/api/bpm-requests/${requestId}/documents`, {
        headers: bearer(empresa),
        data: {
          documentType: "REGISTRO_MERCANTIL",
          fileName: "registro.pdf",
          mimeType: "application/pdf",
          sizeBytes: 4096,
          hash: `sha256-doc-${suffix}`,
          storageReference: `solicitudes/${suffix}/registro.pdf`,
        },
      });
      expect(addDoc.ok(), `adjuntar documento => ${addDoc.status()}`).toBeTruthy();

      const submit = await api.post(`${API_URL}/api/bpm-requests/${requestId}/submit`, { headers: bearer(empresa) });
      expect(submit.ok(), `enviar solicitud => ${submit.status()}`).toBeTruthy();
      caseId = ((await submit.json()) as { id: number }).id;
      expect(caseId, "el envío crea el expediente").toBeTruthy();
    });

    await test.step("asignación de técnico evaluador (RF-10)", async () => {
      const technicians = await api.get(`${API_URL}/api/technicians`, { headers: bearer(coordinador) });
      const list = (await technicians.json()) as Array<{ id: string; email: string }>;
      const target = list.find((t) => t.email.toLowerCase() === DEMO_USERS.tecnico.email) ?? list[0];
      expect(target, "debe haber al menos un técnico evaluador aprobado").toBeTruthy();

      const assign = await api.post(`${API_URL}/api/cases/${caseId}/assign`, {
        headers: bearer(coordinador),
        data: { technicianId: target!.id, reason: "Asignación E2E" },
      });
      expect(assign.ok(), `asignar => ${assign.status()}`).toBeTruthy();
    });

    await test.step("programación de la evaluación (RF-07/RF-11)", async () => {
      // Fecha futura con desplazamiento aleatorio para no solapar con programaciones de ejecuciones
      // anteriores de esta misma spec sobre el mismo técnico (ventana de solapamiento de 2 h).
      const daysAhead = 30 + Math.floor(Math.random() * 600);
      const when = new Date(Date.now() + daysAhead * 24 * 60 * 60 * 1000).toISOString();
      const schedule = await api.post(`${API_URL}/api/cases/${caseId}/schedule`, {
        headers: bearer(coordinador),
        data: { scheduledFor: when, reason: "Programación E2E", priority: "NORMAL" },
      });
      expect(schedule.ok(), `programar => ${schedule.status()}`).toBeTruthy();
    });

    await test.step("evaluación en campo: iniciar, capturar respuestas y sincronizar", async () => {
      const start = await api.post(`${API_URL}/api/cases/${caseId}/evaluations`, { headers: bearer(tecnico) });
      expect(start.ok(), `iniciar evaluación => ${start.status()}`).toBeTruthy();
      const instance = (await start.json()) as { id: number; templateId: number };
      evaluationId = instance.id;

      // Ítems de la plantilla congelada (solo preguntas).
      const items = await api.get(`${API_URL}/api/evaluation-templates/${instance.templateId}/items`, {
        headers: bearer(admin),
      });
      expect(items.ok(), `items de plantilla => ${items.status()}`).toBeTruthy();
      const questions = ((await items.json()) as Array<{ id: number; itemType: string }>).filter(
        (i) => (i.itemType ?? "").toUpperCase() === "QUESTION",
      );
      expect(questions.length, "la ficha BPM tiene 45 preguntas").toBe(45);

      // Autosave idempotente por pregunta; reguardar la misma respuesta no la duplica
      // (mismo comportamiento que la sincronización de la cola local tras recuperar la red).
      for (const question of questions) {
        const save = await api.put(`${API_URL}/api/evaluations/${evaluationId}/responses/${question.id}`, {
          headers: bearer(tecnico),
          data: { optionCode: "C" },
        });
        expect(save.ok(), `guardar respuesta ${question.id} => ${save.status()}`).toBeTruthy();
      }
      const resync = await api.put(`${API_URL}/api/evaluations/${evaluationId}/responses/${questions[0].id}`, {
        headers: bearer(tecnico),
        data: { optionCode: "C" },
      });
      expect(resync.ok(), "reguardar la misma respuesta es idempotente").toBeTruthy();

      const progress = await api.get(`${API_URL}/api/evaluations/${evaluationId}`, { headers: bearer(tecnico) });
      const state = (await progress.json()) as { answeredQuestions: number; totalQuestions: number };
      expect(state.answeredQuestions).toBe(state.totalQuestions);

      const submit = await api.post(`${API_URL}/api/evaluations/${evaluationId}/submit`, { headers: bearer(tecnico) });
      expect(submit.ok(), `enviar evaluación => ${submit.status()}`).toBeTruthy();
    });

    await test.step("cálculo BPM y riesgo integrado (RF-14)", async () => {
      const result = await api.get(`${API_URL}/api/evaluations/${evaluationId}/result`, { headers: bearer(coordinador) });
      expect(result.ok(), `resultado => ${result.status()}`).toBeTruthy();
      const payload = (await result.json()) as { porcentajeBpm?: number; percentage?: number; bpmPercentage?: number };
      const percentage = payload.bpmPercentage ?? payload.porcentajeBpm ?? payload.percentage;
      expect(percentage, "todas las preguntas en C => 100%").toBe(100);
    });

    await test.step("informe: emisión por el técnico (RF-16)", async () => {
      const issue = await api.post(`${API_URL}/api/evaluations/${evaluationId}/report`, {
        headers: bearer(tecnico),
        data: {
          executiveSummary: "Resumen ejecutivo E2E.",
          findings: "Sin hallazgos relevantes.",
          recommendations: "Mantener el sistema de gestión.",
        },
      });
      expect(issue.ok(), `emitir informe => ${issue.status()}`).toBeTruthy();
    });

    await test.step("corrección: el coordinador devuelve el informe (RF-17/RF-18)", async () => {
      const returnReport = await api.post(`${API_URL}/api/evaluations/${evaluationId}/report/review`, {
        headers: bearer(coordinador),
        data: { decision: "RETURNED", observations: "Ampliar el detalle de los hallazgos." },
      });
      expect(returnReport.ok(), `devolver informe => ${returnReport.status()}`).toBeTruthy();

      const reissue = await api.post(`${API_URL}/api/evaluations/${evaluationId}/report`, {
        headers: bearer(tecnico),
        data: {
          executiveSummary: "Resumen ejecutivo E2E (corregido).",
          findings: "Hallazgos ampliados según la observación del coordinador.",
          recommendations: "Mantener el sistema de gestión.",
        },
      });
      expect(reissue.ok(), `reemitir informe corregido => ${reissue.status()}`).toBeTruthy();
    });

    await test.step("aprobación del informe por el coordinador", async () => {
      const approve = await api.post(`${API_URL}/api/evaluations/${evaluationId}/report/review`, {
        headers: bearer(coordinador),
        data: { decision: "APPROVED", observations: "" },
      });
      expect(approve.ok(), `aprobar informe => ${approve.status()}`).toBeTruthy();
    });

    await test.step("informe oficial en PDF (RF-19)", async () => {
      const official = await api.post(`${API_URL}/api/evaluations/${evaluationId}/report/official`, {
        headers: bearer(coordinador),
      });
      expect(official.ok(), `generar PDF oficial => ${official.status()}`).toBeTruthy();

      const content = await api.get(`${API_URL}/api/evaluations/${evaluationId}/report/official/content`, {
        headers: bearer(coordinador),
      });
      expect(content.ok(), `descargar PDF oficial => ${content.status()}`).toBeTruthy();
      expect(content.headers()["content-type"] ?? "").toContain("pdf");
    });

    await test.step("cierre inmutable del expediente", async () => {
      const close = await api.post(`${API_URL}/api/cases/${caseId}/close`, {
        headers: bearer(coordinador),
        data: { result: "CUMPLE" },
      });
      expect(close.ok(), `cerrar expediente => ${close.status()}`).toBeTruthy();

      const detail = await api.get(`${API_URL}/api/cases/${caseId}`, { headers: bearer(coordinador) });
      const state = (await detail.json()) as { status: string };
      expect(state.status).toBe("CLOSED");

      // Un segundo cierre devuelve el cierre existente sin reabrir nada.
      const second = await api.post(`${API_URL}/api/cases/${caseId}/close`, {
        headers: bearer(coordinador),
        data: { result: "CUMPLE" },
      });
      expect(second.ok()).toBeTruthy();
    });
  });
});

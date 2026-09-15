import { useCallback, useEffect, useMemo, useState } from "react";
import {
  activateTemplate, createTemplate, createTemplateItem, createTemplateVersion, deleteTemplateItem,
  getActiveTemplate, listTemplateItems, listTemplates, publishTemplate, updateTemplateItem,
  type EvaluationTemplate, type EvaluationTemplateItem, type UpsertTemplateItemInput,
} from "./api";
import { allowedChildTypes, buildItemTree, flattenTree, isValidItemType, TEMPLATE_ITEM_TYPE_LABEL } from "./hierarchy";

/* ── Tokens visuales (mismos que ResponsiveDashboard, definidos localmente para
   mantener este módulo autocontenido) ─────────────────────────────────────── */
const T = {
  surface: "rgba(255,255,255,0.04)",
  surfaceHi: "rgba(255,255,255,0.07)",
  border: "rgba(255,255,255,0.08)",
  cyan: "#3BF6E5",
  magenta: "#E53BF6",
  yellow: "#F6E53B",
  txt: "#f1f5f9",
  txtMuted: "rgba(148,163,184,0.65)",
  txtFaint: "rgba(148,163,184,0.38)",
};

const STATUS_META: Record<string, { label: string; color: string }> = {
  DRAFT: { label: "Borrador", color: T.yellow },
  PUBLISHED: { label: "Publicada", color: "#4ade80" },
  RETIRED: { label: "Retirada", color: T.txtMuted },
};
const statusMeta = (status: string) => STATUS_META[status] ?? { label: status, color: T.txtMuted };

interface ItemFormState {
  mode: "create" | "edit";
  itemId?: number;
  parentId: number | null;
  code: string;
  description: string;
  itemType: string;
  order: string;
  weight: string;
  isRequired: boolean;
  isCritical: boolean;
  allowsNotApplicable: boolean;
  responseType: string;
  versionToken?: string;
}

const panelStyle: React.CSSProperties = {
  padding: 20, borderRadius: 18, background: T.surface, border: `1px solid ${T.border}`,
  display: "flex", flexDirection: "column", gap: 14,
};
const fieldLabel: React.CSSProperties = { display: "grid", gap: 6, color: T.txtMuted, fontSize: "0.78rem", fontWeight: 600 };
const fieldInput: React.CSSProperties = {
  minHeight: 40, borderRadius: 10, border: `1px solid ${T.border}`, background: "rgba(2,6,23,0.55)",
  color: T.txt, padding: "8px 12px", fontSize: "0.85rem", fontFamily: "Poppins, sans-serif",
};
const primaryButton: React.CSSProperties = {
  minHeight: 40, borderRadius: 10, padding: "9px 18px", fontWeight: 700, cursor: "pointer", border: "none",
  background: `linear-gradient(135deg, ${T.magenta}, ${T.cyan})`, color: "#fff", fontFamily: "Poppins, sans-serif", fontSize: "0.8rem",
};
const secondaryButton: React.CSSProperties = {
  minHeight: 40, borderRadius: 10, padding: "9px 18px", fontWeight: 700, cursor: "pointer",
  border: `1px solid ${T.cyan}66`, background: "rgba(59,246,229,0.08)", color: T.cyan, fontFamily: "Poppins, sans-serif", fontSize: "0.8rem",
};
const dangerButton: React.CSSProperties = {
  minHeight: 32, borderRadius: 8, padding: "6px 12px", fontWeight: 700, cursor: "pointer",
  border: `1px solid ${T.magenta}66`, background: "rgba(229,59,246,0.08)", color: T.magenta, fontFamily: "Poppins, sans-serif", fontSize: "0.72rem",
};
const errorBanner: React.CSSProperties = {
  padding: "10px 14px", borderRadius: 10, background: "rgba(244,63,94,0.1)", border: "1px solid rgba(244,63,94,0.35)",
  color: "#fda4af", fontSize: "0.78rem",
};

function emptyForm(parentId: number | null, defaultType: string): ItemFormState {
  return {
    mode: "create", parentId, code: "", description: "", itemType: defaultType, order: "10",
    weight: "", isRequired: false, isCritical: false, allowsNotApplicable: true, responseType: "",
  };
}

export default function TemplateAdmin({ accessToken }: { accessToken: string }) {
  const [templates, setTemplates] = useState<EvaluationTemplate[]>([]);
  const [templatesLoading, setTemplatesLoading] = useState(true);
  const [templatesError, setTemplatesError] = useState("");
  const [selectedId, setSelectedId] = useState<number | null>(null);

  const [newTemplateName, setNewTemplateName] = useState("");
  const [creatingTemplate, setCreatingTemplate] = useState(false);
  const [createTemplateError, setCreateTemplateError] = useState("");

  const [items, setItems] = useState<EvaluationTemplateItem[]>([]);
  const [itemsLoading, setItemsLoading] = useState(false);
  const [itemsError, setItemsError] = useState("");

  const [form, setForm] = useState<ItemFormState | null>(null);
  const [formError, setFormError] = useState("");
  const [savingItem, setSavingItem] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [actionError, setActionError] = useState("");

  const [confirmingPublish, setConfirmingPublish] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [creatingVersion, setCreatingVersion] = useState(false);

  const [activeTemplateId, setActiveTemplateId] = useState<number | null>(null);
  const [activeLoading, setActiveLoading] = useState(true);
  const [activeError, setActiveError] = useState("");
  const [activating, setActivating] = useState(false);

  const selected = templates.find(item => item.id === selectedId) ?? null;
  const isDraft = selected?.status === "DRAFT";
  const isActive = selected !== null && selected.id === activeTemplateId;

  const tree = useMemo(() => buildItemTree(items), [items]);
  const flat = useMemo(() => flattenTree(tree), [tree]);

  const refreshTemplates = useCallback(async () => {
    setTemplatesLoading(true);
    setTemplatesError("");
    try {
      const result = await listTemplates(accessToken);
      setTemplates(result);
    } catch (error) {
      setTemplatesError(error instanceof Error ? error.message : "No fue posible cargar las plantillas.");
    } finally {
      setTemplatesLoading(false);
    }
  }, [accessToken]);

  useEffect(() => { void refreshTemplates(); }, [refreshTemplates]);

  const refreshActive = useCallback(async () => {
    setActiveLoading(true);
    setActiveError("");
    try {
      const result = await getActiveTemplate(accessToken);
      setActiveTemplateId(result.templateId);
    } catch (error) {
      setActiveError(error instanceof Error ? error.message : "No fue posible consultar la plantilla activa.");
    } finally {
      setActiveLoading(false);
    }
  }, [accessToken]);

  useEffect(() => { void refreshActive(); }, [refreshActive]);

  const refreshItems = useCallback(async (templateId: number) => {
    setItemsLoading(true);
    setItemsError("");
    try {
      const result = await listTemplateItems(accessToken, templateId);
      setItems(result);
    } catch (error) {
      setItemsError(error instanceof Error ? error.message : "No fue posible cargar la estructura de la plantilla.");
    } finally {
      setItemsLoading(false);
    }
  }, [accessToken]);

  useEffect(() => {
    setForm(null);
    setFormError("");
    setActionError("");
    setConfirmingPublish(false);
    if (selectedId === null) { setItems([]); return; }
    void refreshItems(selectedId);
  }, [selectedId, refreshItems]);

  const handleCreateTemplate = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!newTemplateName.trim()) { setCreateTemplateError("El nombre de la plantilla es obligatorio."); return; }
    setCreatingTemplate(true);
    setCreateTemplateError("");
    try {
      const created = await createTemplate(accessToken, newTemplateName);
      setTemplates(current => [created, ...current]);
      setSelectedId(created.id);
      setNewTemplateName("");
    } catch (error) {
      setCreateTemplateError(error instanceof Error ? error.message : "No fue posible crear la plantilla.");
    } finally {
      setCreatingTemplate(false);
    }
  };

  const startCreateItem = (parentId: number | null) => {
    const parentType = parentId === null ? null : items.find(item => item.id === parentId)?.itemType ?? null;
    const options = allowedChildTypes(parentType);
    const siblingCount = items.filter(item => item.parentId === parentId).length;
    const next = emptyForm(parentId, options[0] ?? "");
    next.order = String((siblingCount + 1) * 10);
    setForm(next);
    setFormError("");
  };

  const startEditItem = (item: EvaluationTemplateItem) => {
    setForm({
      mode: "edit", itemId: item.id, parentId: item.parentId, code: item.code, description: item.description,
      itemType: item.itemType, order: String(item.order), weight: item.weight !== null ? String(item.weight) : "",
      isRequired: item.isRequired, isCritical: item.isCritical, allowsNotApplicable: item.allowsNotApplicable,
      responseType: item.responseType ?? "", versionToken: item.versionToken,
    });
    setFormError("");
  };

  const closeForm = () => { setForm(null); setFormError(""); };

  const submitForm = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form || !selected) return;
    const parentType = form.parentId === null ? null : items.find(item => item.id === form.parentId)?.itemType ?? null;
    if (!form.code.trim() || !form.description.trim()) { setFormError("Código y descripción son obligatorios."); return; }
    if (!form.itemType) { setFormError("Selecciona un tipo de ítem."); return; }
    if (!isValidItemType(form.itemType, parentType)) {
      const options = allowedChildTypes(parentType).map(type => TEMPLATE_ITEM_TYPE_LABEL[type]);
      setFormError(`Ese tipo no es válido en este nivel. Tipos permitidos aquí: ${options.join(", ") || "ninguno"}.`);
      return;
    }
    const orderValue = Number(form.order);
    if (!Number.isFinite(orderValue)) { setFormError("El orden debe ser un número."); return; }
    const weightValue = form.weight.trim() ? Number(form.weight) : null;
    if (form.weight.trim() && !Number.isFinite(weightValue)) { setFormError("El peso debe ser un número."); return; }

    const input: UpsertTemplateItemInput = {
      code: form.code, description: form.description, itemType: form.itemType, order: orderValue,
      parentId: form.parentId, weight: weightValue, isRequired: form.isRequired, isCritical: form.isCritical,
      allowsNotApplicable: form.allowsNotApplicable, responseType: form.responseType.trim() || null,
      versionToken: form.versionToken,
    };

    setSavingItem(true);
    setFormError("");
    try {
      if (form.mode === "create") {
        const created = await createTemplateItem(accessToken, selected.id, input);
        setItems(current => [...current, created]);
      } else if (form.itemId) {
        const updated = await updateTemplateItem(accessToken, selected.id, form.itemId, input);
        setItems(current => current.map(item => item.id === updated.id ? updated : item));
      }
      setForm(null);
    } catch (error) {
      setFormError(error instanceof Error ? error.message : "No fue posible guardar el ítem.");
    } finally {
      setSavingItem(false);
    }
  };

  const handleDelete = async (item: EvaluationTemplateItem) => {
    if (!selected) return;
    setActionError("");
    setDeletingId(item.id);
    try {
      await deleteTemplateItem(accessToken, selected.id, item.id);
      setItems(current => current.filter(candidate => candidate.id !== item.id));
      if (form?.itemId === item.id) setForm(null);
    } catch (error) {
      setActionError(error instanceof Error ? error.message : "No fue posible borrar el ítem.");
    } finally {
      setDeletingId(null);
    }
  };

  const handlePublish = async () => {
    if (!selected) return;
    setPublishing(true);
    setActionError("");
    try {
      const updated = await publishTemplate(accessToken, selected.id);
      setTemplates(current => current.map(item => item.id === updated.id ? updated : item));
      setConfirmingPublish(false);
    } catch (error) {
      setActionError(error instanceof Error ? error.message : "No fue posible publicar la plantilla.");
    } finally {
      setPublishing(false);
    }
  };

  const handleActivate = async () => {
    if (!selected) return;
    setActivating(true);
    setActionError("");
    try {
      const result = await activateTemplate(accessToken, selected.id);
      setActiveTemplateId(result.templateId);
    } catch (error) {
      setActionError(error instanceof Error ? error.message : "No fue posible activar la plantilla.");
    } finally {
      setActivating(false);
    }
  };

  const handleCreateVersion = async () => {
    if (!selected) return;
    setCreatingVersion(true);
    setActionError("");
    try {
      const created = await createTemplateVersion(accessToken, selected.id);
      setTemplates(current => [created, ...current]);
      setSelectedId(created.id);
    } catch (error) {
      setActionError(error instanceof Error ? error.message : "No fue posible crear la nueva versión.");
    } finally {
      setCreatingVersion(false);
    }
  };

  const formParentType = form && form.parentId !== null ? items.find(item => item.id === form.parentId)?.itemType ?? null : null;
  const formAllowedTypes = form ? allowedChildTypes(formParentType) : [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
      <div>
        <p style={{ fontSize: "0.65rem", fontWeight: 700, color: T.txtFaint, letterSpacing: "0.14em", textTransform: "uppercase", marginBottom: 4 }}>
          Gestión de contenido normativo
        </p>
        <h2 style={{ fontSize: "1.4rem", fontWeight: 800, color: T.txt, letterSpacing: "-0.02em" }}>Plantillas de evaluación</h2>
        <p style={{ fontSize: "0.78rem", color: T.txtMuted, marginTop: 4 }}>
          Crea y edita el árbol de preguntas de una plantilla mientras esté en borrador. Publicar una versión la
          vuelve inmutable de forma permanente.
        </p>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "minmax(260px, 1fr) minmax(360px, 2fr)", gap: 18 }} className="template-admin-grid">
        {/* ── Columna izquierda: listado de plantillas ─────────────────── */}
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <form onSubmit={handleCreateTemplate} style={panelStyle}>
            <h3 style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt }}>Nueva plantilla</h3>
            <label style={fieldLabel}>
              Nombre
              <input
                value={newTemplateName}
                onChange={event => setNewTemplateName(event.target.value)}
                placeholder="Ej. Ficha de prueba — no usar en producción"
                style={fieldInput}
              />
            </label>
            {createTemplateError && <p role="alert" style={errorBanner}>{createTemplateError}</p>}
            <button type="submit" disabled={creatingTemplate} style={{ ...primaryButton, opacity: creatingTemplate ? 0.6 : 1 }}>
              {creatingTemplate ? "Creando…" : "Crear borrador"}
            </button>
          </form>

          <div style={panelStyle}>
            <h3 style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt }}>Plantillas existentes</h3>
            {templatesLoading && <p style={{ fontSize: "0.78rem", color: T.txtMuted }}>Cargando plantillas…</p>}
            {!templatesLoading && templatesError && <p role="alert" style={errorBanner}>{templatesError}</p>}
            {!templatesLoading && !templatesError && templates.length === 0 && (
              <div style={{ padding: "22px 14px", textAlign: "center", borderRadius: 12, border: `1px dashed ${T.border}` }}>
                <p style={{ fontSize: "0.78rem", color: T.txtFaint }}>Todavía no hay plantillas. Crea la primera arriba.</p>
              </div>
            )}
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {templates.map(template => {
                const meta = statusMeta(template.status);
                const active = template.id === selectedId;
                return (
                  <button
                    key={template.id}
                    onClick={() => setSelectedId(template.id)}
                    style={{
                      textAlign: "left", padding: "12px 14px", borderRadius: 12, cursor: "pointer",
                      background: active ? "rgba(59,246,229,0.1)" : T.surfaceHi,
                      border: active ? `1px solid ${T.cyan}66` : `1px solid ${T.border}`,
                      display: "flex", flexDirection: "column", gap: 4, fontFamily: "Poppins, sans-serif",
                    }}
                  >
                    <span style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt }}>{template.name}</span>
                    <span style={{ display: "flex", gap: 8, alignItems: "center", fontSize: "0.68rem", color: T.txtMuted }}>
                      <span>v{template.version}</span>
                      <span style={{ padding: "2px 8px", borderRadius: 99, background: `${meta.color}22`, border: `1px solid ${meta.color}55`, color: meta.color, fontWeight: 700 }}>
                        {meta.label}
                      </span>
                      {template.id === activeTemplateId && (
                        <span style={{ padding: "2px 8px", borderRadius: 99, background: `${T.cyan}22`, border: `1px solid ${T.cyan}55`, color: T.cyan, fontWeight: 700 }}>
                          Activa para evaluaciones
                        </span>
                      )}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>
        </div>

        {/* ── Columna derecha: detalle de la plantilla seleccionada ────── */}
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          {!selected && (
            <div style={{ ...panelStyle, alignItems: "center", justifyContent: "center", minHeight: 220 }}>
              <p style={{ fontSize: "0.82rem", color: T.txtFaint }}>Selecciona una plantilla de la izquierda, o crea una nueva.</p>
            </div>
          )}

          {selected && (
            <>
              <div style={panelStyle}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 10 }}>
                  <div>
                    <p style={{ fontSize: "1rem", fontWeight: 800, color: T.txt }}>{selected.name}</p>
                    <p style={{ fontSize: "0.72rem", color: T.txtMuted }}>Versión {selected.version} · {items.length} ítem(s)</p>
                  </div>
                  <span style={{
                    padding: "4px 12px", borderRadius: 99, fontSize: "0.72rem", fontWeight: 700,
                    background: `${statusMeta(selected.status).color}22`, border: `1px solid ${statusMeta(selected.status).color}55`,
                    color: statusMeta(selected.status).color,
                  }}>
                    {statusMeta(selected.status).label}
                  </span>
                </div>

                {!isDraft && (
                  <div style={{ padding: "12px 14px", borderRadius: 12, background: "rgba(148,163,184,0.08)", border: `1px solid ${T.border}` }}>
                    <p style={{ fontSize: "0.78rem", color: T.txtMuted }}>
                      Esta versión está {statusMeta(selected.status).label.toLowerCase()} y es <strong>inmutable</strong>: solo
                      puede consultarse. Para modificar su contenido, crea una nueva versión en borrador clonada de esta.
                    </p>
                    <button
                      type="button"
                      onClick={() => void handleCreateVersion()}
                      disabled={creatingVersion || selected.status !== "PUBLISHED"}
                      style={{ ...secondaryButton, marginTop: 10, opacity: creatingVersion || selected.status !== "PUBLISHED" ? 0.6 : 1 }}
                      title={selected.status !== "PUBLISHED" ? "Solo una versión publicada puede clonarse." : undefined}
                    >
                      {creatingVersion ? "Creando versión…" : "Crear nueva versión editable"}
                    </button>
                  </div>
                )}

                {selected.status === "PUBLISHED" && (
                  <div style={{
                    padding: "12px 14px", borderRadius: 12,
                    background: isActive ? "rgba(59,246,229,0.08)" : "rgba(148,163,184,0.08)",
                    border: `1px solid ${isActive ? `${T.cyan}55` : T.border}`,
                    display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 10,
                  }}>
                    <p style={{ fontSize: "0.78rem", color: isActive ? T.cyan : T.txtMuted, fontWeight: isActive ? 700 : 400 }}>
                      {isActive
                        ? "Esta es la plantilla activa: las evaluaciones nuevas que inicien los técnicos usan esta versión."
                        : "Esta versión está publicada pero no es la plantilla activa: las evaluaciones nuevas no la usan hasta que se active."}
                    </p>
                    {!isActive && (
                      <button
                        type="button"
                        onClick={() => void handleActivate()}
                        disabled={activating || activeLoading}
                        style={{ ...primaryButton, opacity: activating || activeLoading ? 0.6 : 1 }}
                      >
                        {activating ? "Activando…" : "Activar para evaluaciones"}
                      </button>
                    )}
                  </div>
                )}
                {activeError && <p role="alert" style={errorBanner}>{activeError}</p>}

                {isDraft && !confirmingPublish && (
                  <button type="button" onClick={() => setConfirmingPublish(true)} disabled={items.length === 0} style={{ ...primaryButton, opacity: items.length === 0 ? 0.5 : 1, alignSelf: "flex-start" }}>
                    Publicar plantilla
                  </button>
                )}
                {isDraft && confirmingPublish && (
                  <div style={{ padding: "14px", borderRadius: 12, background: "rgba(245,158,11,0.08)", border: "1px solid rgba(245,158,11,0.4)", display: "flex", flexDirection: "column", gap: 10 }}>
                    <p style={{ fontSize: "0.8rem", color: T.txt, fontWeight: 700 }}>
                      ¿Confirmas publicar &quot;{selected.name}&quot; v{selected.version}?
                    </p>
                    <p style={{ fontSize: "0.75rem", color: T.txtMuted }}>
                      Después de publicar, esta versión queda <strong>inmutable de forma permanente</strong>: ya no
                      podrás crear, editar ni borrar sus ítems. Si necesitas cambiarla más adelante, tendrás que crear
                      una versión nueva.
                    </p>
                    <div style={{ display: "flex", gap: 10 }}>
                      <button type="button" onClick={() => void handlePublish()} disabled={publishing} style={{ ...primaryButton, opacity: publishing ? 0.6 : 1 }}>
                        {publishing ? "Publicando…" : "Sí, publicar de forma definitiva"}
                      </button>
                      <button type="button" onClick={() => setConfirmingPublish(false)} disabled={publishing} style={{ ...secondaryButton, borderColor: T.border, color: T.txtMuted }}>
                        Cancelar
                      </button>
                    </div>
                  </div>
                )}
                {isDraft && items.length === 0 && !confirmingPublish && (
                  <p style={{ fontSize: "0.72rem", color: T.txtFaint }}>Agrega al menos un ítem antes de publicar.</p>
                )}

                {actionError && <p role="alert" style={errorBanner}>{actionError}</p>}
              </div>

              <div style={panelStyle}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                  <h3 style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt }}>Estructura</h3>
                  {isDraft && (
                    <button type="button" onClick={() => startCreateItem(null)} style={{ ...secondaryButton, minHeight: 32, padding: "6px 12px" }}>
                      + Capítulo raíz
                    </button>
                  )}
                </div>

                {itemsLoading && <p style={{ fontSize: "0.78rem", color: T.txtMuted }}>Cargando estructura…</p>}
                {!itemsLoading && itemsError && <p role="alert" style={errorBanner}>{itemsError}</p>}
                {!itemsLoading && !itemsError && items.length === 0 && (
                  <div style={{ padding: "22px 14px", textAlign: "center", borderRadius: 12, border: `1px dashed ${T.border}` }}>
                    <p style={{ fontSize: "0.78rem", color: T.txtFaint }}>
                      {isDraft ? "Esta plantilla todavía no tiene ítems. Comienza agregando un capítulo." : "Esta versión no tiene ítems."}
                    </p>
                  </div>
                )}

                {!itemsLoading && !itemsError && flat.length > 0 && (
                  <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                    {flat.map(({ item, depth }) => {
                      const childOptions = allowedChildTypes(item.itemType);
                      return (
                        <div
                          key={item.id}
                          style={{
                            marginLeft: Math.min(depth * 18, 90), padding: "10px 12px", borderRadius: 10,
                            border: `1px solid ${T.border}`, borderLeft: `3px solid ${item.itemType === "QUESTION" ? T.magenta : T.cyan}`,
                            background: T.surfaceHi, display: "flex", alignItems: "center", justifyContent: "space-between", gap: 10, flexWrap: "wrap",
                          }}
                        >
                          <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 180 }}>
                            <span style={{ fontSize: "0.65rem", fontWeight: 800, color: item.itemType === "QUESTION" ? T.magenta : T.cyan, letterSpacing: "0.06em" }}>
                              {TEMPLATE_ITEM_TYPE_LABEL[item.itemType as keyof typeof TEMPLATE_ITEM_TYPE_LABEL] ?? item.itemType}
                            </span>
                            <span style={{ fontSize: "0.82rem", fontWeight: 700, color: T.txt }}>{item.code} · {item.description}</span>
                            <span style={{ fontSize: "0.68rem", color: T.txtMuted }}>
                              orden {item.order}{item.weight !== null ? ` · peso ${item.weight}` : ""}{item.isCritical ? " · crítico" : ""}
                            </span>
                          </div>
                          {isDraft && (
                            <div style={{ display: "flex", gap: 6 }}>
                              {childOptions.length > 0 && (
                                <button type="button" onClick={() => startCreateItem(item.id)} style={{ ...secondaryButton, minHeight: 30, padding: "5px 10px", fontSize: "0.68rem" }}>
                                  + Hijo
                                </button>
                              )}
                              <button type="button" onClick={() => startEditItem(item)} style={{ ...secondaryButton, minHeight: 30, padding: "5px 10px", fontSize: "0.68rem" }}>
                                Editar
                              </button>
                              <button
                                type="button"
                                onClick={() => void handleDelete(item)}
                                disabled={deletingId === item.id}
                                style={{ ...dangerButton, opacity: deletingId === item.id ? 0.6 : 1 }}
                              >
                                {deletingId === item.id ? "Borrando…" : "Borrar"}
                              </button>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {isDraft && form && (
                <form onSubmit={submitForm} style={panelStyle} aria-label={form.mode === "create" ? "Nuevo ítem" : "Editar ítem"}>
                  <h3 style={{ fontSize: "0.9rem", fontWeight: 700, color: T.txt }}>
                    {form.mode === "create" ? "Nuevo ítem" : `Editar ítem: ${form.code}`}
                  </h3>
                  <p style={{ fontSize: "0.72rem", color: T.txtMuted }}>
                    {form.parentId === null
                      ? "Se creará en la raíz de la plantilla."
                      : `Padre: ${items.find(item => item.id === form.parentId)?.code ?? form.parentId}`}
                  </p>

                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                    <label style={fieldLabel}>
                      Código
                      <input value={form.code} onChange={event => setForm({ ...form, code: event.target.value })} style={fieldInput} placeholder="1.1.2" />
                    </label>
                    <label style={fieldLabel}>
                      Tipo de ítem
                      <select value={form.itemType} onChange={event => setForm({ ...form, itemType: event.target.value })} style={fieldInput}>
                        {formAllowedTypes.length === 0 && <option value="">Sin tipos válidos en este nivel</option>}
                        {formAllowedTypes.map(type => <option key={type} value={type}>{TEMPLATE_ITEM_TYPE_LABEL[type]}</option>)}
                      </select>
                    </label>
                  </div>

                  <label style={fieldLabel}>
                    Descripción
                    <textarea value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} rows={3} style={{ ...fieldInput, minHeight: 72, resize: "vertical" }} />
                  </label>

                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                    <label style={fieldLabel}>
                      Orden
                      <input type="number" value={form.order} onChange={event => setForm({ ...form, order: event.target.value })} style={fieldInput} />
                    </label>
                    <label style={fieldLabel}>
                      Peso (opcional, preguntas)
                      <input type="number" step="0.01" value={form.weight} onChange={event => setForm({ ...form, weight: event.target.value })} style={fieldInput} disabled={form.itemType !== "QUESTION"} />
                    </label>
                  </div>

                  <label style={fieldLabel}>
                    Tipo de respuesta (opcional, ej. COMPLIANCE)
                    <input value={form.responseType} onChange={event => setForm({ ...form, responseType: event.target.value })} style={fieldInput} disabled={form.itemType !== "QUESTION"} />
                  </label>

                  <div style={{ display: "flex", gap: 16, flexWrap: "wrap" }}>
                    <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: "0.76rem", color: T.txtMuted }}>
                      <input type="checkbox" checked={form.isRequired} onChange={event => setForm({ ...form, isRequired: event.target.checked })} />
                      Obligatorio
                    </label>
                    <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: "0.76rem", color: T.txtMuted }}>
                      <input type="checkbox" checked={form.isCritical} onChange={event => setForm({ ...form, isCritical: event.target.checked })} />
                      Crítico
                    </label>
                    <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: "0.76rem", color: T.txtMuted }}>
                      <input type="checkbox" checked={form.allowsNotApplicable} onChange={event => setForm({ ...form, allowsNotApplicable: event.target.checked })} />
                      Permite &quot;No aplica&quot;
                    </label>
                  </div>

                  {formError && <p role="alert" style={errorBanner}>{formError}</p>}

                  <div style={{ display: "flex", gap: 10 }}>
                    <button type="submit" disabled={savingItem} style={{ ...primaryButton, opacity: savingItem ? 0.6 : 1 }}>
                      {savingItem ? "Guardando…" : form.mode === "create" ? "Crear ítem" : "Guardar cambios"}
                    </button>
                    <button type="button" onClick={closeForm} disabled={savingItem} style={{ ...secondaryButton, borderColor: T.border, color: T.txtMuted }}>
                      Cancelar
                    </button>
                  </div>

                </form>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

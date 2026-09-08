import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createTemplate, createTemplateItem, listTemplateItems, listTemplates, publishTemplate,
  type EvaluationTemplate, type EvaluationTemplateItem,
} from "./template-api";

export default function TemplateEditor({ accessToken }: { accessToken: string }) {
  const [templates, setTemplates] = useState<EvaluationTemplate[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [items, setItems] = useState<EvaluationTemplateItem[]>([]);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [description, setDescription] = useState("");
  const [itemType, setItemType] = useState<EvaluationTemplateItem["itemType"]>("CHAPTER");
  const [parentId, setParentId] = useState<number | null>(null);
  const [weight, setWeight] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const selected = templates.find(template => template.id === selectedId) ?? null;
  const ordered = useMemo(() => flattenTree(items), [items]);

  const refreshTemplates = useCallback(async () => {
    try {
      const result = await listTemplates(accessToken);
      setTemplates(result);
      setSelectedId(current => current ?? result[0]?.id ?? null);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar las plantillas.");
    }
  }, [accessToken]);

  useEffect(() => { void refreshTemplates(); }, [refreshTemplates]);
  useEffect(() => {
    if (selectedId === null) { setItems([]); return; }
    void listTemplateItems(accessToken, selectedId).then(setItems).catch(loadError =>
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar la estructura."));
  }, [accessToken, selectedId]);

  const addTemplate = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true); setError("");
    try {
      const created = await createTemplate(accessToken, name);
      setTemplates(current => [created, ...current]); setSelectedId(created.id); setName("");
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "No fue posible crear la plantilla."); }
    finally { setSaving(false); }
  };

  const addItem = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selected) return;
    setSaving(true); setError("");
    try {
      const created = await createTemplateItem(accessToken, selected.id, {
        parentId, code, description, itemType, order: siblings(items, parentId) + 1,
        weight: weight ? Number(weight) : null, isRequired: itemType === "QUESTION",
        isCritical: false, allowsNotApplicable: true, responseType: itemType === "QUESTION" ? "COMPLIANCE" : null,
      });
      setItems(current => [...current, created]); setCode(""); setDescription(""); setWeight("");
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "No fue posible crear el ítem."); }
    finally { setSaving(false); }
  };

  const publish = async () => {
    if (!selected) return;
    setSaving(true); setError("");
    try {
      const updated = await publishTemplate(accessToken, selected.id);
      setTemplates(current => current.map(item => item.id === updated.id ? updated : item));
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "No fue posible publicar."); }
    finally { setSaving(false); }
  };

  return <div style={{ display: "grid", gap: 20 }}>
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(min(100%,280px),1fr))", gap: 18 }}>
      <form onSubmit={addTemplate} style={panel}>
        <h2 style={heading}>Nueva plantilla</h2>
        <label style={label}>Nombre<input value={name} onChange={event => setName(event.target.value)} required style={input} /></label>
        <button disabled={saving} style={primary}>Crear borrador</button>
      </form>
      <section style={panel}>
        <h2 style={heading}>Versiones</h2>
        <label style={label}>Plantilla
          <select value={selectedId ?? ""} onChange={event => setSelectedId(Number(event.target.value))} style={input}>
            <option value="" disabled>Selecciona una plantilla</option>
            {templates.map(template => <option key={template.id} value={template.id}>{template.name} · v{template.version} · {template.status}</option>)}
          </select>
        </label>
        {selected?.status === "DRAFT" && <button type="button" disabled={saving || items.length === 0} onClick={() => void publish()} style={secondary}>Publicar y congelar versión</button>}
      </section>
    </div>
    {error && <p role="alert" style={errorStyle}>{error}</p>}
    {selected && <div style={{ display: "grid", gridTemplateColumns: "minmax(280px,.8fr) minmax(320px,1.2fr)", gap: 18 }} className="template-editor-grid">
      <form onSubmit={addItem} style={panel}>
        <h2 style={heading}>Agregar a la estructura</h2>
        {selected.status !== "DRAFT" && <p style={muted}>Esta versión está publicada y solo puede consultarse.</p>}
        <label style={label}>Código<input value={code} onChange={event => setCode(event.target.value)} required disabled={selected.status !== "DRAFT"} placeholder="1.1.2" style={input} /></label>
        <label style={label}>Descripción<textarea value={description} onChange={event => setDescription(event.target.value)} required disabled={selected.status !== "DRAFT"} rows={4} style={input} /></label>
        <label style={label}>Tipo<select value={itemType} onChange={event => setItemType(event.target.value as EvaluationTemplateItem["itemType"])} disabled={selected.status !== "DRAFT"} style={input}>
          <option value="CHAPTER">Capítulo</option><option value="SECTION">Sección</option><option value="SUBSECTION">Subsección</option>
          <option value="QUESTION">Pregunta</option><option value="INSTRUCTION">Instrucción</option><option value="OPTION">Opción</option>
        </select></label>
        <label style={label}>Pertenece a<select value={parentId ?? ""} onChange={event => setParentId(event.target.value ? Number(event.target.value) : null)} disabled={selected.status !== "DRAFT"} style={input}>
          <option value="">Nivel raíz</option>{ordered.map(({ item, depth }) => <option key={item.id} value={item.id}>{"— ".repeat(depth)}{item.code} {item.description}</option>)}
        </select></label>
        {itemType === "QUESTION" && <label style={label}>Peso<input type="number" min="0" step="0.001" value={weight} onChange={event => setWeight(event.target.value)} style={input} /></label>}
        <button disabled={saving || selected.status !== "DRAFT"} style={primary}>Agregar ítem</button>
      </form>
      <section style={panel} aria-live="polite">
        <h2 style={heading}>Estructura editable · {items.length} ítems</h2>
        {ordered.length === 0 ? <p style={muted}>Comienza agregando un capítulo.</p> : ordered.map(({ item, depth }) =>
          <article key={item.id} style={{ ...row, marginLeft: Math.min(depth * 18, 72), borderLeftColor: typeColor(item.itemType) }}>
            <span style={{ color: typeColor(item.itemType), fontSize: 11, fontWeight: 800 }}>{item.itemType}</span>
            <strong>{item.code} · {item.description}</strong>
            {item.weight !== null && <span style={muted}>Peso: {item.weight}</span>}
          </article>)}
      </section>
    </div>}
  </div>;
}

function flattenTree(items: EvaluationTemplateItem[]) {
  const result: Array<{ item: EvaluationTemplateItem; depth: number }> = [];
  const visit = (parentId: number | null, depth: number) => items.filter(item => item.parentId === parentId)
    .sort((a, b) => a.order - b.order || a.code.localeCompare(b.code, undefined, { numeric: true }))
    .forEach(item => { result.push({ item, depth }); visit(item.id, depth + 1); });
  visit(null, 0); return result;
}
function siblings(items: EvaluationTemplateItem[], parentId: number | null) { return items.filter(item => item.parentId === parentId).length; }
function typeColor(type: EvaluationTemplateItem["itemType"]) { return type === "QUESTION" ? "#E53BF6" : type === "CHAPTER" ? "#3BF6E5" : "#94a3b8"; }

const panel = { padding: 20, borderRadius: 18, background: "rgba(15,23,42,.72)", border: "1px solid rgba(148,163,184,.18)", display: "flex", flexDirection: "column", gap: 13 } as const;
const heading = { fontSize: 17 } as const;
const label = { display: "grid", gap: 7, color: "#cbd5e1", fontSize: 14, fontWeight: 600 } as const;
const input = { minHeight: 46, borderRadius: 11, border: "1px solid rgba(148,163,184,.3)", background: "rgba(2,6,23,.7)", color: "#f8fafc", padding: "10px 12px", fontSize: 16 } as const;
const button = { minHeight: 44, borderRadius: 11, padding: "10px 16px", fontWeight: 700, cursor: "pointer" } as const;
const primary = { ...button, border: 0, background: "linear-gradient(135deg,#E53BF6,#3BF6E5)", color: "white" } as const;
const secondary = { ...button, border: "1px solid #3BF6E5", background: "rgba(59,246,229,.1)", color: "#3BF6E5" } as const;
const row = { display: "grid", gap: 4, padding: 12, borderRadius: 10, border: "1px solid rgba(148,163,184,.14)", borderLeft: "4px solid", background: "rgba(255,255,255,.025)" } as const;
const muted = { color: "#94a3b8", fontSize: 13 } as const;
const errorStyle = { background: "rgba(244,63,94,.1)", color: "#fecdd3", border: "1px solid rgba(244,63,94,.35)", padding: 12, borderRadius: 10 } as const;

import { useCallback, useEffect, useState } from "react";
import { createRiskLevel, listRiskLevels, type RiskLevel } from "../catalogs/api";
import { createCompany, listCompanies, type Company } from "./api";
import TemplateEditor from "../evaluations/TemplateEditor";

export default function CompanyManagement({ accessToken, onBack }: { accessToken: string; onBack: () => void }) {
  const [tab, setTab] = useState<"companies" | "risk" | "templates">("companies");
  const [companies, setCompanies] = useState<Company[]>([]);
  const [riskLevels, setRiskLevels] = useState<RiskLevel[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [legalName, setLegalName] = useState("");
  const [tradeName, setTradeName] = useState("");
  const [rnc, setRnc] = useState("");
  const [riskName, setRiskName] = useState("");
  const [riskPoints, setRiskPoints] = useState(1);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [companyItems, levelItems] = await Promise.all([
        listCompanies(accessToken),
        listRiskLevels(accessToken),
      ]);
      setCompanies(companyItems);
      setRiskLevels(levelItems);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar los datos.");
    } finally {
      setLoading(false);
    }
  }, [accessToken]);

  useEffect(() => { void refresh(); }, [refresh]);

  const submitCompany = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError("");
    try {
      const created = await createCompany(accessToken, { legalName, tradeName, rnc });
      setCompanies(current => [...current, created].sort((a, b) => a.legalName.localeCompare(b.legalName)));
      setLegalName(""); setTradeName(""); setRnc("");
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible crear la empresa.");
    } finally {
      setSaving(false);
    }
  };

  const submitRiskLevel = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError("");
    try {
      const created = await createRiskLevel(accessToken, { name: riskName, points: riskPoints });
      setRiskLevels(current => [...current, created].sort((a, b) => a.points - b.points));
      setRiskName(""); setRiskPoints(1);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible crear el nivel.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <main className="mesh-bg" style={{ minHeight: "100svh", color: "#f1f5f9", padding: "clamp(16px,4vw,40px)" }}>
      <section className="glass-card" style={{ maxWidth: 1120, margin: "0 auto", padding: "clamp(18px,3vw,32px)", borderRadius: 24 }}>
        <header style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 16, flexWrap: "wrap" }}>
          <div>
            <p style={eyebrow}>CONFIGURACIÓN MAESTRA</p>
            <h1 style={{ fontSize: "clamp(1.35rem,3vw,2rem)", marginTop: 4 }}>Empresas y catálogos de riesgo</h1>
          </div>
          <button type="button" onClick={onBack} style={secondaryButton}>Volver al panel</button>
        </header>

        <nav aria-label="Secciones de configuración" style={{ display: "flex", gap: 8, margin: "24px 0", flexWrap: "wrap" }}>
          <button type="button" onClick={() => setTab("companies")} style={tabButton(tab === "companies")}>Empresas</button>
          <button type="button" onClick={() => setTab("risk")} style={tabButton(tab === "risk")}>Niveles de riesgo</button>
          <button type="button" onClick={() => setTab("templates")} style={tabButton(tab === "templates")}>Plantillas EBR</button>
        </nav>

        {error && <p role="alert" style={errorBox}>{error}</p>}
        {tab === "templates" ? <TemplateEditor accessToken={accessToken} /> : loading ? <p aria-live="polite" style={{ color: "#cbd5e1" }}>Cargando configuración…</p> : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(min(100%,320px),1fr))", gap: 24 }}>
            {tab === "companies" ? (
              <>
                <form onSubmit={submitCompany} style={panel}>
                  <h2 style={panelHeading}>Registrar empresa</h2>
                  <LabeledInput label="Razón social" value={legalName} onChange={setLegalName} required />
                  <LabeledInput label="Nombre comercial" value={tradeName} onChange={setTradeName} required />
                  <LabeledInput label="RNC" value={rnc} onChange={setRnc} required inputMode="numeric" />
                  <button disabled={saving} style={primaryButton}>{saving ? "Guardando…" : "Crear empresa"}</button>
                </form>
                <DataList title={`${companies.length} empresas activas`} empty="No hay empresas registradas.">
                  {companies.map(company => (
                    <article key={company.id} style={rowCard}>
                      <strong>{company.legalName}</strong>
                      <span style={muted}>{company.tradeName} · RNC {company.rnc}</span>
                    </article>
                  ))}
                </DataList>
              </>
            ) : (
              <>
                <form onSubmit={submitRiskLevel} style={panel}>
                  <h2 style={panelHeading}>Crear nivel de riesgo</h2>
                  <LabeledInput label="Nombre" value={riskName} onChange={setRiskName} required />
                  <label style={labelStyle}>Puntos
                    <input type="number" min={1} step={1} value={riskPoints} onChange={event => setRiskPoints(Number(event.target.value))} style={inputStyle} required />
                  </label>
                  <button disabled={saving} style={primaryButton}>{saving ? "Guardando…" : "Crear nivel"}</button>
                </form>
                <DataList title={`${riskLevels.length} niveles configurados`} empty="No hay niveles configurados.">
                  {riskLevels.map(level => (
                    <article key={level.id} style={rowCard}>
                      <strong>{level.name}</strong>
                      <span style={{ color: "#3BF6E5" }}>{level.points} puntos</span>
                    </article>
                  ))}
                </DataList>
              </>
            )}
          </div>
        )}
      </section>
    </main>
  );
}

function LabeledInput({ label, value, onChange, ...props }: { label: string; value: string; onChange: (value: string) => void } & Omit<React.InputHTMLAttributes<HTMLInputElement>, "onChange" | "value">) {
  return <label style={labelStyle}>{label}<input {...props} value={value} onChange={event => onChange(event.target.value)} style={inputStyle} /></label>;
}

function DataList({ title, empty, children }: { title: string; empty: string; children: React.ReactNode }) {
  const hasChildren = Array.isArray(children) ? children.length > 0 : Boolean(children);
  return <section style={panel}><h2 style={panelHeading}>{title}</h2>{hasChildren ? <div style={{ display: "grid", gap: 10 }}>{children}</div> : <p style={muted}>{empty}</p>}</section>;
}

const eyebrow = { color: "#3BF6E5", fontSize: 12, fontWeight: 700, letterSpacing: ".12em" } as const;
const panel = { padding: 20, borderRadius: 18, background: "rgba(15,23,42,.72)", border: "1px solid rgba(148,163,184,.18)", display: "flex", flexDirection: "column", gap: 14 } as const;
const panelHeading = { fontSize: 17, marginBottom: 2 } as const;
const labelStyle = { display: "grid", gap: 7, color: "#cbd5e1", fontSize: 14, fontWeight: 600 } as const;
const inputStyle = { minHeight: 46, borderRadius: 11, border: "1px solid rgba(148,163,184,.3)", background: "rgba(2,6,23,.7)", color: "#f8fafc", padding: "10px 12px", fontSize: 16, outlineColor: "#3BF6E5" } as const;
const baseButton = { minHeight: 44, borderRadius: 11, padding: "10px 16px", fontWeight: 700, cursor: "pointer" } as const;
const primaryButton = { ...baseButton, border: 0, background: "linear-gradient(135deg,#E53BF6,#3BF6E5)", color: "#fff" } as const;
const secondaryButton = { ...baseButton, border: "1px solid rgba(148,163,184,.3)", background: "rgba(15,23,42,.75)", color: "#e2e8f0" } as const;
const tabButton = (active: boolean) => ({ ...baseButton, border: `1px solid ${active ? "#3BF6E5" : "rgba(148,163,184,.25)"}`, background: active ? "rgba(59,246,229,.12)" : "transparent", color: active ? "#3BF6E5" : "#cbd5e1" });
const errorBox = { background: "rgba(244,63,94,.1)", color: "#fecdd3", border: "1px solid rgba(244,63,94,.35)", padding: 12, borderRadius: 10, marginBottom: 16 } as const;
const rowCard = { display: "flex", flexDirection: "column", gap: 5, padding: 14, borderRadius: 12, border: "1px solid rgba(148,163,184,.14)", background: "rgba(255,255,255,.025)" } as const;
const muted = { color: "#94a3b8", fontSize: 13 } as const;

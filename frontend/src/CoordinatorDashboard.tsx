import { useState, useEffect } from "react";
import { listAlerts, type HealthAlert } from "./features/alerts/api";
import { listComplaints, type Complaint } from "./features/complaints/api";
import { listCompanies, type Company } from "./features/companies/api";
import ReportViewer from "./features/reports/ReportViewer";
import type { SignedReport } from "./features/reports/report-pdf";

/* ── Backend → UI mappers (Alertas / Denuncias) ───────────────────────
   El backend guarda el resultado de la decisión y el estado de trámite
   en un solo campo `status` ("PENDING" | "PROCEED" | "NOT_PROCEED" |
   "REFERRED"). La UI separa eso en `resultado` (chip de decisión) y
   `estado` (Activa mientras no se ha decidido, Cerrada una vez decidida). */
function formatFecha(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleDateString("es-DO", { day: "2-digit", month: "short", year: "numeric" });
}
function mapResultado(status: string): AlertResult {
  if (status === "PROCEED") return "Procede";
  if (status === "NOT_PROCEED") return "No Procede";
  return "En Revisión"; // PENDING, REFERRED, o cualquier otro estado intermedio
}
function toAlertaItem(alert: HealthAlert, companies: Company[]): AlertaItem {
  return {
    id: alert.id,
    numero: alert.alertNumber,
    fecha: formatFecha(alert.receivedAt),
    producto: alert.product,
    empresa: companies.find(c => c.id === alert.companyId)?.tradeName ?? `Empresa #${alert.companyId}`,
    desc: alert.description,
    resultado: mapResultado(alert.status),
    estado: alert.status === "PENDING" ? "Activa" : "Cerrada",
  };
}
function toDenunciaItem(complaint: Complaint): DenunciaItem {
  return {
    id: complaint.id,
    codigo: `DEN-${complaint.id}`,
    tipo: complaint.complaintType,
    fechaRec: formatFecha(complaint.receivedAt),
    desc: complaint.description,
    resultado: mapResultado(complaint.status),
    estado: complaint.status === "PENDING" ? "Activa" : "Cerrada",
  };
}

function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

/* ── Types ──────────────────────────────────────────────────────────── */
type Section    = "inicio" | "evaluaciones" | "calendario" | "alertas" | "denuncias" | "asignaciones" | "asignar-caso" | "confirmacion" | "reportes" | "configuracion";
type Priority   = "Alta" | "Media" | "Baja";
type EvalStatus = "En Curso" | "Programada" | "Completada" | "Pendiente" | "En Revisión";
type AlertResult= "Procede" | "En Revisión" | "No Procede";
type RiskLevel  = "Bajo" | "Moderado" | "Alto" | "Crítico";
type TechStatus = "Disponible" | "En Campo" | "Descanso";

interface Caso {
  id: string; empresa: string; tipo: string; zona: string;
  fecha: string; priority: Priority; asignado: string | null;
  solicitudId?: string;
}
interface Tecnico {
  id: string; name: string; initials: string; status: TechStatus;
  specialty: string; cases: number; maxCases: number; zona: string;
}

/* ── Color maps ─────────────────────────────────────────────────────── */
const PRI: Record<Priority, { color:string; bg:string; border:string }> = {
  Alta:  { color:"#E53BF6", bg:"rgba(229,59,246,0.1)",  border:"rgba(229,59,246,0.3)"  },
  Media: { color:"#F6E53B", bg:"rgba(246,229,59,0.1)",  border:"rgba(246,229,59,0.3)"  },
  Baja:  { color:"#3BF6E5", bg:"rgba(59,246,229,0.1)",  border:"rgba(59,246,229,0.3)"  },
};
const EVAL_STATUS: Record<EvalStatus, { color:string; bg:string; border:string }> = {
  "En Curso":   { color:"#F6E53B", bg:"rgba(246,229,59,0.1)",  border:"rgba(246,229,59,0.35)"  },
  "Programada": { color:"#3BF6E5", bg:"rgba(59,246,229,0.1)",  border:"rgba(59,246,229,0.35)"  },
  "Completada": { color:"#22c55e", bg:"rgba(34,197,94,0.1)",   border:"rgba(34,197,94,0.35)"   },
  "Pendiente":  { color:"rgba(148,163,184,0.6)", bg:"rgba(148,163,184,0.06)", border:"rgba(148,163,184,0.2)" },
  "En Revisión":{ color:"#8b5cf6", bg:"rgba(139,92,246,0.1)", border:"rgba(139,92,246,0.35)" },
};
const RISK: Record<RiskLevel, { color:string; bg:string }> = {
  "Bajo":     { color:"#22c55e", bg:"rgba(34,197,94,0.12)"   },
  "Moderado": { color:"#f59e0b", bg:"rgba(245,158,11,0.12)"  },
  "Alto":     { color:"#E53BF6", bg:"rgba(229,59,246,0.12)"  },
  "Crítico":  { color:"#ef4444", bg:"rgba(239,68,68,0.12)"   },
};
const ALERT_RESULT: Record<AlertResult, { color:string; bg:string; border:string }> = {
  "Procede":     { color:"#ef4444", bg:"rgba(239,68,68,0.1)",    border:"rgba(239,68,68,0.3)"    },
  "En Revisión": { color:"#F6E53B", bg:"rgba(246,229,59,0.1)",   border:"rgba(246,229,59,0.3)"   },
  "No Procede":  { color:"#22c55e", bg:"rgba(34,197,94,0.1)",    border:"rgba(34,197,94,0.3)"    },
};

/* ── Sample data ────────────────────────────────────────────────────── */
const CASOS_INIT: Caso[] = [
  { id:"ASG-001", empresa:"Constructora García & Asoc.",  tipo:"Inspección General",      zona:"Santo Domingo",  fecha:"Hoy",    priority:"Alta",  asignado:null,        solicitudId:"SOL-2026-0039" },
  { id:"ASG-002", empresa:"Farmacia San Martín",          tipo:"Registro Sanitario",      zona:"Santiago",       fecha:"Hoy",    priority:"Alta",  asignado:null         },
  { id:"ASG-003", empresa:"Industrias Montoya S.A.",      tipo:"Auditoría LAPCH",         zona:"La Vega",        fecha:"Mañana", priority:"Alta",  asignado:null         },
  { id:"ASG-004", empresa:"Distribuidora Norte",          tipo:"Evaluación Eléctrica",    zona:"Puerto Plata",   fecha:"03 sep", priority:"Media", asignado:null         },
  { id:"ASG-005", empresa:"Ferrería del Sur",             tipo:"Inspección de Seguridad", zona:"San Cristóbal",  fecha:"03 sep", priority:"Alta",  asignado:null         },
  { id:"ASG-006", empresa:"Comercial Ángel Ltda.",        tipo:"Inspección General",      zona:"Santo Domingo",  fecha:"04 sep", priority:"Media", asignado:null         },
  { id:"ASG-007", empresa:"Torres Hermanos S.A.",         tipo:"Revisión Normativa ISO",  zona:"Santiago",       fecha:"04 sep", priority:"Baja",  asignado:"Ing. M. Santos" },
  { id:"ASG-008", empresa:"Agro del Cibao C. por A.",    tipo:"Inspección Ambiental",    zona:"Moca",           fecha:"05 sep", priority:"Media", asignado:null         },
];
const TECHNICIANS: Tecnico[] = [
  { id:"T1", name:"Ing. M. Santos",    initials:"MS", status:"Disponible", specialty:"Inspección General",  cases:4, maxCases:8, zona:"Distrito Nacional / Santiago" },
  { id:"T2", name:"Lic. A. Fernández", initials:"AF", status:"Disponible", specialty:"Especialista LAPCH",  cases:2, maxCases:8, zona:"Santiago / La Vega" },
  { id:"T3", name:"Ing. R. Méndez",   initials:"RM", status:"En Campo",   specialty:"Técnico Evaluador",   cases:6, maxCases:8, zona:"Santo Domingo" },
  { id:"T4", name:"Lic. C. Vargas",   initials:"CV", status:"Disponible", specialty:"Inspector Senior",    cases:3, maxCases:8, zona:"Sur / San Cristóbal" },
  { id:"T5", name:"Ing. P. Castillo", initials:"PC", status:"En Campo",   specialty:"Técnico Evaluador",   cases:5, maxCases:8, zona:"Norte / Puerto Plata" },
  { id:"T6", name:"Dra. L. Reyes",    initials:"LR", status:"Descanso",   specialty:"Calidad y Normas",    cases:0, maxCases:8, zona:"Distrito Nacional" },
];
const EVALUACIONES_DATA: { id:string; empresa:string; tipo:string; tecnico:string|null; estado:EvalStatus; riesgo:RiskLevel|null; fecha:string; priority:Priority }[] = [
  { id:"EBR-2026-0089", empresa:"Farmacéutica del Sur S.A.",    tipo:"Inspección BPM",      tecnico:"Ing. M. Santos",    estado:"En Curso",   riesgo:"Moderado", fecha:"Hoy 09:00",      priority:"Alta"  },
  { id:"EBR-2026-0088", empresa:"Constructora García & Asoc.",  tipo:"Evaluación General",  tecnico:"Lic. A. Fernández", estado:"Programada", riesgo:null,       fecha:"Mañana 10:30",   priority:"Media" },
  { id:"EBR-2026-0087", empresa:"Alimentos del Caribe SRL",     tipo:"Auditoría Calidad",   tecnico:"Ing. R. Méndez",   estado:"Completada", riesgo:"Alto",     fecha:"28 ago 2026",    priority:"Alta"  },
  { id:"EBR-2026-0085", empresa:"Cosmética Bella Dominicana",   tipo:"Inspección BPM",      tecnico:"Lic. C. Vargas",   estado:"Completada", riesgo:"Bajo",     fecha:"25 ago 2026",    priority:"Baja"  },
  { id:"EBR-2026-0083", empresa:"Laboratorio Santos Cruz",      tipo:"Evaluación General",  tecnico:"Ing. M. Santos",    estado:"En Revisión",riesgo:"Crítico",  fecha:"22 ago 2026",    priority:"Alta"  },
  { id:"EBR-2026-0081", empresa:"Distribuidora Norte S.A.",     tipo:"Inspección Eléctrica",tecnico:null,                estado:"Pendiente",  riesgo:null,       fecha:"Pendiente asign.",priority:"Media" },
];
/* Alertas y Denuncias ya no son datos simulados: se cargan desde
   /api/alerts y /api/complaints (ver features/alerts, features/complaints)
   y se transforman a esta forma con toAlertaItem/toDenunciaItem. */
interface AlertaItem { id:number; numero:string; fecha:string; producto:string; empresa:string; desc:string; resultado:AlertResult; estado:"Activa"|"Cerrada" }
interface DenunciaItem { id:number; codigo:string; tipo:string; fechaRec:string; desc:string; resultado:AlertResult; estado:"Activa"|"Cerrada" }
const REPORTES_DATA = [
  { id:"INF-2026-083", empresa:"Laboratorio Santos Cruz",    tipo:"Evaluación General",  tecnico:"Ing. M. Santos",    estado:"En Revisión", fecha:"22 ago 2026", riesgo:"Crítico"  as RiskLevel },
  { id:"INF-2026-087", empresa:"Alimentos del Caribe SRL",  tipo:"Auditoría Calidad",   tecnico:"Ing. R. Méndez",   estado:"Aprobado",    fecha:"28 ago 2026", riesgo:"Alto"     as RiskLevel },
  { id:"INF-2026-085", empresa:"Cosmética Bella Dominicana",tipo:"Inspección BPM",      tecnico:"Lic. C. Vargas",   estado:"Aprobado",    fecha:"25 ago 2026", riesgo:"Bajo"     as RiskLevel },
  { id:"INF-2026-079", empresa:"Distribuidora Norte S.A.",  tipo:"Inspección General",  tecnico:"Lic. A. Fernández", estado:"Devuelto",    fecha:"18 ago 2026", riesgo:"Moderado" as RiskLevel },
];
const CAL_EVENTS: Record<number, { label:string; color:string }[]> = {
  1:  [{ label:"EBR-0089", color:"#F6E53B" }],
  2:  [{ label:"EBR-0088", color:"#3BF6E5" }],
  4:  [{ label:"ALP-042",  color:"#E53BF6" }],
  8:  [{ label:"EBR-0091", color:"#3BF6E5" }, { label:"EBR-0092", color:"#22c55e" }],
  10: [{ label:"DEN-015",  color:"#ef4444" }],
  15: [{ label:"EBR-0093", color:"#3BF6E5" }],
  17: [{ label:"EBR-0094", color:"#F6E53B" }],
  22: [{ label:"EBR-0095", color:"#3BF6E5" }, { label:"EBR-0096", color:"#3BF6E5" }],
  25: [{ label:"ALP-043",  color:"#E53BF6" }],
  29: [{ label:"EBR-0097", color:"#22c55e" }],
};

/* ── Nav ────────────────────────────────────────────────────────────── */
const NAV: { id:Section; label:string; badge?:number; icon:(c:string)=>React.ReactNode }[] = [
  { id:"inicio",       label:"Inicio",         icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" stroke={c} strokeWidth="1.75"/><polyline points="9,22 9,12 15,12 15,22" stroke={c} strokeWidth="1.75"/></svg> },
  { id:"evaluaciones", label:"Evaluaciones",   badge:3, icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M9 11l3 3L22 4" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg> },
  { id:"calendario",   label:"Calendario",     icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><rect x="3" y="4" width="18" height="18" rx="2" stroke={c} strokeWidth="1.75"/><path d="M16 2v4M8 2v4M3 10h18" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg> },
  { id:"alertas",      label:"Alertas LAPCH",  badge:2, icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/><line x1="12" y1="9" x2="12" y2="13" stroke={c} strokeWidth="1.75" strokeLinecap="round"/><line x1="12" y1="17" x2="12.01" y2="17" stroke={c} strokeWidth="2.5" strokeLinecap="round"/></svg> },
  { id:"denuncias",    label:"Denuncias",      badge:1, icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="10" stroke={c} strokeWidth="1.75"/><path d="M12 8v4M12 16h.01" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg> },
  { id:"asignaciones", label:"Asignaciones",   badge:5, icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2" stroke={c} strokeWidth="1.75"/><circle cx="9" cy="7" r="4" stroke={c} strokeWidth="1.75"/><path d="M23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg> },
  { id:"reportes",     label:"Reportes",       icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" stroke={c} strokeWidth="1.75"/><polyline points="14,2 14,8 20,8" stroke={c} strokeWidth="1.75"/><line x1="16" y1="13" x2="8" y2="13" stroke={c} strokeWidth="1.75" strokeLinecap="round"/><line x1="16" y1="17" x2="8" y2="17" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg> },
  { id:"configuracion",label:"Configuración",  icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="3" stroke={c} strokeWidth="1.75"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" stroke={c} strokeWidth="1.75"/></svg> },
];
const NAV_VISIBLE_IDS: Section[] = ["inicio","evaluaciones","calendario","alertas","denuncias","asignaciones","reportes","configuracion"];

/* ── Small reusable components ──────────────────────────────────────── */
function Chip({ label, color, bg, border }: { label:string; color:string; bg:string; border:string }) {
  return (
    <span style={{ display:"inline-flex", alignItems:"center", gap:5, padding:"2px 9px", borderRadius:99, background:bg, borderTop:`1px solid ${border}`, borderRight:`1px solid ${border}`, borderBottom:`1px solid ${border}`, borderLeft:`1px solid ${border}`, fontSize:"0.58rem", fontWeight:700, color, fontFamily:"Poppins, sans-serif", whiteSpace:"nowrap" }}>
      <span style={{ width:5, height:5, borderRadius:"50%", background:color, flexShrink:0 }} />
      {label}
    </span>
  );
}

function SectionHeader({ title, subtitle, action }: { title:string; subtitle?:string; action?:React.ReactNode }) {
  return (
    <div style={{ display:"flex", alignItems:"flex-end", justifyContent:"space-between", marginBottom:20, gap:12 }}>
      <div>
        <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>EVALIA · COORDINADOR</p>
        <h2 style={{ fontSize:"1.1rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em" }}>{title}</h2>
        {subtitle && <p style={{ fontSize:"0.72rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif", marginTop:2 }}>{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}

function GlassCard({ children, accent, style }: { children:React.ReactNode; accent?:string; style?:React.CSSProperties }) {
  return (
    <div style={{ borderRadius:16, background:"rgba(10,18,36,0.82)", backdropFilter:"blur(24px) saturate(160%)", WebkitBackdropFilter:"blur(24px) saturate(160%)", borderTop:`1px solid ${accent ? accent+"22" : "rgba(255,255,255,0.07)"}`, borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:`2px solid ${accent ? accent+"55" : "rgba(255,255,255,0.07)"}`, overflow:"hidden", ...style }}>
      {children}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Inicio
───────────────────────────────────────────────────────────────────── */
function InicioSection({ onNavigate, casos }: { onNavigate:(s:Section)=>void; casos:Caso[] }) {
  const pendientes = casos.filter(c=>!c.asignado).length;
  const metricCards = [
    { label:"Casos pendientes",        value:pendientes, delta:`${pendientes} sin asignar`, color:"#3BF6E5", section:"asignaciones" as Section },
    { label:"Evaluaciones programadas",value:8,          delta:"3 en curso",                color:"#22c55e", section:"evaluaciones"  as Section },
    { label:"Alertas LAPCH",           value:2,          delta:"Urgentes",                  color:"#E53BF6", section:"alertas"       as Section },
    { label:"Denuncias",               value:5,          delta:"+1 nueva",                  color:"#F6E53B", section:"denuncias"     as Section },
  ];

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:20 }}>
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(190px, 1fr))", gap:12 }}>
        {metricCards.map(m => (
          <button key={m.label} onClick={()=>onNavigate(m.section)} style={{ textAlign:"left", cursor:"pointer", border:"none", background:"transparent", padding:0 }}>
            <GlassCard accent={m.color}>
              <div style={{ padding:"16px 18px" }}>
                <div style={{ display:"flex", justifyContent:"space-between", marginBottom:12 }}>
                  <div style={{ width:38, height:38, borderRadius:12, background:`${m.color}18`, borderTop:`1px solid ${m.color}35`, borderRight:`1px solid ${m.color}35`, borderBottom:`1px solid ${m.color}35`, borderLeft:`1px solid ${m.color}35`, display:"flex", alignItems:"center", justifyContent:"center" }}>
                    <span style={{ width:14, height:14, borderRadius:"50%", background:m.color, opacity:0.7 }}/>
                  </div>
                  <span style={{ fontSize:"0.58rem", color:m.color, fontFamily:"Poppins, sans-serif", fontWeight:600, background:`${m.color}14`, padding:"2px 8px", borderRadius:99, borderTop:`1px solid ${m.color}30`, borderRight:`1px solid ${m.color}30`, borderBottom:`1px solid ${m.color}30`, borderLeft:`1px solid ${m.color}30` }}>{m.delta}</span>
                </div>
                <p style={{ fontSize:"2rem", fontWeight:900, color:m.color, fontFamily:"Poppins, sans-serif", lineHeight:1, textShadow:`0 0 20px ${m.color}50` }}>{m.value}</p>
                <p style={{ fontSize:"0.68rem", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", marginTop:4 }}>{m.label}</p>
              </div>
            </GlassCard>
          </button>
        ))}
      </div>

      <GlassCard accent="#3BF6E5">
        <div style={{ padding:"16px 20px" }}>
          <div style={{ display:"flex", justifyContent:"space-between", marginBottom:12 }}>
            <span style={{ fontSize:"0.68rem", fontWeight:700, color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", textTransform:"uppercase", letterSpacing:"0.08em" }}>Avance semanal · Evaluaciones</span>
            <span style={{ fontSize:"0.68rem", color:"#3BF6E5", fontFamily:"Poppins, sans-serif", fontWeight:700 }}>5 / 8</span>
          </div>
          <div style={{ height:6, borderRadius:99, background:"rgba(255,255,255,0.06)", overflow:"hidden" }}>
            <div style={{ height:"100%", width:"62.5%", background:"linear-gradient(90deg, #3BF6E5, #06b6d4)", borderRadius:99, boxShadow:"0 0 12px rgba(59,246,229,0.5)" }} />
          </div>
          <div style={{ display:"flex", justifyContent:"space-between", marginTop:8 }}>
            <span style={{ fontSize:"0.56rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif" }}>Sem. 01 – 07 sep 2026</span>
            <span style={{ fontSize:"0.56rem", color:"#3BF6E5", fontFamily:"Poppins, sans-serif", fontWeight:700 }}>62.5% completado</span>
          </div>
        </div>
      </GlassCard>

      {/* Casos urgentes sin asignar */}
      {pendientes > 0 && (
        <div>
          <div style={{ display:"flex", alignItems:"center", justifyContent:"space-between", marginBottom:10 }}>
            <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif" }}>CASOS URGENTES SIN ASIGNAR</p>
            <button onClick={()=>onNavigate("asignaciones")} style={{ fontSize:"0.65rem", fontWeight:600, color:"rgba(229,59,246,0.6)", fontFamily:"Poppins, sans-serif", background:"none", border:"none", cursor:"pointer" }}>Ver todos →</button>
          </div>
          <div style={{ display:"flex", flexDirection:"column", gap:8 }}>
            {casos.filter(c=>!c.asignado && c.priority==="Alta").slice(0,3).map(caso=>{
              const p=PRI[caso.priority];
              return (
                <div key={caso.id} style={{ display:"flex", alignItems:"center", gap:12, padding:"12px 14px", borderRadius:13, background:"rgba(10,18,36,0.82)", backdropFilter:"blur(16px)", WebkitBackdropFilter:"blur(16px)", borderTop:`1px solid rgba(229,59,246,0.15)`, borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:`2px solid ${p.color}80`, cursor:"pointer" }} onClick={()=>onNavigate("asignaciones")}>
                  <div style={{ flex:1, minWidth:0 }}>
                    <p style={{ fontSize:"0.82rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }}>{caso.empresa}</p>
                    <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{caso.tipo} · {caso.zona} · {caso.fecha}</p>
                  </div>
                  <Chip label={caso.priority} color={p.color} bg={p.bg} border={p.border}/>
                  <span style={{ fontSize:"0.65rem", fontWeight:700, color:"rgba(229,59,246,0.7)", fontFamily:"Poppins, sans-serif", flexShrink:0 }}>Asignar →</span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div style={{ display:"flex", flexDirection:"column", gap:6 }}>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(148,163,184,0.3)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif" }}>ACTIVIDAD DE HOY</p>
        {[
          { time:"09:00", label:"EBR-0089 iniciada – Farmacéutica del Sur", color:"#F6E53B" },
          { time:"10:30", label:"Alerta ALP-2026-042 recibida – Procede evaluación", color:"#E53BF6" },
          { time:"11:15", label:"Informe INF-083 en revisión – Laboratorio Santos Cruz", color:"#8b5cf6" },
          { time:"14:00", label:`${pendientes} casos sin asignar – Ver asignaciones`, color:"rgba(148,163,184,0.4)" },
        ].map(ev=>(
          <div key={ev.time} style={{ display:"flex", alignItems:"center", gap:12 }}>
            <span style={{ fontSize:"0.6rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.3)", flexShrink:0, width:38 }}>{ev.time}</span>
            <div style={{ width:6, height:6, borderRadius:"50%", background:ev.color, flexShrink:0, boxShadow:`0 0 6px ${ev.color}` }}/>
            <span style={{ fontSize:"0.72rem", color:"rgba(148,163,184,0.6)", fontFamily:"Poppins, sans-serif" }}>{ev.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Evaluaciones
───────────────────────────────────────────────────────────────────── */
function EvaluacionesSection() {
  const [filter, setFilter] = useState<EvalStatus|"Todas">("Todas");
  const filtered = filter==="Todas" ? EVALUACIONES_DATA : EVALUACIONES_DATA.filter(e=>e.estado===filter);
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <SectionHeader title="Evaluaciones" subtitle={`${EVALUACIONES_DATA.length} evaluaciones registradas`}/>
      <div className="hide-scroll" style={{ display:"flex", gap:7, overflowX:"auto", flexShrink:0 }}>
        {(["Todas","En Curso","Programada","Completada","En Revisión","Pendiente"] as const).map(f=>{
          const active=filter===f; const c=f==="Todas"?"rgba(148,163,184,0.7)":EVAL_STATUS[f as EvalStatus].color;
          return <button key={f} onClick={()=>setFilter(f)} style={{ flexShrink:0, padding:"5px 14px", borderRadius:99, fontSize:"0.65rem", fontWeight:600, fontFamily:"Poppins, sans-serif", cursor:"pointer", background:active?`${c}18`:"rgba(255,255,255,0.04)", borderTop:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderRight:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderBottom:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderLeft:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", color:active?c:"rgba(148,163,184,0.45)" }}>{f}</button>;
        })}
      </div>
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(290px, 1fr))", gap:12 }}>
        {filtered.map(ev=>{
          const st=EVAL_STATUS[ev.estado];
          return (
            <GlassCard key={ev.id} accent={st.color}>
              <div style={{ padding:"15px 16px", display:"flex", flexDirection:"column", gap:10 }}>
                <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start" }}>
                  <div>
                    <span style={{ fontSize:"0.52rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.3)" }}>{ev.id}</span>
                    <p style={{ fontSize:"0.88rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap", maxWidth:180 }}>{ev.empresa}</p>
                    <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{ev.tipo}</p>
                  </div>
                  <Chip label={ev.estado} color={st.color} bg={st.bg} border={st.border}/>
                </div>
                <div style={{ display:"flex", gap:8, flexWrap:"wrap", alignItems:"center" }}>
                  {ev.riesgo && <Chip label={ev.riesgo} color={RISK[ev.riesgo].color} bg={RISK[ev.riesgo].bg} border={RISK[ev.riesgo].color+"40"}/>}
                  <Chip label={ev.priority} color={PRI[ev.priority].color} bg={PRI[ev.priority].bg} border={PRI[ev.priority].border}/>
                </div>
                <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center" }}>
                  <div>
                    <p style={{ fontSize:"0.6rem", color:"rgba(148,163,184,0.35)", fontFamily:"Poppins, sans-serif" }}>{ev.tecnico ?? "Sin asignar"}</p>
                    <p style={{ fontSize:"0.6rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif" }}>{ev.fecha}</p>
                  </div>
                  <button style={{ padding:"6px 12px", borderRadius:9, fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif", cursor:"pointer", background:`${st.color}10`, borderTop:`1px solid ${st.color}40`, borderRight:`1px solid ${st.color}40`, borderBottom:`1px solid ${st.color}40`, borderLeft:`1px solid ${st.color}40`, color:st.color }}>Ver detalle</button>
                </div>
              </div>
            </GlassCard>
          );
        })}
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Calendario
───────────────────────────────────────────────────────────────────── */
function CalendarioSection() {
  const days=["L","M","X","J","V","S","D"]; const cells:(number|null)[]=[null,...Array.from({length:30},(_,i)=>i+1)];
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:20 }}>
      <SectionHeader title="Calendario" subtitle="Septiembre 2026"/>
      <GlassCard accent="#3BF6E5">
        <div style={{ padding:"20px" }}>
          <div style={{ display:"flex", alignItems:"center", justifyContent:"space-between", marginBottom:16 }}>
            <button style={{ width:32, height:32, borderRadius:9, background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"rgba(148,163,184,0.5)", cursor:"pointer", display:"flex", alignItems:"center", justifyContent:"center" }}>‹</button>
            <h3 style={{ fontSize:"0.95rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>Septiembre 2026</h3>
            <button style={{ width:32, height:32, borderRadius:9, background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"rgba(148,163,184,0.5)", cursor:"pointer", display:"flex", alignItems:"center", justifyContent:"center" }}>›</button>
          </div>
          <div style={{ display:"grid", gridTemplateColumns:"repeat(7, 1fr)", gap:4, marginBottom:8 }}>
            {days.map(d=><div key={d} style={{ textAlign:"center", fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif", textTransform:"uppercase", padding:"4px 0" }}>{d}</div>)}
          </div>
          <div style={{ display:"grid", gridTemplateColumns:"repeat(7, 1fr)", gap:4 }}>
            {cells.map((day,i)=>{
              const events=day?CAL_EVENTS[day]:null; const isToday=day===1;
              return (
                <div key={i} style={{ aspectRatio:"1", borderRadius:10, display:"flex", flexDirection:"column", alignItems:"center", justifyContent:"center", gap:3, cursor:day?"pointer":"default", background:isToday?"rgba(59,246,229,0.15)":day?"rgba(255,255,255,0.03)":"transparent", borderTop:isToday?"1px solid rgba(59,246,229,0.5)":"1px solid transparent", borderRight:isToday?"1px solid rgba(59,246,229,0.5)":"1px solid transparent", borderBottom:isToday?"1px solid rgba(59,246,229,0.5)":"1px solid transparent", borderLeft:isToday?"1px solid rgba(59,246,229,0.5)":"1px solid transparent" }}>
                  {day && <>
                    <span style={{ fontSize:"0.7rem", fontWeight:isToday?800:400, color:isToday?"#3BF6E5":"rgba(148,163,184,0.65)", fontFamily:"Poppins, sans-serif" }}>{day}</span>
                    {events && <div style={{ display:"flex", gap:2, flexWrap:"wrap", justifyContent:"center" }}>{events.slice(0,3).map((ev,ei)=><span key={ei} style={{ width:5, height:5, borderRadius:"50%", background:ev.color, boxShadow:`0 0 4px ${ev.color}` }}/>)}</div>}
                  </>}
                </div>
              );
            })}
          </div>
        </div>
      </GlassCard>
      <GlassCard>
        <div style={{ padding:"16px 20px" }}>
          <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(148,163,184,0.3)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:12 }}>EVENTOS DEL MES</p>
          <div style={{ display:"flex", flexDirection:"column", gap:8 }}>
            {[{day:"01 sep",label:"EBR-0089 – Farmacéutica del Sur",color:"#F6E53B"},{day:"02 sep",label:"EBR-0088 – Constructora García",color:"#3BF6E5"},{day:"04 sep",label:"Alerta ALP-042 – Proc. evaluación",color:"#E53BF6"},{day:"08 sep",label:"EBR-0091 + EBR-0092 programadas",color:"#3BF6E5"},{day:"10 sep",label:"Denuncia DEN-015 – Seguimiento",color:"#ef4444"},{day:"15 sep",label:"EBR-0093 – Alimentos del Caribe",color:"#3BF6E5"},{day:"22 sep",label:"EBR-0095 + EBR-0096 programadas",color:"#3BF6E5"},{day:"25 sep",label:"Alerta ALP-043 – Nueva alerta",color:"#E53BF6"}].map(ev=>(
              <div key={ev.day} style={{ display:"flex", alignItems:"center", gap:10 }}>
                <span style={{ fontSize:"0.58rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.3)", flexShrink:0, width:42 }}>{ev.day}</span>
                <span style={{ width:7, height:7, borderRadius:"50%", background:ev.color, flexShrink:0, boxShadow:`0 0 6px ${ev.color}` }}/>
                <span style={{ fontSize:"0.7rem", color:"rgba(148,163,184,0.55)", fontFamily:"Poppins, sans-serif" }}>{ev.label}</span>
              </div>
            ))}
          </div>
        </div>
      </GlassCard>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Alertas
───────────────────────────────────────────────────────────────────── */
function AlertasSection({ alertas, onToast }:{ alertas:AlertaItem[]; onToast:(m:string)=>void }) {
  const [showForm, setShowForm] = useState(false);
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <SectionHeader title="Alertas LAPCH" subtitle={`${alertas.filter(a=>a.estado==="Activa").length} alertas activas`}
        action={<button onClick={()=>setShowForm(s=>!s)} style={{ padding:"8px 16px", borderRadius:11, fontSize:"0.72rem", fontWeight:700, fontFamily:"Poppins, sans-serif", cursor:"pointer", background:"rgba(229,59,246,0.12)", borderTop:"1px solid rgba(229,59,246,0.4)", borderRight:"1px solid rgba(229,59,246,0.4)", borderBottom:"1px solid rgba(229,59,246,0.4)", borderLeft:"1px solid rgba(229,59,246,0.4)", color:"#E53BF6" }}>+ Registrar alerta</button>}/>
      {showForm && <GlassCard accent="#E53BF6"><div style={{ padding:"18px 20px", display:"flex", flexDirection:"column", gap:12 }}>
        <p style={{ fontSize:"0.65rem", fontWeight:700, color:"#E53BF6", fontFamily:"Poppins, sans-serif", textTransform:"uppercase", letterSpacing:"0.1em" }}>Nueva Alerta LAPCH</p>
        {["Número de alerta","Producto afectado","Empresa","Descripción"].map(f=><div key={f} style={{ borderRadius:11, padding:"10px 12px", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)" }}><p style={{ fontSize:"0.55rem", color:"rgba(148,163,184,0.35)", fontFamily:"Poppins, sans-serif" }}>{f}</p></div>)}
        <div style={{ display:"flex", gap:8 }}>
          <button onClick={()=>{ onToast("Alerta registrada correctamente"); setShowForm(false); }} style={{ flex:1, padding:"9px 0", borderRadius:10, cursor:"pointer", background:"rgba(229,59,246,0.12)", borderTop:"1px solid rgba(229,59,246,0.4)", borderRight:"1px solid rgba(229,59,246,0.4)", borderBottom:"1px solid rgba(229,59,246,0.4)", borderLeft:"1px solid rgba(229,59,246,0.4)", color:"#E53BF6", fontSize:"0.72rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Registrar</button>
          <button onClick={()=>setShowForm(false)} style={{ padding:"9px 16px", borderRadius:10, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"rgba(148,163,184,0.4)", fontSize:"0.72rem", fontFamily:"Poppins, sans-serif" }}>Cancelar</button>
        </div>
      </div></GlassCard>}
      {alertas.map(al=>{
        const r=ALERT_RESULT[al.resultado];
        return <GlassCard key={al.id} accent={r.color}>
          <div style={{ padding:"16px 18px", display:"flex", flexDirection:"column", gap:10 }}>
            <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start" }}>
              <div><span style={{ fontSize:"0.52rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.25)" }}>LAPCH # {al.numero}</span><p style={{ fontSize:"0.88rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{al.producto}</p><p style={{ fontSize:"0.65rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{al.empresa}</p></div>
              <div style={{ display:"flex", flexDirection:"column", alignItems:"flex-end", gap:6 }}><Chip label={al.resultado} color={r.color} bg={r.bg} border={r.border}/><span style={{ fontSize:"0.55rem", color:al.estado==="Activa"?"#22c55e":"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif", fontWeight:600 }}>{al.estado}</span></div>
            </div>
            <p style={{ fontSize:"0.7rem", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", lineHeight:1.5 }}>{al.desc}</p>
            <div style={{ display:"flex", gap:8, alignItems:"center", justifyContent:"space-between" }}>
              <span style={{ fontSize:"0.58rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif" }}>{al.fecha}</span>
              <div style={{ display:"flex", gap:7 }}>
                {al.resultado==="Procede" && <button onClick={()=>onToast("Evaluación generada · "+al.numero)} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(239,68,68,0.1)", borderTop:"1px solid rgba(239,68,68,0.35)", borderRight:"1px solid rgba(239,68,68,0.35)", borderBottom:"1px solid rgba(239,68,68,0.35)", borderLeft:"1px solid rgba(239,68,68,0.35)", color:"#ef4444", fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Generar evaluación</button>}
                <button onClick={()=>onToast("Caso cerrado · "+al.numero)} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"rgba(148,163,184,0.4)", fontSize:"0.62rem", fontFamily:"Poppins, sans-serif" }}>Cerrar caso</button>
              </div>
            </div>
          </div>
        </GlassCard>;
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Denuncias
───────────────────────────────────────────────────────────────────── */
function DenunciasSection({ denuncias, onToast }:{ denuncias:DenunciaItem[]; onToast:(m:string)=>void }) {
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <SectionHeader title="Denuncias" subtitle={`${denuncias.filter(d=>d.estado==="Activa").length} activas`}
        action={<button onClick={()=>onToast("Formulario de denuncia abierto")} style={{ padding:"8px 16px", borderRadius:11, fontSize:"0.72rem", fontWeight:700, fontFamily:"Poppins, sans-serif", cursor:"pointer", background:"rgba(246,229,59,0.1)", borderTop:"1px solid rgba(246,229,59,0.38)", borderRight:"1px solid rgba(246,229,59,0.38)", borderBottom:"1px solid rgba(246,229,59,0.38)", borderLeft:"1px solid rgba(246,229,59,0.38)", color:"#F6E53B" }}>+ Registrar denuncia</button>}/>
      {denuncias.map(d=>{
        const r=ALERT_RESULT[d.resultado];
        return <GlassCard key={d.id} accent={r.color}><div style={{ padding:"16px 18px", display:"flex", flexDirection:"column", gap:10 }}>
          <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start" }}>
            <div><span style={{ fontSize:"0.52rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.25)" }}>{d.codigo}</span><p style={{ fontSize:"0.88rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{d.tipo}</p><p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>Recibida: {d.fechaRec}</p></div>
            <div style={{ display:"flex", flexDirection:"column", alignItems:"flex-end", gap:5 }}><Chip label={d.resultado} color={r.color} bg={r.bg} border={r.border}/><span style={{ fontSize:"0.55rem", color:d.estado==="Activa"?"#22c55e":"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif", fontWeight:600 }}>{d.estado}</span></div>
          </div>
          <p style={{ fontSize:"0.7rem", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", lineHeight:1.5 }}>{d.desc}</p>
          <div style={{ display:"flex", justifyContent:"flex-end", gap:7 }}>
            {d.resultado!=="No Procede" && <button onClick={()=>onToast("Evaluación generada · "+d.codigo)} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(229,59,246,0.1)", borderTop:"1px solid rgba(229,59,246,0.35)", borderRight:"1px solid rgba(229,59,246,0.35)", borderBottom:"1px solid rgba(229,59,246,0.35)", borderLeft:"1px solid rgba(229,59,246,0.35)", color:"#E53BF6", fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Generar evaluación</button>}
            <button onClick={()=>onToast("Denuncia remitida · "+d.codigo)} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"rgba(148,163,184,0.4)", fontSize:"0.62rem", fontFamily:"Poppins, sans-serif" }}>Remitir</button>
          </div>
        </div></GlassCard>;
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Asignaciones (list)
───────────────────────────────────────────────────────────────────── */
function AsignacionesSection({ casos, onOpenCaso }: {
  casos: Caso[];
  onOpenCaso: (id:string) => void;
}) {
  const [filter, setFilter] = useState<Priority|"Todas">("Todas");
  const filtered = filter==="Todas" ? casos : casos.filter(c=>c.priority===filter);
  const pendientes = casos.filter(c=>!c.asignado).length;

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <SectionHeader title="Asignaciones" subtitle={`${pendientes} caso${pendientes!==1?"s":""} pendiente${pendientes!==1?"s":""} sin asignar`}/>

      {/* Priority filter */}
      <div className="hide-scroll" style={{ display:"flex", gap:7, overflowX:"auto" }}>
        {(["Todas","Alta","Media","Baja"] as const).map(f=>{
          const active=filter===f; const c=f==="Todas"?"rgba(148,163,184,0.7)":PRI[f].color;
          return <button key={f} onClick={()=>setFilter(f)} style={{ flexShrink:0, padding:"5px 14px", borderRadius:99, fontSize:"0.65rem", fontWeight:600, fontFamily:"Poppins, sans-serif", cursor:"pointer", background:active?`${c}18`:"rgba(255,255,255,0.04)", borderTop:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderRight:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderBottom:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderLeft:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", color:active?c:"rgba(148,163,184,0.45)" }}>{f}</button>;
        })}
      </div>

      {/* Status summary row */}
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(110px, 1fr))", gap:9 }}>
        {[
          { label:"Sin asignar", v:casos.filter(c=>!c.asignado).length, c:"rgba(148,163,184,0.6)" },
          { label:"Asignados",   v:casos.filter(c=>!!c.asignado).length, c:"#22c55e" },
          { label:"Prioridad Alta", v:casos.filter(c=>c.priority==="Alta"&&!c.asignado).length, c:"#E53BF6" },
        ].map(s=>(
          <div key={s.label} style={{ borderRadius:13, padding:"12px 14px", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(16px)", WebkitBackdropFilter:"blur(16px)", borderTop:`1px solid rgba(255,255,255,0.06)`, borderRight:"1px solid rgba(255,255,255,0.04)", borderBottom:"1px solid rgba(255,255,255,0.04)", borderLeft:`2px solid ${s.c}55` }}>
            <p style={{ fontSize:"1.5rem", fontWeight:900, color:s.c, fontFamily:"Poppins, sans-serif", lineHeight:1 }}>{s.v}</p>
            <p style={{ fontSize:"0.54rem", color:"rgba(148,163,184,0.35)", fontFamily:"Poppins, sans-serif", textTransform:"uppercase", letterSpacing:"0.08em", marginTop:3 }}>{s.label}</p>
          </div>
        ))}
      </div>

      {/* Case list */}
      <div style={{ display:"flex", flexDirection:"column", gap:10 }}>
        {filtered.map(caso=>{
          const p=PRI[caso.priority];
          const assigned=!!caso.asignado;
          return (
            <div key={caso.id} style={{ borderRadius:16, padding:"14px 16px", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(24px) saturate(160%)", WebkitBackdropFilter:"blur(24px) saturate(160%)", borderTop:`1px solid ${assigned?"rgba(34,197,94,0.15)":p.color+"22"}`, borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:`2px solid ${assigned?"rgba(34,197,94,0.55)":p.color+"55"}`, display:"flex", alignItems:"center", gap:14 }}>
              <div style={{ flex:1, minWidth:0 }}>
                <div style={{ display:"flex", alignItems:"center", gap:8, marginBottom:4, flexWrap:"wrap" }}>
                  <span style={{ fontSize:"0.5rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.25)" }}>{caso.id}</span>
                  {caso.solicitudId && <span style={{ fontSize:"0.5rem", fontFamily:"'Courier New',monospace", color:"rgba(59,246,229,0.35)" }}>← {caso.solicitudId}</span>}
                </div>
                <p style={{ fontSize:"0.88rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap", marginBottom:4 }}>{caso.empresa}</p>
                <p style={{ fontSize:"0.65rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>{caso.tipo} · {caso.zona} · {caso.fecha}</p>
                {caso.asignado && <p style={{ fontSize:"0.62rem", color:"#22c55e", fontFamily:"Poppins, sans-serif", marginTop:4, fontWeight:600 }}>✓ {caso.asignado}</p>}
              </div>
              <div style={{ display:"flex", flexDirection:"column", alignItems:"flex-end", gap:8, flexShrink:0 }}>
                <Chip label={caso.priority} color={p.color} bg={p.bg} border={p.border}/>
                <button onClick={()=>onOpenCaso(caso.id)} style={{ padding:"8px 14px", borderRadius:10, cursor:"pointer", background: assigned?"rgba(34,197,94,0.08)":"linear-gradient(135deg, rgba(229,59,246,0.12), rgba(59,246,229,0.06))", borderTop: assigned?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(229,59,246,0.4)", borderRight: assigned?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(59,246,229,0.3)", borderBottom: assigned?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(59,246,229,0.3)", borderLeft: assigned?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(229,59,246,0.4)", color: assigned?"#22c55e":"#E53BF6", fontSize:"0.68rem", fontWeight:700, fontFamily:"Poppins, sans-serif", whiteSpace:"nowrap" }}>
                  {assigned ? "Reasignar" : "Asignar Técnico →"}
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Asignar Caso (full-page picker)
───────────────────────────────────────────────────────────────────── */
function AsignarCasoSection({ caso, onConfirm, onBack }: {
  caso: Caso;
  onConfirm: (tech: Tecnico) => void;
  onBack: () => void;
}) {
  const [selectedTechId, setSelectedTechId] = useState<string|null>(null);
  const [fechaProg, setFechaProg] = useState("");
  const [horaProg,  setHoraProg]  = useState("");
  const [obs,       setObs]       = useState("");
  const [confirming,setConfirming]= useState(false);

  const p = PRI[caso.priority];
  const selectedTech = TECHNICIANS.find(t=>t.id===selectedTechId);

  const handleConfirm = () => {
    if (!selectedTech) return;
    setConfirming(true);
    setTimeout(()=>{ onConfirm(selectedTech); }, 1400);
  };

  const techStatusColor = (s:TechStatus) =>
    s==="Disponible" ? "#22c55e" : s==="En Campo" ? "#F6E53B" : "rgba(148,163,184,0.3)";

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:0, maxWidth:720 }}>
      {/* Back */}
      <button onClick={onBack} style={{ display:"inline-flex", alignItems:"center", gap:8, marginBottom:20, background:"none", border:"none", cursor:"pointer", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", fontSize:"0.75rem", fontWeight:500, padding:0 }}>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M19 12H5M12 19l-7-7 7-7" stroke="rgba(148,163,184,0.5)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
        Volver a Asignaciones
      </button>

      {/* Header */}
      <div style={{ marginBottom:20 }}>
        <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>EVALIA · COORDINADOR</p>
        <h1 style={{ fontSize:"1.2rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em", marginBottom:4 }}>Asignar Técnico Evaluador</h1>
        <p style={{ fontSize:"0.72rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>Selecciona el técnico disponible más adecuado para este caso.</p>
      </div>

      {/* Caso card */}
      <div style={{ borderRadius:18, padding:"16px 20px", background:"rgba(10,18,36,0.9)", backdropFilter:"blur(32px)", WebkitBackdropFilter:"blur(32px)", borderTop:`1px solid ${p.color}35`, borderRight:"1px solid rgba(255,255,255,0.07)", borderBottom:"1px solid rgba(255,255,255,0.07)", borderLeft:`3px solid ${p.color}88`, marginBottom:16, overflow:"hidden", position:"relative" }}>
        <div style={{ position:"absolute", top:0, left:0, right:0, height:2, background:`linear-gradient(90deg, ${p.color}, transparent)`, opacity:0.6 }}/>
        <div style={{ display:"flex", alignItems:"flex-start", justifyContent:"space-between", gap:12 }}>
          <div style={{ minWidth:0, flex:1 }}>
            <span style={{ fontSize:"0.48rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.25)", display:"block", marginBottom:4 }}>{caso.id} {caso.solicitudId?`← ${caso.solicitudId}`:""}</span>
            <p style={{ fontSize:"1rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>{caso.empresa}</p>
            <p style={{ fontSize:"0.72rem", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif" }}>{caso.tipo}</p>
          </div>
          <div style={{ display:"flex", flexDirection:"column", alignItems:"flex-end", gap:6 }}>
            <Chip label={caso.priority} color={p.color} bg={p.bg} border={p.border}/>
          </div>
        </div>
        <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(100px, 1fr))", gap:12, marginTop:14 }}>
          {[{label:"Zona",v:caso.zona},{label:"Fecha",v:caso.fecha},{label:"Solicitud",v:caso.solicitudId??"—"}].map(f=>(
            <div key={f.label}>
              <p style={{ fontSize:"0.52rem", fontWeight:700, color:"rgba(148,163,184,0.28)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif" }}>{f.label}</p>
              <p style={{ fontSize:"0.78rem", fontWeight:600, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", marginTop:2 }}>{f.v}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Scheduling fields */}
      <div style={{ borderRadius:16, padding:"16px 20px", background:"rgba(10,18,36,0.7)", backdropFilter:"blur(24px)", WebkitBackdropFilter:"blur(24px)", borderTop:"1px solid rgba(255,255,255,0.07)", borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:"1px solid rgba(255,255,255,0.05)", marginBottom:16 }}>
        <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:12 }}>PROGRAMAR VISITA</p>
        <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(160px, 1fr))", gap:10, marginBottom:10 }}>
          {[{label:"Fecha de inspección",v:fechaProg,s:setFechaProg,type:"date"},{label:"Hora",v:horaProg,s:setHoraProg,type:"time"}].map(f=>(
            <div key={f.label} style={{ position:"relative" }}>
              <input type={f.type} value={f.v} onChange={e=>f.s(e.target.value)} style={{ width:"100%", padding:"1.2rem 1rem 0.5rem", borderRadius:12, outline:"none", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.1)", borderRight:"1px solid rgba(255,255,255,0.1)", borderBottom:"1px solid rgba(255,255,255,0.1)", borderLeft:"1px solid rgba(255,255,255,0.1)", color:"#f1f5f9", fontFamily:"Poppins, sans-serif", fontSize:"0.82rem", boxSizing:"border-box" as const, colorScheme:"dark" as const }}/>
              <label style={{ position:"absolute", top:"0.5rem", left:"1rem", fontSize:"0.52rem", fontWeight:700, color:"rgba(229,59,246,0.6)", textTransform:"uppercase" as const, letterSpacing:"0.08em", fontFamily:"Poppins, sans-serif", pointerEvents:"none" as const }}>{f.label}</label>
            </div>
          ))}
        </div>
        <div style={{ position:"relative" }}>
          <textarea value={obs} onChange={e=>setObs(e.target.value)} placeholder="Instrucciones adicionales para el técnico…" rows={2} style={{ width:"100%", padding:"14px", borderRadius:12, resize:"none" as const, outline:"none", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.1)", borderRight:"1px solid rgba(255,255,255,0.1)", borderBottom:"1px solid rgba(255,255,255,0.1)", borderLeft:"1px solid rgba(255,255,255,0.1)", color:"#f1f5f9", fontFamily:"Poppins, sans-serif", fontSize:"0.82rem", boxSizing:"border-box" as const }}/>
        </div>
      </div>

      {/* Technician picker */}
      <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:10 }}>SELECCIONAR TÉCNICO EVALUADOR</p>
      <div style={{ display:"flex", flexDirection:"column", gap:8, marginBottom:16 }}>
        {TECHNICIANS.map(tech=>{
          const isSelected = selectedTechId===tech.id;
          const isDisabled = tech.status==="Descanso";
          const statusColor= techStatusColor(tech.status);
          const load = tech.cases / tech.maxCases;
          const loadColor = load>=0.8 ? "#E53BF6" : load>=0.5 ? "#F6E53B" : "#22c55e";

          return (
            <button key={tech.id} onClick={()=>!isDisabled && setSelectedTechId(tech.id)} disabled={isDisabled}
              style={{ display:"flex", alignItems:"center", gap:14, padding:"14px 16px", borderRadius:14, cursor:isDisabled?"not-allowed":"pointer", opacity:isDisabled?0.38:1, transition:"all 0.18s ease", background: isSelected?"rgba(229,59,246,0.09)":"rgba(10,18,36,0.75)", backdropFilter:"blur(20px)", WebkitBackdropFilter:"blur(20px)", borderTop: isSelected?"1px solid rgba(229,59,246,0.5)":"1px solid rgba(255,255,255,0.07)", borderRight: isSelected?"1px solid rgba(229,59,246,0.25)":"1px solid rgba(255,255,255,0.05)", borderBottom: isSelected?"1px solid rgba(229,59,246,0.25)":"1px solid rgba(255,255,255,0.05)", borderLeft: isSelected?"3px solid rgba(229,59,246,0.8)":"2px solid rgba(255,255,255,0.07)", boxShadow: isSelected?"0 4px 20px rgba(229,59,246,0.15)":"none" }}>
              {/* Avatar */}
              <div style={{ width:44, height:44, borderRadius:13, flexShrink:0, background: isSelected?"linear-gradient(135deg,#E53BF6,#8b5cf6,#3BF6E5)":"linear-gradient(135deg, rgba(229,59,246,0.3), rgba(59,246,229,0.3))", display:"flex", alignItems:"center", justifyContent:"center", fontSize:"0.85rem", fontWeight:800, color:"white", boxShadow: isSelected?"0 4px 16px rgba(229,59,246,0.4)":"none" }}>{tech.initials}</div>

              {/* Info */}
              <div style={{ flex:1, minWidth:0 }}>
                <div style={{ display:"flex", alignItems:"center", gap:8, marginBottom:3 }}>
                  <p style={{ fontSize:"0.88rem", fontWeight:700, color: isSelected?"#f1f5f9":"rgba(241,245,249,0.85)", fontFamily:"Poppins, sans-serif" }}>{tech.name}</p>
                  <span style={{ fontSize:"0.6rem", fontWeight:700, color:statusColor, fontFamily:"Poppins, sans-serif" }}>{tech.status}</span>
                </div>
                <p style={{ fontSize:"0.64rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif", marginBottom:6 }}>{tech.specialty} · {tech.zona}</p>
                {/* Load bar */}
                <div style={{ display:"flex", alignItems:"center", gap:8 }}>
                  <div style={{ flex:1, height:3.5, borderRadius:99, background:"rgba(255,255,255,0.06)", overflow:"hidden" }}>
                    <div style={{ height:"100%", width:`${load*100}%`, background:`linear-gradient(90deg, ${loadColor}99, ${loadColor})`, borderRadius:99, transition:"width 0.4s ease" }}/>
                  </div>
                  <span style={{ fontSize:"0.58rem", color:loadColor, fontFamily:"Poppins, sans-serif", fontWeight:700, flexShrink:0 }}>{tech.cases}/{tech.maxCases}</span>
                </div>
              </div>

              {/* Selection indicator */}
              <div style={{ width:20, height:20, borderRadius:"50%", flexShrink:0, border:`2px solid ${isSelected?"#E53BF6":"rgba(255,255,255,0.15)"}`, background: isSelected?"#E53BF6":"transparent", display:"flex", alignItems:"center", justifyContent:"center", transition:"all 0.18s ease" }}>
                {isSelected && <svg width="10" height="10" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="white" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"/></svg>}
              </div>
            </button>
          );
        })}
      </div>

      {/* Confirm button */}
      <div style={{ display:"flex", gap:10 }}>
        <button onClick={onBack} style={{ flex:1, padding:"14px 0", borderRadius:14, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.1)", borderRight:"1px solid rgba(255,255,255,0.1)", borderBottom:"1px solid rgba(255,255,255,0.1)", borderLeft:"1px solid rgba(255,255,255,0.1)", color:"rgba(148,163,184,0.5)", fontSize:"0.85rem", fontWeight:600, fontFamily:"Poppins, sans-serif" }}>Cancelar</button>
        <button onClick={handleConfirm} disabled={!selectedTech||confirming}
          style={{ flex:2, padding:"14px 0", borderRadius:14, border:"none", cursor:selectedTech&&!confirming?"pointer":"not-allowed", fontWeight:800, fontSize:"0.92rem", fontFamily:"Poppins, sans-serif", letterSpacing:"0.02em", transition:"all 0.2s ease", background: selectedTech?"linear-gradient(135deg,#E53BF6,#8b5cf6,#3BF6E5)":"rgba(255,255,255,0.06)", color: selectedTech?"white":"rgba(148,163,184,0.3)", boxShadow: selectedTech?"0 8px 28px rgba(229,59,246,0.4)":"none", opacity:confirming?0.8:1, display:"flex", alignItems:"center", justifyContent:"center", gap:8 }}>
          {confirming
            ? <><span style={{ width:16,height:16,borderRadius:"50%",borderTop:"2px solid white",borderRight:"2px solid rgba(255,255,255,0.25)",borderBottom:"2px solid rgba(255,255,255,0.25)",borderLeft:"2px solid rgba(255,255,255,0.25)",animation:"cdSpin 0.7s linear infinite",display:"inline-block" }}/>Procesando…</>
            : <><svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2" stroke="white" strokeWidth="2"/><circle cx="9" cy="7" r="4" stroke="white" strokeWidth="2"/><path d="M23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75" stroke="white" strokeWidth="2" strokeLinecap="round"/></svg>Confirmar Asignación</>}
        </button>
      </div>

      {!selectedTech && <p style={{ textAlign:"center", fontSize:"0.65rem", color:"rgba(148,163,184,0.28)", fontFamily:"Poppins, sans-serif", marginTop:6 }}>Selecciona un técnico disponible para confirmar la asignación.</p>}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Confirmación de Asignación
───────────────────────────────────────────────────────────────────── */
function ConfirmacionSection({ caso, tecnico, onGoAsignaciones, onGoInicio }: {
  caso: Caso; tecnico: Tecnico;
  onGoAsignaciones: () => void;
  onGoInicio: () => void;
}) {
  const [shown, setShown] = useState(false);
  useEffect(()=>{ const t=setTimeout(()=>setShown(true),120); return ()=>clearTimeout(t); },[]);
  const p=PRI[caso.priority];

  return (
    <div style={{ display:"flex", flexDirection:"column", alignItems:"center", justifyContent:"flex-start", paddingTop:32, gap:0 }}>
      <div style={{ width:"100%", maxWidth:540, display:"flex", flexDirection:"column", gap:20 }}>
        {/* Success ring */}
        <div style={{ display:"flex", justifyContent:"center", marginBottom:8 }}>
          <div style={{ position:"relative", width:96, height:96 }}>
            <div style={{ width:96, height:96, borderRadius:"50%", background:"rgba(34,197,94,0.1)", borderTop:"2px solid rgba(34,197,94,0.6)", borderRight:"2px solid rgba(34,197,94,0.6)", borderBottom:"2px solid rgba(34,197,94,0.6)", borderLeft:"2px solid rgba(34,197,94,0.6)", display:"flex", alignItems:"center", justifyContent:"center", boxShadow:"0 0 48px rgba(34,197,94,0.25), inset 0 0 32px rgba(34,197,94,0.05)", transition:"all 0.5s ease", transform:shown?"scale(1)":"scale(0.6)", opacity:shown?1:0 }}>
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" style={{ transition:"all 0.6s ease 0.2s", transform:shown?"scale(1)":"scale(0)", opacity:shown?1:0 }}>
                <path d="M20 6L9 17l-5-5" stroke="#22c55e" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/>
              </svg>
            </div>
            {/* Glow pulse rings */}
            <div style={{ position:"absolute", inset:-10, borderRadius:"50%", borderTop:"1px solid rgba(34,197,94,0.2)", borderRight:"1px solid rgba(34,197,94,0.2)", borderBottom:"1px solid rgba(34,197,94,0.2)", borderLeft:"1px solid rgba(34,197,94,0.2)", animation:"cdPulse 2.5s ease-in-out infinite" }}/>
          </div>
        </div>

        {/* Title */}
        <div style={{ textAlign:"center" }}>
          <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(34,197,94,0.6)", letterSpacing:"0.16em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:6 }}>ASIGNACIÓN CONFIRMADA</p>
          <h1 style={{ fontSize:"1.4rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em", marginBottom:4 }}>Técnico asignado con éxito</h1>
          <p style={{ fontSize:"0.8rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>El técnico evaluador ha sido notificado y el caso ha sido actualizado.</p>
        </div>

        {/* Summary card */}
        <div style={{ borderRadius:20, overflow:"hidden", background:"rgba(10,18,36,0.9)", backdropFilter:"blur(32px)", WebkitBackdropFilter:"blur(32px)", borderTop:"1px solid rgba(34,197,94,0.25)", borderRight:"1px solid rgba(255,255,255,0.06)", borderBottom:"1px solid rgba(255,255,255,0.06)", borderLeft:"1px solid rgba(255,255,255,0.06)" }}>
          <div style={{ height:3, background:"linear-gradient(90deg,#22c55e,#3BF6E5)" }}/>
          <div style={{ padding:"20px 22px", display:"flex", flexDirection:"column", gap:14 }}>
            {/* Case info */}
            <div>
              <p style={{ fontSize:"0.54rem", fontWeight:700, color:"rgba(148,163,184,0.28)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:8 }}>CASO ASIGNADO</p>
              <div style={{ display:"flex", alignItems:"flex-start", justifyContent:"space-between", gap:12 }}>
                <div>
                  <span style={{ fontSize:"0.52rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.3)", display:"block", marginBottom:3 }}>{caso.id}</span>
                  <p style={{ fontSize:"0.92rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{caso.empresa}</p>
                  <p style={{ fontSize:"0.68rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>{caso.tipo} · {caso.zona}</p>
                </div>
                <Chip label={caso.priority} color={p.color} bg={p.bg} border={p.border}/>
              </div>
            </div>

            <div style={{ height:1, background:"rgba(255,255,255,0.05)" }}/>

            {/* Technician info */}
            <div>
              <p style={{ fontSize:"0.54rem", fontWeight:700, color:"rgba(148,163,184,0.28)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:8 }}>TÉCNICO EVALUADOR</p>
              <div style={{ display:"flex", alignItems:"center", gap:12 }}>
                <div style={{ width:44, height:44, borderRadius:13, background:"linear-gradient(135deg,#22c55e,#06b6d4)", display:"flex", alignItems:"center", justifyContent:"center", fontSize:"0.85rem", fontWeight:800, color:"white", flexShrink:0, boxShadow:"0 4px 16px rgba(34,197,94,0.3)" }}>{tecnico.initials}</div>
                <div style={{ flex:1 }}>
                  <p style={{ fontSize:"0.92rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{tecnico.name}</p>
                  <p style={{ fontSize:"0.65rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>{tecnico.specialty}</p>
                  <p style={{ fontSize:"0.6rem", color:"#22c55e", fontFamily:"Poppins, sans-serif", fontWeight:600, marginTop:2 }}>● Disponible · {tecnico.cases+1} casos activos</p>
                </div>
                <div style={{ width:28, height:28, borderRadius:99, background:"rgba(34,197,94,0.12)", borderTop:"1px solid rgba(34,197,94,0.4)", borderRight:"1px solid rgba(34,197,94,0.4)", borderBottom:"1px solid rgba(34,197,94,0.4)", borderLeft:"1px solid rgba(34,197,94,0.4)", display:"flex", alignItems:"center", justifyContent:"center" }}>
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#22c55e" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
                </div>
              </div>
            </div>

            <div style={{ height:1, background:"rgba(255,255,255,0.05)" }}/>

            {/* Timestamp */}
            <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center" }}>
              <span style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif" }}>Asignado el 01 sep 2026 · 14:32</span>
              <span style={{ fontSize:"0.62rem", fontWeight:700, color:"rgba(34,197,94,0.7)", fontFamily:"Poppins, sans-serif", background:"rgba(34,197,94,0.08)", padding:"3px 10px", borderRadius:99, borderTop:"1px solid rgba(34,197,94,0.25)", borderRight:"1px solid rgba(34,197,94,0.25)", borderBottom:"1px solid rgba(34,197,94,0.25)", borderLeft:"1px solid rgba(34,197,94,0.25)" }}>Notificación enviada</span>
            </div>
          </div>
        </div>

        {/* CTA buttons */}
        <div style={{ display:"flex", gap:10 }}>
          <button onClick={onGoAsignaciones} style={{ flex:1, padding:"14px 0", borderRadius:14, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.12)", borderRight:"1px solid rgba(255,255,255,0.12)", borderBottom:"1px solid rgba(255,255,255,0.12)", borderLeft:"1px solid rgba(255,255,255,0.12)", color:"rgba(148,163,184,0.6)", fontSize:"0.85rem", fontWeight:600, fontFamily:"Poppins, sans-serif" }}>
            Ver Asignaciones
          </button>
          <button onClick={onGoInicio} style={{ flex:1, padding:"14px 0", borderRadius:14, border:"none", cursor:"pointer", fontWeight:800, fontSize:"0.85rem", fontFamily:"Poppins, sans-serif", background:"linear-gradient(135deg,#22c55e,#06b6d4)", color:"white", boxShadow:"0 8px 24px rgba(34,197,94,0.35)" }}>
            Ir al Inicio
          </button>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Reportes
───────────────────────────────────────────────────────────────────── */
function ReportesSection({ onToast, onOpenReport }:{ onToast:(m:string)=>void; onOpenReport:(r:SignedReport)=>void }) {
  const statusStyle: Record<string,{color:string;bg:string;border:string}> = {
    "En Revisión":{ color:"#8b5cf6", bg:"rgba(139,92,246,0.1)", border:"rgba(139,92,246,0.35)" },
    "Aprobado":   { color:"#22c55e", bg:"rgba(34,197,94,0.1)",  border:"rgba(34,197,94,0.35)"  },
    "Devuelto":   { color:"#E53BF6", bg:"rgba(229,59,246,0.1)", border:"rgba(229,59,246,0.35)" },
  };
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <SectionHeader title="Reportes" subtitle="Revisión y aprobación de informes"/>
      {REPORTES_DATA.map(rep=>{
        const st=statusStyle[rep.estado];
        return <GlassCard key={rep.id} accent={RISK[rep.riesgo].color}>
          <div style={{ padding:"16px 18px", display:"flex", flexDirection:"column", gap:10 }}>
            <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start" }}>
              <div><span style={{ fontSize:"0.52rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.25)" }}>{rep.id}</span><p style={{ fontSize:"0.88rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{rep.empresa}</p><p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{rep.tipo} · {rep.tecnico}</p></div>
              <div style={{ display:"flex", flexDirection:"column", alignItems:"flex-end", gap:6 }}><Chip label={rep.estado} color={st.color} bg={st.bg} border={st.border}/><Chip label={rep.riesgo} color={RISK[rep.riesgo].color} bg={RISK[rep.riesgo].bg} border={RISK[rep.riesgo].color+"40"}/></div>
            </div>
            <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center" }}>
              <span style={{ fontSize:"0.6rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif" }}>{rep.fecha}</span>
              <div style={{ display:"flex", gap:7 }}>
                {rep.estado==="En Revisión" && <><button onClick={()=>onToast("Informe aprobado · "+rep.id)} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(34,197,94,0.1)", borderTop:"1px solid rgba(34,197,94,0.35)", borderRight:"1px solid rgba(34,197,94,0.35)", borderBottom:"1px solid rgba(34,197,94,0.35)", borderLeft:"1px solid rgba(34,197,94,0.35)", color:"#22c55e", fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Aprobar</button><button onClick={()=>onToast("Informe devuelto · "+rep.id)} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(229,59,246,0.08)", borderTop:"1px solid rgba(229,59,246,0.3)", borderRight:"1px solid rgba(229,59,246,0.3)", borderBottom:"1px solid rgba(229,59,246,0.3)", borderLeft:"1px solid rgba(229,59,246,0.3)", color:"#E53BF6", fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Devolver</button></>}
                {rep.estado==="Aprobado" && <button onClick={()=>onOpenReport({ report:{ id:rep.id, empresa:rep.empresa, tipo:rep.tipo, riesgo:rep.riesgo, fecha:rep.fecha, tecnico:rep.tecnico }, signature:{ signerName:rep.tecnico, signedAt:rep.fecha } })} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(34,197,94,0.08)", borderTop:"1px solid rgba(34,197,94,0.3)", borderRight:"1px solid rgba(34,197,94,0.3)", borderBottom:"1px solid rgba(34,197,94,0.3)", borderLeft:"1px solid rgba(34,197,94,0.3)", color:"#22c55e", fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Descargar PDF</button>}
                {rep.estado==="Devuelto" && <button onClick={()=>onToast("Corrección solicitada · "+rep.id)} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(229,59,246,0.08)", borderTop:"1px solid rgba(229,59,246,0.3)", borderRight:"1px solid rgba(229,59,246,0.3)", borderBottom:"1px solid rgba(229,59,246,0.3)", borderLeft:"1px solid rgba(229,59,246,0.3)", color:"#E53BF6", fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Solicitar corrección</button>}
              </div>
            </div>
          </div>
        </GlassCard>;
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────
   Section: Configuración
───────────────────────────────────────────────────────────────────── */
function ConfiguracionSection({ userName, onToast }:{ userName:string; onToast:(m:string)=>void }) {
  const [nombre, setNombre]=useState(userName); const [correo,setCorreo]=useState("coordinador@evalia.com"); const [telefono,setTelefono]=useState("+1 (809) 555-0100");
  const [n1,setN1]=useState(true); const [n2,setN2]=useState(true); const [n3,setN3]=useState(false);
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16, maxWidth:680 }}>
      <SectionHeader title="Configuración" subtitle="Preferencias de la cuenta"/>
      <GlassCard accent="#3BF6E5"><div style={{ padding:"20px" }}>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(59,246,229,0.5)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>PERFIL DE USUARIO</p>
        <div style={{ display:"flex", flexDirection:"column", gap:11 }}>
          {[{label:"Nombre completo",value:nombre,set:setNombre},{label:"Correo electrónico",value:correo,set:setCorreo},{label:"Teléfono",value:telefono,set:setTelefono}].map(f=>(
            <div key={f.label} style={{ position:"relative" }}>
              <input value={f.value} onChange={e=>f.set(e.target.value)} style={{ width:"100%", padding:"1.2rem 1rem 0.5rem", borderRadius:11, outline:"none", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(59,246,229,0.2)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"#f1f5f9", fontFamily:"Poppins, sans-serif", fontSize:"0.875rem", boxSizing:"border-box" as const }}/>
              <label style={{ position:"absolute", top:"0.5rem", left:"1rem", fontSize:"0.55rem", fontWeight:700, color:"rgba(59,246,229,0.6)", textTransform:"uppercase" as const, letterSpacing:"0.08em", fontFamily:"Poppins, sans-serif", pointerEvents:"none" as const }}>{f.label}</label>
            </div>
          ))}
        </div>
        <button onClick={()=>onToast("Perfil actualizado correctamente")} style={{ marginTop:14, width:"100%", padding:"11px 0", borderRadius:12, cursor:"pointer", background:"linear-gradient(135deg,#3BF6E5,#06b6d4)", border:"none", color:"#0F172A", fontFamily:"Poppins, sans-serif", fontWeight:800, fontSize:"0.85rem" }}>Guardar cambios</button>
      </div></GlassCard>
      <GlassCard accent="#E53BF6"><div style={{ padding:"20px" }}>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(229,59,246,0.5)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>NOTIFICACIONES</p>
        {[{label:"Nuevas evaluaciones asignadas",value:n1,set:setN1},{label:"Alertas LAPCH urgentes",value:n2,set:setN2},{label:"Informes pendientes de revisión",value:n3,set:setN3}].map(n=>(
          <div key={n.label} style={{ display:"flex", justifyContent:"space-between", alignItems:"center", padding:"10px 0", borderBottom:"1px solid rgba(255,255,255,0.05)" }}>
            <span style={{ fontSize:"0.78rem", color:"rgba(148,163,184,0.65)", fontFamily:"Poppins, sans-serif" }}>{n.label}</span>
            <button onClick={()=>n.set(!n.value)} style={{ width:44, height:24, borderRadius:99, cursor:"pointer", border:"none", background:n.value?"linear-gradient(135deg,#E53BF6,#8b5cf6)":"rgba(255,255,255,0.08)", position:"relative", transition:"all 0.2s ease" }}>
              <span style={{ position:"absolute", top:3, left:n.value?22:3, width:18, height:18, borderRadius:"50%", background:"white", transition:"left 0.2s ease" }}/>
            </button>
          </div>
        ))}
      </div></GlassCard>
      <button onClick={()=>onToast("Contraseña: revisa tu correo")} style={{ padding:"12px 0", borderRadius:12, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", fontSize:"0.8rem", fontWeight:600 }}>Cambiar contraseña</button>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════════
   Main Component
══════════════════════════════════════════════════════════════════════ */
export default function CoordinatorDashboard({
  onBack, userName = "María García", accessToken,
}: {
  onBack?: () => void;
  userName?: string;
  onCompanyProfile?: () => void;
  accessToken: string;
}) {
  const width    = useWidth();
  const isMobile = width < 768;
  const isTablet = width >= 768 && width < 1100;
  const expanded = !isMobile && !isTablet;

  const [section,       setSection]      = useState<Section>("inicio");
  const [casos,         setCasos]        = useState<Caso[]>(CASOS_INIT);
  const [selectedCaseId,setSelectedCase] = useState<string|null>(null);
  const [confirmedTech, setConfirmedTech]= useState<Tecnico|null>(null);
  const [drawerOpen,    setDrawer]       = useState(false);
  const [toast,         setToast]        = useState<string|null>(null);
  /* Informe ya firmado por el técnico: el coordinador solo lo descarga o imprime. */
  const [signedReport,  setSignedReport]  = useState<SignedReport|null>(null);
  const [alertas,       setAlertas]      = useState<AlertaItem[]>([]);
  const [denuncias,     setDenuncias]    = useState<DenunciaItem[]>([]);

  const showToast = (msg: string) => { setToast(msg); setTimeout(()=>setToast(null), 2800); };

  useEffect(() => {
    void Promise.all([listAlerts(accessToken), listComplaints(accessToken), listCompanies(accessToken)])
      .then(([alertsData, complaintsData, companies]) => {
        setAlertas(alertsData.map(a => toAlertaItem(a, companies)));
        setDenuncias(complaintsData.map(toDenunciaItem));
      })
      .catch(error => showToast(error instanceof Error ? error.message : "No fue posible cargar alertas y denuncias."));
  }, [accessToken]);

  const navigate = (s: Section) => { setSection(s); if(isMobile) setDrawer(false); };

  const openCaso = (id: string) => { setSelectedCase(id); navigate("asignar-caso"); };

  const handleConfirmAssignment = (tech: Tecnico) => {
    if (!selectedCaseId) return;
    setCasos(prev=>prev.map(c=>c.id===selectedCaseId ? {...c,asignado:tech.name} : c));
    setConfirmedTech(tech);
    navigate("confirmacion");
  };

  const selectedCaso  = casos.find(c=>c.id===selectedCaseId) ?? null;
  const pendingCount  = casos.filter(c=>!c.asignado).length;
  const totalBadge    = pendingCount + 3; /* alertas + denuncias */

  const sectionLabel = (): string => {
    if (section==="asignar-caso")  return "Asignar Técnico";
    if (section==="confirmacion")  return "Asignación Confirmada";
    return NAV.find(n=>n.id===section)?.label ?? "Inicio";
  };

  const sidebarActiveId: Section = (section==="asignar-caso"||section==="confirmacion") ? "asignaciones" : section;

  return (
    <>
      <style>{`
        @keyframes cdPulse { 0%,100%{transform:scale(1);opacity:1} 50%{transform:scale(1.5);opacity:0.45} }
        @keyframes cdSpin  { to{transform:rotate(360deg)} }
        @keyframes cdToast { from{opacity:0;transform:translateY(10px) scale(0.96)} to{opacity:1;transform:none} }
        .hide-scroll::-webkit-scrollbar{display:none}
        .hide-scroll{-ms-overflow-style:none;scrollbar-width:none}
      `}</style>

      <div style={{ display:"flex", height:"100svh", background:"#0F172A", fontFamily:"Poppins, sans-serif", overflow:"hidden", position:"relative" }}>
        {/* Orbs */}
        <div className="mesh-orb"            style={{ width:500, height:500, background:"rgba(229,59,246,0.14)", top:"-120px", left:"-60px", zIndex:0 }}/>
        <div className="mesh-orb mesh-orb-2" style={{ width:400, height:400, background:"rgba(59,246,229,0.12)", bottom:"-80px", right:"-60px", zIndex:0 }}/>
        <div className="mesh-orb mesh-orb-3" style={{ width:250, height:250, background:"rgba(139,92,246,0.1)", top:"45%", left:"42%", zIndex:0 }}/>

        {isMobile && drawerOpen && <div onClick={()=>setDrawer(false)} style={{ position:"fixed", inset:0, background:"rgba(0,0,0,0.65)", zIndex:40, backdropFilter:"blur(3px)" }}/>}

        {/* ── Sidebar ── */}
        <aside style={{ position:"relative", top:0, left:0, height:"100%", width:isTablet?64:220, flexShrink:0, zIndex:10, transition:"width 0.25s ease", background:"rgba(7,12,24,0.96)", backdropFilter:"blur(36px)", WebkitBackdropFilter:"blur(36px)", borderRight:"1px solid rgba(255,255,255,0.07)", display:isMobile?"none":"flex", flexDirection:"column" }}>
          <div style={{ padding:expanded?"20px 18px 16px":"18px 0 14px", display:"flex", alignItems:"center", gap:11, justifyContent:expanded?"flex-start":"center", borderBottom:"1px solid rgba(255,255,255,0.06)", flexShrink:0 }}>
            <div style={{ width:36, height:36, borderRadius:10, background:"linear-gradient(135deg,rgba(229,59,246,0.35),rgba(59,246,229,0.35))", borderTop:"1.5px solid rgba(229,59,246,0.5)", borderRight:"1.5px solid rgba(59,246,229,0.4)", borderBottom:"1.5px solid rgba(59,246,229,0.4)", borderLeft:"1.5px solid rgba(229,59,246,0.5)", display:"flex", alignItems:"center", justifyContent:"center", flexShrink:0 }}>
              <span style={{ fontSize:"0.75rem", fontWeight:900, background:"linear-gradient(135deg,#E53BF6,#3BF6E5)", WebkitBackgroundClip:"text", WebkitTextFillColor:"transparent", backgroundClip:"text" }}>CO</span>
            </div>
            {expanded && <div><p style={{ fontSize:"0.8rem", fontWeight:700, color:"#f1f5f9" }}>Coordinador</p><p style={{ fontSize:"0.52rem", color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.07em" }}>Evalia BPM</p></div>}
          </div>

          <nav className="hide-scroll" style={{ flex:1, padding:expanded?"14px 10px":"14px 0", display:"flex", flexDirection:"column", gap:2, overflowY:"auto" }}>
            {expanded && <p style={{ fontSize:"0.52rem", fontWeight:700, color:"rgba(148,163,184,0.22)", letterSpacing:"0.14em", textTransform:"uppercase", padding:"0 6px 8px" }}>NAVEGACIÓN</p>}
            {NAV.filter(n=>NAV_VISIBLE_IDS.includes(n.id)).map(item=>{
              const active = sidebarActiveId===item.id;
              const c = active?"#E53BF6":"rgba(148,163,184,0.4)";
              const badge = item.id==="asignaciones" ? pendingCount : item.badge;
              return (
                <button key={item.id} onClick={()=>navigate(item.id)} title={!expanded?item.label:undefined}
                  style={{ display:"flex", alignItems:"center", gap:10, padding:expanded?"9px 12px":"10px", justifyContent:expanded?"flex-start":"center", borderRadius:10, border:"none", cursor:"pointer", background:active?"rgba(229,59,246,0.1)":"transparent", color:c, fontFamily:"Poppins, sans-serif", fontWeight:active?600:400, fontSize:"0.78rem", transition:"all 0.18s ease", boxShadow:active?"inset 0 0 0 1px rgba(229,59,246,0.18)":"none", position:"relative", width:"100%" }}>
                  {active && <div style={{ position:"absolute", left:-10, top:"50%", transform:"translateY(-50%)", width:3, height:16, borderRadius:99, background:"#E53BF6", boxShadow:"0 0 10px #E53BF6" }}/>}
                  <span>{item.icon(c)}</span>
                  {expanded && <span style={{ flex:1, textAlign:"left" }}>{item.label}</span>}
                  {expanded && badge ? <span style={{ padding:"1px 6px", borderRadius:99, background:"rgba(229,59,246,0.15)", borderTop:"1px solid rgba(229,59,246,0.35)", borderRight:"1px solid rgba(229,59,246,0.35)", borderBottom:"1px solid rgba(229,59,246,0.35)", borderLeft:"1px solid rgba(229,59,246,0.35)", fontSize:"0.5rem", fontWeight:800, color:"#E53BF6" }}>{badge}</span> : null}
                  {!expanded && badge ? <span style={{ position:"absolute", top:6, right:6, width:7, height:7, borderRadius:"50%", background:"#E53BF6" }}/> : null}
                </button>
              );
            })}
          </nav>

          <div style={{ padding:expanded?"12px 10px":"12px 0", borderTop:"1px solid rgba(255,255,255,0.06)", flexShrink:0 }}>
            {expanded && <div style={{ padding:"8px 10px", borderRadius:10, background:"rgba(255,255,255,0.03)", display:"flex", alignItems:"center", gap:9, marginBottom:8 }}>
              <div style={{ width:28, height:28, borderRadius:8, background:"linear-gradient(135deg,#E53BF6,#3BF6E5)", padding:1.5, flexShrink:0 }}>
                <div style={{ width:"100%", height:"100%", borderRadius:6, background:"#1e293b", display:"flex", alignItems:"center", justifyContent:"center", fontSize:"0.65rem", fontWeight:800, color:"#f1f5f9" }}>{userName[0]}</div>
              </div>
              <div style={{ flex:1, minWidth:0 }}>
                <p style={{ fontSize:"0.72rem", fontWeight:700, color:"#f1f5f9", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }}>{userName}</p>
                <p style={{ fontSize:"0.52rem", color:"rgba(148,163,184,0.35)" }}>Coordinador</p>
              </div>
            </div>}
            <button onClick={onBack} title={!expanded?"Salir":undefined} style={{ display:"flex", alignItems:"center", gap:9, padding:expanded?"9px 10px":"10px", borderRadius:10, border:"none", cursor:"pointer", background:"rgba(229,59,246,0.06)", borderTop:"1px solid rgba(229,59,246,0.18)", borderRight:"1px solid rgba(229,59,246,0.18)", borderBottom:"1px solid rgba(229,59,246,0.18)", borderLeft:"1px solid rgba(229,59,246,0.18)", color:"#E53BF6", fontFamily:"Poppins, sans-serif", fontSize:"0.72rem", fontWeight:600, justifyContent:expanded?"flex-start":"center", width:"100%" }}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#E53BF6" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
              {expanded && "Cerrar sesión"}
            </button>
          </div>
        </aside>

        {/* ── Main ── */}
        <main style={{ flex:1, display:"flex", flexDirection:"column", overflow:"hidden", position:"relative", zIndex:1, minWidth:0 }}>
          <header style={{ flexShrink:0, display:"flex", alignItems:"center", justifyContent:"space-between", padding:isMobile?"13px 16px":"14px 28px", background:"rgba(7,12,24,0.85)", backdropFilter:"blur(24px)", WebkitBackdropFilter:"blur(24px)", borderBottom:"1px solid rgba(255,255,255,0.06)" }}>
            <div style={{ display:"flex", alignItems:"center", gap:12 }}>
              <div>
                <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif" }}>EVALIA · COORDINADOR</p>
                <h1 style={{ fontSize:isMobile?"1rem":"1.1rem", fontWeight:800, color:"#f1f5f9", letterSpacing:"-0.02em", fontFamily:"Poppins, sans-serif" }}>{sectionLabel()}</h1>
              </div>
            </div>
            <div style={{ display:"flex", alignItems:"center", gap:8 }}>
              {pendingCount>0 && <div style={{ display:"flex", alignItems:"center", gap:6, padding:"5px 11px", borderRadius:99, background:"rgba(229,59,246,0.08)", borderTop:"1px solid rgba(229,59,246,0.25)", borderRight:"1px solid rgba(229,59,246,0.25)", borderBottom:"1px solid rgba(229,59,246,0.25)", borderLeft:"1px solid rgba(229,59,246,0.25)" }}>
                <span style={{ fontSize:"0.6rem", fontWeight:700, color:"#E53BF6", fontFamily:"Poppins, sans-serif" }}>{pendingCount} pendiente{pendingCount!==1?"s":""}</span>
              </div>}
              <div style={{ display:"flex", alignItems:"center", gap:6, padding:"5px 11px", borderRadius:99, background:"rgba(34,197,94,0.08)", borderTop:"1px solid rgba(34,197,94,0.25)", borderRight:"1px solid rgba(34,197,94,0.25)", borderBottom:"1px solid rgba(34,197,94,0.25)", borderLeft:"1px solid rgba(34,197,94,0.25)" }}>
                <span style={{ width:6, height:6, borderRadius:"50%", background:"#22c55e", boxShadow:"0 0 6px #22c55e", display:"inline-block", animation:"cdPulse 2.5s ease-in-out infinite" }}/>
                <span style={{ fontSize:"0.6rem", fontWeight:700, color:"#22c55e", fontFamily:"Poppins, sans-serif" }}>Online</span>
              </div>
              {isMobile && (
                <button onClick={onBack} title="Cerrar sesión" style={{ width:36, height:36, borderRadius:10, background:"rgba(229,59,246,0.08)", border:"1px solid rgba(229,59,246,0.25)", display:"flex", alignItems:"center", justifyContent:"center", cursor:"pointer" }}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#E53BF6" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
                </button>
              )}
            </div>
          </header>

          <div key={section} className="hide-scroll" style={{ flex:1, overflowY:"auto", padding:isMobile?"16px 14px 84px":"24px 28px 36px" }}>
            {section==="inicio"        && <InicioSection onNavigate={navigate} casos={casos}/>}
            {section==="evaluaciones"  && <EvaluacionesSection/>}
            {section==="calendario"    && <CalendarioSection/>}
            {section==="alertas"       && <AlertasSection alertas={alertas} onToast={showToast}/>}
            {section==="denuncias"     && <DenunciasSection denuncias={denuncias} onToast={showToast}/>}
            {section==="asignaciones"  && <AsignacionesSection casos={casos} onOpenCaso={openCaso}/>}
            {section==="asignar-caso"  && selectedCaso && <AsignarCasoSection caso={selectedCaso} onConfirm={handleConfirmAssignment} onBack={()=>navigate("asignaciones")}/>}
            {section==="confirmacion"  && selectedCaso && confirmedTech && <ConfirmacionSection caso={selectedCaso} tecnico={confirmedTech} onGoAsignaciones={()=>navigate("asignaciones")} onGoInicio={()=>navigate("inicio")}/>}
            {section==="reportes"      && <ReportesSection onToast={showToast} onOpenReport={setSignedReport}/>}
            {section==="configuracion" && <ConfiguracionSection userName={userName} onToast={showToast}/>}
          </div>
        </main>

        {/* Bottom tab bar — mobile only */}
        {isMobile && (
          <div style={{ position:"fixed", bottom:0, left:0, right:0, zIndex:60, background:"rgba(7,12,24,0.97)", backdropFilter:"blur(28px)", WebkitBackdropFilter:"blur(28px)", borderTop:"1px solid rgba(255,255,255,0.09)", display:"flex", alignItems:"stretch", height:66, paddingBottom:"env(safe-area-inset-bottom,0px)" }}>
            {([
              { id:"inicio"       as Section, label:"Inicio",      badge:0 },
              { id:"evaluaciones" as Section, label:"Eval.",       badge:3 },
              { id:"asignaciones" as Section, label:"Asignar",     badge:pendingCount },
              { id:"alertas"      as Section, label:"Alertas",     badge:2 },
              { id:"reportes"     as Section, label:"Reportes",    badge:0 },
            ]).map(tab=>{
              const active = sidebarActiveId===tab.id;
              const navItem = NAV.find(n=>n.id===tab.id)!;
              const accent = "#E53BF6";
              return (
                <button key={tab.id} onClick={()=>navigate(tab.id)} style={{ flex:1, display:"flex", flexDirection:"column", alignItems:"center", justifyContent:"center", gap:3, border:"none", cursor:"pointer", background:"transparent", color:active?accent:"rgba(148,163,184,0.4)", position:"relative", paddingTop:4 }}>
                  {active && <div style={{ position:"absolute", top:0, left:"50%", transform:"translateX(-50%)", width:30, height:2.5, borderRadius:"0 0 3px 3px", background:accent, boxShadow:`0 0 8px ${accent}99` }}/>}
                  <div style={{ position:"relative" }}>
                    {navItem.icon(active?accent:"rgba(148,163,184,0.4)")}
                    {tab.badge>0 && !active && <span style={{ position:"absolute", top:-3, right:-5, minWidth:13, height:13, borderRadius:99, background:accent, color:"white", fontSize:"0.4rem", fontWeight:900, display:"flex", alignItems:"center", justifyContent:"center", padding:"0 2px" }}>{tab.badge}</span>}
                  </div>
                  <span style={{ fontSize:"0.5rem", fontWeight:active?700:400, fontFamily:"Poppins, sans-serif", letterSpacing:"0.02em" }}>{tab.label}</span>
                </button>
              );
            })}
          </div>
        )}

        {signedReport && <ReportViewer report={signedReport.report} signature={signedReport.signature} canSign={false} onClose={()=>setSignedReport(null)} onToast={showToast}/>}

        {toast && <div style={{ position:"fixed", bottom:isMobile?74:24, left:"50%", transform:"translateX(-50%)", zIndex:90, display:"flex", alignItems:"center", gap:10, padding:"11px 18px", borderRadius:13, whiteSpace:"nowrap", background:"rgba(8,14,28,0.97)", backdropFilter:"blur(24px)", borderTop:"1px solid rgba(59,246,229,0.4)", borderRight:"1px solid rgba(59,246,229,0.4)", borderBottom:"1px solid rgba(59,246,229,0.4)", borderLeft:"1px solid rgba(59,246,229,0.4)", boxShadow:"0 16px 48px rgba(0,0,0,0.6), 0 0 24px rgba(59,246,229,0.15)", animation:"cdToast 0.3s cubic-bezier(.22,1,.36,1) both" }}>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#3BF6E5" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
          <span style={{ fontSize:"0.78rem", fontWeight:600, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{toast}</span>
        </div>}
      </div>
    </>
  );
}

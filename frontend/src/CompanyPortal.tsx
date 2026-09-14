import { useState, useEffect } from "react";
import { listCompanies } from "./features/companies/api";
import { createAndSubmitRequest, listBpmRequests, type BpmRequest } from "./features/requests/api";
import ReportViewer from "./features/reports/ReportViewer";
import type { ReportRisk, SignedReport } from "./features/reports/report-pdf";

function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

/* ── Types ─────────────────────────────────────────────────── */
type Tab = "dashboard" | "nueva-solicitud" | "solicitudes" | "perfil" | "evaluaciones" | "configuracion";
type SolStatus = "Borrador" | "Pendiente de Asignación" | "En Revisión" | "Aprobado" | "Rechazado";
type Priority  = "Alta" | "Media" | "Baja";

interface Solicitud {
  id: string; titulo: string; tipo: string; motivo: string;
  status: SolStatus; fechaLabel: string;
  tecnico: string | null; prioridad: Priority; progreso: number;
  observaciones?: string;
}

/* ── Data ───────────────────────────────────────────────────── */
const COMPANY_DATA = {
  rnc:        "1-31-01234-5",
  razonSocial:"Constructora García & Asociados, S.A.",
  comercial:  "García Constructores",
  actividad:  "Construcción y Obras Civiles",
  provincia:  "Santo Domingo",
  municipio:  "Distrito Nacional",
  telefono:   "+1 (809) 555-0192",
  correo:     "operaciones@garcia-const.com.do",
  repLegal:   { nombre: "Francisco García Pérez",   cedula:   "001-1234567-8"  },
  repCalidad: { nombre: "Mariela Torres Rodríguez", telefono: "+1 809 555-0208" },
  repContacto:{ nombre: "Luis Mendoza Castillo",    correo:   "l.mendoza@garcia-const.com.do" },
};

function toSolicitud(request: BpmRequest): Solicitud {
  return {
    id: `SOL-${String(request.id).padStart(6, "0")}`,
    titulo: request.reason,
    tipo: request.establishmentType,
    motivo: request.reason,
    status: request.status === "DRAFT" ? "Borrador" : "Pendiente de Asignación",
    fechaLabel: new Date(request.createdAt).toLocaleDateString("es-DO"),
    tecnico: null,
    prioridad: "Media",
    progreso: 0,
    observaciones: request.observations,
  };
}

const TIPOS_ESTABLECIMIENTO = ["Farmacéutico","Alimentos y Bebidas","Cosméticos","Dispositivos Médicos","Industria General","Servicios Sanitarios","Construcción","Otro"];
const MOTIVOS = ["Solicitud Inicial de Licencia","Renovación de Licencia","Ampliación de Operaciones","Cambio de Representante","Actualización de Productos","Inspección Voluntaria","Registro Sanitario","Otro"];

/* ── Color maps ─────────────────────────────────────────────── */
const STATUS: Record<SolStatus, { color:string; bg:string; border:string; dot:string }> = {
  "Borrador":                 { color:"#F6E53B", bg:"rgba(246,229,59,0.08)", border:"rgba(246,229,59,0.3)", dot:"#F6E53B" },
  "Pendiente de Asignación": { color:"rgba(148,163,184,0.8)", bg:"rgba(148,163,184,0.07)", border:"rgba(148,163,184,0.22)", dot:"rgba(148,163,184,0.6)" },
  "En Revisión":             { color:"#3BF6E5", bg:"rgba(59,246,229,0.1)",   border:"rgba(59,246,229,0.35)",   dot:"#3BF6E5"  },
  "Aprobado":                { color:"#22c55e", bg:"rgba(34,197,94,0.1)",    border:"rgba(34,197,94,0.35)",    dot:"#22c55e"  },
  "Rechazado":               { color:"#E53BF6", bg:"rgba(229,59,246,0.1)",   border:"rgba(229,59,246,0.35)",   dot:"#E53BF6"  },
};
const PRI: Record<Priority, { color:string }> = {
  Alta: { color:"#E53BF6" }, Media: { color:"#F6E53B" }, Baja: { color:"#3BF6E5" },
};

/* ── Nav items ──────────────────────────────────────────────── */
const NAV_ITEMS: { id: Tab; label: string; icon: (c:string) => React.ReactNode; badge?: number }[] = [
  { id:"dashboard",    label:"Dashboard",      icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" stroke={c} strokeWidth="1.75"/><polyline points="9,22 9,12 15,12 15,22" stroke={c} strokeWidth="1.75"/></svg> },
  { id:"solicitudes",  label:"Mis Solicitudes",icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><rect x="8" y="2" width="8" height="4" rx="1" stroke={c} strokeWidth="1.5"/><path d="M8 3H6a2 2 0 00-2 2v14a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2h-2" stroke={c} strokeWidth="1.5"/><path d="M9 12h6M9 16h4" stroke={c} strokeWidth="1.5" strokeLinecap="round"/></svg> },
  { id:"evaluaciones", label:"Evaluaciones",   icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M9 11l3 3L22 4" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" stroke={c} strokeWidth="1.75"/></svg> },
  { id:"perfil",       label:"Mi Perfil",      icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" stroke={c} strokeWidth="1.75"/><circle cx="12" cy="7" r="4" stroke={c} strokeWidth="1.75"/></svg> },
  { id:"configuracion",label:"Configuración",  icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="3" stroke={c} strokeWidth="1.75"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" stroke={c} strokeWidth="1.75"/></svg> },
];

/* ── Floating label input ───────────────────────────────────── */
function FloatInput({ id, label, value, onChange, type="text", readOnly=false, mono=false }: {
  id:string; label:string; value:string; onChange:(v:string)=>void;
  type?:string; readOnly?:boolean; mono?:boolean;
}) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;
  return (
    <div style={{ position:"relative" }}>
      <input id={id} type={type} value={value} readOnly={readOnly}
        onFocus={()=>setFocused(true)} onBlur={()=>setFocused(false)}
        onChange={e=>onChange(e.target.value)}
        style={{ width:"100%", padding:"1.25rem 1rem 0.5rem", borderRadius:14, outline:"none", background: focused ? "rgba(229,59,246,0.06)" : readOnly ? "rgba(255,255,255,0.02)" : "rgba(255,255,255,0.04)", borderTop:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, borderRight:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, borderBottom:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, borderLeft:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, boxShadow: focused?"0 0 0 3px rgba(229,59,246,0.09)":"none", color: readOnly?"rgba(59,246,229,0.9)":"#f1f5f9", fontFamily: mono?"'Courier New', monospace":"Poppins, sans-serif", fontSize:"0.875rem", letterSpacing: mono?"0.06em":"normal", boxSizing:"border-box" as const, transition:"all 0.2s ease" }} />
      <label htmlFor={id} style={{ position:"absolute", left:"1rem", top: active?"0.55rem":"50%", transform: active?"none":"translateY(-50%)", fontSize: active?"0.6rem":"0.875rem", fontWeight: active?600:400, color: active?(focused?"rgba(229,59,246,0.8)":"rgba(229,59,246,0.6)"):"rgba(148,163,184,0.5)", letterSpacing: active?"0.07em":"normal", textTransform: active?"uppercase":"none" as const, pointerEvents:"none" as const, transition:"all 0.2s ease", fontFamily:"Poppins, sans-serif" }}>{label}</label>
    </div>
  );
}

/* ── Select input ───────────────────────────────────────────── */
function FloatSelect({ id, label, value, onChange, options }: {
  id:string; label:string; value:string; onChange:(v:string)=>void; options:string[];
}) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;
  return (
    <div style={{ position:"relative" }}>
      <select id={id} value={value} onFocus={()=>setFocused(true)} onBlur={()=>setFocused(false)} onChange={e=>onChange(e.target.value)}
        style={{ width:"100%", padding:"1.25rem 2rem 0.5rem 1rem", borderRadius:14, outline:"none", appearance:"none" as const, background: focused?"rgba(229,59,246,0.06)":"rgba(255,255,255,0.04)", borderTop:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, borderRight:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, borderBottom:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, borderLeft:`1px solid ${focused?"rgba(229,59,246,0.6)":"rgba(255,255,255,0.1)"}`, boxShadow: focused?"0 0 0 3px rgba(229,59,246,0.09)":"none", color: value?"#f1f5f9":"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif", fontSize:"0.875rem", boxSizing:"border-box" as const, transition:"all 0.2s ease", cursor:"pointer" }}>
        <option value="" disabled hidden>—</option>
        {options.map(o=><option key={o} value={o} style={{ background:"#1e293b", color:"#f1f5f9" }}>{o}</option>)}
      </select>
      <label htmlFor={id} style={{ position:"absolute", left:"1rem", top: active?"0.55rem":"50%", transform: active?"none":"translateY(-50%)", fontSize: active?"0.6rem":"0.875rem", fontWeight: active?600:400, color: active?(focused?"rgba(229,59,246,0.8)":"rgba(229,59,246,0.6)"):"rgba(148,163,184,0.5)", letterSpacing: active?"0.07em":"normal", textTransform: active?"uppercase":"none" as const, pointerEvents:"none" as const, transition:"all 0.2s ease", fontFamily:"Poppins, sans-serif" }}>{label}</label>
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" style={{ position:"absolute", right:"14px", top:"50%", transform:"translateY(-50%)", pointerEvents:"none" }}><path d="M6 9l6 6 6-6" stroke="rgba(148,163,184,0.4)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
    </div>
  );
}

/* ── SolicitudCard ──────────────────────────────────────────── */
function SolicitudCard({ sol, onToast, isNew }: { sol:Solicitud; onToast:(m:string)=>void; isNew?:boolean }) {
  const st = STATUS[sol.status];
  const isRejected = sol.status === "Rechazado";
  const isPending  = sol.status === "Pendiente de Asignación";

  const actionLabel = sol.status==="Pendiente de Asignación" ? "Ver estado"
                    : sol.status==="En Revisión"             ? "Seguimiento"
                    : sol.status==="Aprobado"                ? "Descargar PDF"
                    : "Ver observaciones";
  const actionColor = sol.status==="Aprobado" ? "#22c55e" : st.color;

  return (
    <div className={isNew ? "cp-new-entry" : ""} style={{ borderRadius:16, padding:"16px 18px 14px", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(24px)", WebkitBackdropFilter:"blur(24px)", borderTop:`1px solid ${isNew?"rgba(59,246,229,0.3)":isRejected?"rgba(229,59,246,0.15)":"rgba(255,255,255,0.07)"}`, borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:`3px solid ${st.dot}80`, boxShadow: isNew?"0 4px 32px rgba(59,246,229,0.15)":isRejected?"0 4px 24px rgba(229,59,246,0.08)":"0 4px 16px rgba(0,0,0,0.28)", display:"flex", flexDirection:"column", gap:10, position:"relative", overflow:"hidden" }}>
      {isNew && <div style={{ position:"absolute", top:0, left:0, right:0, height:2, background:"linear-gradient(90deg,#3BF6E5,#E53BF6)", opacity:0.7 }} />}

      {/* Header */}
      <div style={{ display:"flex", alignItems:"flex-start", gap:10, justifyContent:"space-between" }}>
        <div style={{ minWidth:0, flex:1 }}>
          <span style={{ fontSize:"0.48rem", fontFamily:"'Courier New', monospace", color:"rgba(148,163,184,0.25)", letterSpacing:"0.07em", display:"block", marginBottom:3 }}>{sol.id}</span>
          <p style={{ fontSize:"0.9rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.01em", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap", marginBottom:2 }}>{sol.titulo}</p>
          <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{sol.tipo} · Motivo: {sol.motivo}</p>
        </div>
        <span style={{ flexShrink:0, display:"inline-flex", alignItems:"center", gap:5, padding:"3px 10px", borderRadius:99, background:st.bg, borderTop:`1px solid ${st.border}`, borderRight:`1px solid ${st.border}`, borderBottom:`1px solid ${st.border}`, borderLeft:`1px solid ${st.border}` }}>
          <span style={{ width:5, height:5, borderRadius:"50%", background:st.dot, flexShrink:0, animation: sol.status==="En Revisión"?"cpPulse 2s ease-in-out infinite":"none" }} />
          <span style={{ fontSize:"0.58rem", fontWeight:700, color:st.color, fontFamily:"Poppins, sans-serif", whiteSpace:"nowrap" }}>{sol.status}</span>
        </span>
      </div>

      {/* Priority + date */}
      <div style={{ display:"flex", alignItems:"center", gap:10 }}>
        <span style={{ fontSize:"0.6rem", fontWeight:700, color:PRI[sol.prioridad].color, fontFamily:"Poppins, sans-serif" }}>↑ {sol.prioridad}</span>
        <span style={{ width:3, height:3, borderRadius:"50%", background:"rgba(255,255,255,0.12)" }} />
        <span style={{ fontSize:"0.6rem", color:"rgba(148,163,184,0.38)", fontFamily:"Poppins, sans-serif" }}>{sol.fechaLabel}</span>
        {sol.tecnico && <>
          <span style={{ width:3, height:3, borderRadius:"50%", background:"rgba(255,255,255,0.12)" }} />
          <span style={{ fontSize:"0.6rem", color:"#22c55e", fontFamily:"Poppins, sans-serif" }}>{sol.tecnico}</span>
        </>}
      </div>

      {/* Progress bar (skip for rejected/pending-assignment) */}
      {!isRejected && !isPending && (
        <div>
          <div style={{ height:3.5, borderRadius:99, background:"rgba(255,255,255,0.05)", overflow:"hidden" }}>
            <div style={{ height:"100%", width:`${sol.progreso}%`, borderRadius:99, background:`linear-gradient(90deg, ${st.dot}88, ${st.dot})`, transition:"width 0.5s ease" }} />
          </div>
          <div style={{ display:"flex", justifyContent:"space-between", marginTop:3 }}>
            <span style={{ fontSize:"0.5rem", color:"rgba(148,163,184,0.25)", fontFamily:"Poppins, sans-serif" }}>Progreso</span>
            <span style={{ fontSize:"0.5rem", color:st.color, fontFamily:"Poppins, sans-serif", fontWeight:600 }}>{sol.progreso}%</span>
          </div>
        </div>
      )}
      {isPending && (
        <div style={{ display:"flex", alignItems:"center", gap:7, padding:"6px 10px", borderRadius:9, background:"rgba(148,163,184,0.05)", borderTop:"1px solid rgba(148,163,184,0.12)", borderRight:"1px solid rgba(148,163,184,0.12)", borderBottom:"1px solid rgba(148,163,184,0.12)", borderLeft:"1px solid rgba(148,163,184,0.12)" }}>
          <svg width="11" height="11" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="10" stroke="rgba(148,163,184,0.4)" strokeWidth="1.75"/><path d="M12 8v4l3 3" stroke="rgba(148,163,184,0.4)" strokeWidth="1.75" strokeLinecap="round"/></svg>
          <span style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>En espera de asignación de técnico evaluador</span>
        </div>
      )}

      {/* Action */}
      <div style={{ display:"flex", gap:8 }}>
        <button onClick={()=>onToast(`${actionLabel} · ${sol.id}`)} style={{ flex:1, padding:"8px 0", borderRadius:10, cursor:"pointer", background:`${actionColor}0d`, borderTop:`1px solid ${actionColor}40`, borderRight:`1px solid ${actionColor}40`, borderBottom:`1px solid ${actionColor}40`, borderLeft:`1px solid ${actionColor}40`, color:actionColor, fontSize:"0.68rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>
          {actionLabel}
        </button>
        <button style={{ width:34, height:34, borderRadius:10, background:"rgba(255,255,255,0.03)", borderTop:"1px solid rgba(255,255,255,0.08)", borderRight:"1px solid rgba(255,255,255,0.08)", borderBottom:"1px solid rgba(255,255,255,0.08)", borderLeft:"1px solid rgba(255,255,255,0.08)", color:"rgba(148,163,184,0.35)", cursor:"pointer", display:"flex", alignItems:"center", justifyContent:"center", flexShrink:0 }}>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke="currentColor" strokeWidth="1.75"/><circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="1.75"/></svg>
        </button>
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────── */
/* ── Section: Dashboard Empresa ──────────────────────────── */
/* ──────────────────────────────────────────────────────────── */
function DashboardSection({ solicitudes, onNavigate, onToast }: {
  solicitudes: Solicitud[];
  onNavigate: (t:Tab) => void;
  onToast: (m:string) => void;
}) {
  const pending  = solicitudes.filter(s=>s.status==="Pendiente de Asignación").length;
  const review   = solicitudes.filter(s=>s.status==="En Revisión").length;
  const approved = solicitudes.filter(s=>s.status==="Aprobado").length;

  const hora = new Date().getHours();
  const greeting = hora < 12 ? "Buenos días" : hora < 19 ? "Buenas tardes" : "Buenas noches";

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:24 }}>
      {/* Welcome */}
      <div>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:6 }}>EVALIA · PORTAL EMPRESA</p>
        <h1 style={{ fontSize:"1.4rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em", marginBottom:4 }}>{greeting}, García Constructores</h1>
        <p style={{ fontSize:"0.78rem", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif" }}>Gestiona tus solicitudes BPM y evaluaciones desde aquí.</p>
      </div>

      {/* Primary CTA — Nueva Solicitud BPM */}
      <button onClick={()=>onNavigate("nueva-solicitud")} style={{ width:"100%", padding:"20px 24px", borderRadius:18, cursor:"pointer", background:"transparent", borderTop:"1.5px dashed rgba(229,59,246,0.5)", borderRight:"1.5px dashed rgba(229,59,246,0.5)", borderBottom:"1.5px dashed rgba(229,59,246,0.5)", borderLeft:"1.5px dashed rgba(229,59,246,0.5)", display:"flex", alignItems:"center", gap:16, position:"relative", overflow:"hidden", transition:"all 0.2s ease" }}>
        <div style={{ position:"absolute", inset:0, background:"linear-gradient(135deg, rgba(229,59,246,0.06), rgba(59,246,229,0.04))", pointerEvents:"none" }} />
        <div style={{ width:48, height:48, borderRadius:14, background:"linear-gradient(135deg, #E53BF6, #8b5cf6, #3BF6E5)", display:"flex", alignItems:"center", justifyContent:"center", boxShadow:"0 8px 24px rgba(229,59,246,0.45)", flexShrink:0 }}>
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none"><path d="M12 5v14M5 12h14" stroke="white" strokeWidth="2.5" strokeLinecap="round"/></svg>
        </div>
        <div style={{ textAlign:"left", flex:1 }}>
          <p style={{ fontSize:"1.05rem", fontWeight:800, background:"linear-gradient(135deg, #E53BF6, #3BF6E5)", WebkitBackgroundClip:"text", WebkitTextFillColor:"transparent", backgroundClip:"text", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.01em" }}>+ Nueva Solicitud BPM</p>
          <p style={{ fontSize:"0.68rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>Inspecciones · Registros · Auditorías · Certificaciones</p>
        </div>
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" style={{ flexShrink:0 }}><path d="M9 18l6-6-6-6" stroke="rgba(229,59,246,0.5)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
      </button>

      {/* Quick-access grid */}
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(200px, 1fr))", gap:12 }}>
        {/* Mis Solicitudes */}
        <button onClick={()=>onNavigate("solicitudes")} style={{ padding:"18px 20px", borderRadius:16, cursor:"pointer", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(20px)", WebkitBackdropFilter:"blur(20px)", borderTop:"1px solid rgba(59,246,229,0.18)", borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:"2px solid rgba(59,246,229,0.5)", textAlign:"left", transition:"all 0.15s ease" }}>
          <div style={{ display:"flex", alignItems:"center", justifyContent:"space-between", marginBottom:12 }}>
            <div style={{ width:36, height:36, borderRadius:11, background:"rgba(59,246,229,0.12)", borderTop:"1px solid rgba(59,246,229,0.3)", borderRight:"1px solid rgba(59,246,229,0.3)", borderBottom:"1px solid rgba(59,246,229,0.3)", borderLeft:"1px solid rgba(59,246,229,0.3)", display:"flex", alignItems:"center", justifyContent:"center" }}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><rect x="8" y="2" width="8" height="4" rx="1" stroke="#3BF6E5" strokeWidth="1.5"/><path d="M8 3H6a2 2 0 00-2 2v14a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2h-2" stroke="#3BF6E5" strokeWidth="1.5"/><path d="M9 12h6M9 16h4" stroke="#3BF6E5" strokeWidth="1.5" strokeLinecap="round"/></svg>
            </div>
            <span style={{ fontSize:"1.8rem", fontWeight:900, color:"#3BF6E5", fontFamily:"Poppins, sans-serif", textShadow:"0 0 20px rgba(59,246,229,0.5)" }}>{solicitudes.length}</span>
          </div>
          <p style={{ fontSize:"0.82rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", marginBottom:2 }}>Mis Solicitudes</p>
          <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{pending} pendiente{pending!==1?"s":""} de asignación</p>
        </button>

        {/* Evaluaciones */}
        <button onClick={()=>onNavigate("evaluaciones")} style={{ padding:"18px 20px", borderRadius:16, cursor:"pointer", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(20px)", WebkitBackdropFilter:"blur(20px)", borderTop:"1px solid rgba(34,197,94,0.15)", borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:"2px solid rgba(34,197,94,0.5)", textAlign:"left" }}>
          <div style={{ display:"flex", alignItems:"center", justifyContent:"space-between", marginBottom:12 }}>
            <div style={{ width:36, height:36, borderRadius:11, background:"rgba(34,197,94,0.12)", borderTop:"1px solid rgba(34,197,94,0.3)", borderRight:"1px solid rgba(34,197,94,0.3)", borderBottom:"1px solid rgba(34,197,94,0.3)", borderLeft:"1px solid rgba(34,197,94,0.3)", display:"flex", alignItems:"center", justifyContent:"center" }}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 11l3 3L22 4" stroke="#22c55e" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" stroke="#22c55e" strokeWidth="1.75"/></svg>
            </div>
            <span style={{ fontSize:"1.8rem", fontWeight:900, color:"#22c55e", fontFamily:"Poppins, sans-serif", textShadow:"0 0 20px rgba(34,197,94,0.5)" }}>6</span>
          </div>
          <p style={{ fontSize:"0.82rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", marginBottom:2 }}>Evaluaciones</p>
          <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{approved} aprobada{approved!==1?"s":""}</p>
        </button>

        {/* Notificaciones */}
        <button onClick={()=>onToast("No hay notificaciones nuevas")} style={{ padding:"18px 20px", borderRadius:16, cursor:"pointer", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(20px)", WebkitBackdropFilter:"blur(20px)", borderTop:"1px solid rgba(246,229,59,0.15)", borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:"2px solid rgba(246,229,59,0.5)", textAlign:"left" }}>
          <div style={{ display:"flex", alignItems:"center", justifyContent:"space-between", marginBottom:12 }}>
            <div style={{ width:36, height:36, borderRadius:11, background:"rgba(246,229,59,0.12)", borderTop:"1px solid rgba(246,229,59,0.3)", borderRight:"1px solid rgba(246,229,59,0.3)", borderBottom:"1px solid rgba(246,229,59,0.3)", borderLeft:"1px solid rgba(246,229,59,0.3)", display:"flex", alignItems:"center", justifyContent:"center" }}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 01-3.46 0" stroke="#F6E53B" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
            </div>
            <span style={{ fontSize:"1.8rem", fontWeight:900, color:"#F6E53B", fontFamily:"Poppins, sans-serif", textShadow:"0 0 20px rgba(246,229,59,0.5)" }}>2</span>
          </div>
          <p style={{ fontSize:"0.82rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", marginBottom:2 }}>Notificaciones</p>
          <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>2 sin leer</p>
        </button>
      </div>

      {/* Recent solicitudes preview */}
      <div>
        <div style={{ display:"flex", alignItems:"center", justifyContent:"space-between", marginBottom:12 }}>
          <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(148,163,184,0.3)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif" }}>SOLICITUDES RECIENTES</p>
          <button onClick={()=>onNavigate("solicitudes")} style={{ fontSize:"0.65rem", fontWeight:600, color:"rgba(59,246,229,0.6)", fontFamily:"Poppins, sans-serif", background:"none", border:"none", cursor:"pointer" }}>Ver todas →</button>
        </div>
        <div style={{ display:"flex", flexDirection:"column", gap:8 }}>
          {solicitudes.slice(0,3).map((sol,i)=>{
            const st=STATUS[sol.status];
            return (
              <div key={sol.id} style={{ display:"flex", alignItems:"center", gap:12, padding:"10px 14px", borderRadius:13, background:"rgba(10,18,36,0.7)", backdropFilter:"blur(16px)", WebkitBackdropFilter:"blur(16px)", borderTop:"1px solid rgba(255,255,255,0.06)", borderRight:"1px solid rgba(255,255,255,0.04)", borderBottom:"1px solid rgba(255,255,255,0.04)", borderLeft:`2px solid ${st.dot}60` }}>
                <div style={{ flex:1, minWidth:0 }}>
                  <p style={{ fontSize:"0.8rem", fontWeight:600, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }}>{sol.titulo}</p>
                  <p style={{ fontSize:"0.58rem", color:"rgba(148,163,184,0.35)", fontFamily:"Poppins, sans-serif" }}>{sol.tipo} · {sol.fechaLabel}</p>
                </div>
                <span style={{ flexShrink:0, padding:"2px 9px", borderRadius:99, background:st.bg, borderTop:`1px solid ${st.border}`, borderRight:`1px solid ${st.border}`, borderBottom:`1px solid ${st.border}`, borderLeft:`1px solid ${st.border}`, fontSize:"0.58rem", fontWeight:700, color:st.color, fontFamily:"Poppins, sans-serif", whiteSpace:"nowrap" }}>{sol.status}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────── */
/* ── Section: Nueva Solicitud BPM (form) ─────────────────── */
/* ──────────────────────────────────────────────────────────── */
function NuevaSolicitudSection({ onSubmit, onCancel }: {
  onSubmit: (sol: Solicitud) => void;
  onCancel: () => void;
}) {
  const [tipoEstab,   setTipoEstab]   = useState("");
  const [motivo,      setMotivo]      = useState("");
  const [observ,      setObserv]      = useState("");
  const [prioridad,   setPrioridad]   = useState<Priority>("Media");
  const [submitting,  setSubmitting]  = useState(false);
  const [hasDraft,    setHasDraft]    = useState(false);

  const canSubmit = tipoEstab && motivo;

  const handleSubmit = () => {
    if (!canSubmit) return;
    setSubmitting(true);
    setTimeout(() => {
      const nextId = `SOL-2026-${String(Math.floor(43 + Math.random() * 10)).padStart(4,"0")}`;
      const titulo = motivo === "Otro" ? (observ.split(" ").slice(0,4).join(" ") || "Nueva Solicitud") : motivo;
      const newSol: Solicitud = {
        id: nextId,
        titulo,
        tipo: tipoEstab,
        motivo,
        status: "Pendiente de Asignación",
        fechaLabel: "Ahora",
        tecnico: null,
        prioridad,
        progreso: 0,
        observaciones: observ,
      };
      onSubmit(newSol);
    }, 1600);
  };

  const handleDraft = () => {
    setHasDraft(true);
    setTimeout(() => setHasDraft(false), 2000);
  };

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:0, maxWidth:660 }}>
      {/* Back */}
      <button onClick={onCancel} style={{ display:"inline-flex", alignItems:"center", gap:8, marginBottom:20, background:"none", border:"none", cursor:"pointer", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", fontSize:"0.75rem", fontWeight:500, padding:0 }}>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M19 12H5M12 19l-7-7 7-7" stroke="rgba(148,163,184,0.5)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
        Volver al Dashboard
      </button>

      {/* Header */}
      <div style={{ marginBottom:24 }}>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:6 }}>EVALIA · PORTAL EMPRESA</p>
        <h1 style={{ fontSize:"1.3rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em", marginBottom:4 }}>Nueva Solicitud BPM</h1>
        <p style={{ fontSize:"0.75rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>Completa la información para iniciar el proceso de inspección.</p>
      </div>

      {/* Glass form card */}
      <div style={{ borderRadius:22, background:"rgba(10,18,36,0.82)", backdropFilter:"blur(32px) saturate(160%)", WebkitBackdropFilter:"blur(32px) saturate(160%)", borderTop:"1px solid rgba(229,59,246,0.2)", borderRight:"1px solid rgba(255,255,255,0.07)", borderBottom:"1px solid rgba(255,255,255,0.07)", borderLeft:"1px solid rgba(255,255,255,0.07)", overflow:"hidden", marginBottom:12 }}>
        <div style={{ height:3, background:"linear-gradient(90deg, #E53BF6, #8b5cf6, #3BF6E5)" }} />
        <div style={{ padding:"24px 24px 20px", display:"flex", flexDirection:"column", gap:14 }}>

          {/* Empresa — auto-filled */}
          <div>
            <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif", marginBottom:8 }}>DATOS DE LA EMPRESA</p>
            <FloatInput id="empresa" label="Empresa" value={COMPANY_DATA.razonSocial} onChange={()=>{}} readOnly />
            <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(180px, 1fr))", gap:12, marginTop:12 }}>
              <FloatInput id="rnc-form" label="RNC" value={COMPANY_DATA.rnc} onChange={()=>{}} readOnly mono />
              <FloatInput id="actividad-form" label="Actividad" value={COMPANY_DATA.actividad} onChange={()=>{}} readOnly />
            </div>
          </div>

          <div style={{ height:1, background:"rgba(255,255,255,0.05)" }} />

          {/* Request fields */}
          <div>
            <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif", marginBottom:8 }}>DETALLES DE LA SOLICITUD</p>
            <div style={{ display:"flex", flexDirection:"column", gap:12 }}>
              <FloatSelect id="tipo-estab" label="Tipo de establecimiento" value={tipoEstab} onChange={setTipoEstab} options={TIPOS_ESTABLECIMIENTO} />
              <FloatSelect id="motivo" label="Motivo de la solicitud" value={motivo} onChange={setMotivo} options={MOTIVOS} />
            </div>
          </div>

          {/* Priority */}
          <div>
            <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif", marginBottom:8 }}>PRIORIDAD</p>
            <div style={{ display:"flex", gap:8 }}>
              {(["Alta","Media","Baja"] as Priority[]).map(p=>{
                const active = prioridad===p; const c=PRI[p].color;
                return (
                  <button key={p} onClick={()=>setPrioridad(p)} style={{ flex:1, padding:"10px 0", borderRadius:12, fontSize:"0.75rem", fontWeight:700, fontFamily:"Poppins, sans-serif", cursor:"pointer", transition:"all 0.15s ease", background: active?`${c}18`:"rgba(255,255,255,0.04)", borderTop: active?`1px solid ${c}55`:"1px solid rgba(255,255,255,0.1)", borderRight: active?`1px solid ${c}55`:"1px solid rgba(255,255,255,0.1)", borderBottom: active?`1px solid ${c}55`:"1px solid rgba(255,255,255,0.1)", borderLeft: active?`1px solid ${c}55`:"1px solid rgba(255,255,255,0.1)", color: active?c:"rgba(148,163,184,0.45)", boxShadow: active?`0 0 14px ${c}25`:"none" }}>{p}</button>
                );
              })}
            </div>
          </div>

          {/* Observaciones */}
          <div>
            <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif", marginBottom:8 }}>OBSERVACIONES</p>
            <textarea value={observ} onChange={e=>setObserv(e.target.value)} placeholder="Describe detalles adicionales, áreas de la planta a evaluar, antecedentes relevantes…" rows={4}
              style={{ width:"100%", padding:"14px", borderRadius:14, resize:"vertical" as const, outline:"none", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.1)", borderRight:"1px solid rgba(255,255,255,0.1)", borderBottom:"1px solid rgba(255,255,255,0.1)", borderLeft:"1px solid rgba(255,255,255,0.1)", color:"#f1f5f9", fontFamily:"Poppins, sans-serif", fontSize:"0.82rem", boxSizing:"border-box" as const, transition:"border-color 0.2s ease" }} />
          </div>

          {/* Documentación placeholder */}
          <div>
            <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif", marginBottom:8 }}>DOCUMENTACIÓN OBLIGATORIA</p>
            <div style={{ borderRadius:14, padding:"20px", borderTop:"1.5px dashed rgba(255,255,255,0.12)", borderRight:"1.5px dashed rgba(255,255,255,0.12)", borderBottom:"1.5px dashed rgba(255,255,255,0.12)", borderLeft:"1.5px dashed rgba(255,255,255,0.12)", background:"rgba(255,255,255,0.02)", display:"flex", flexDirection:"column", alignItems:"center", gap:8, cursor:"pointer" }}>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M17 8l-5-5-5 5M12 3v12" stroke="rgba(148,163,184,0.3)" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
              <p style={{ fontSize:"0.75rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif", textAlign:"center" }}>Adjuntar documentos<br/><span style={{ fontSize:"0.62rem", opacity:0.6 }}>PDF, JPG, PNG · máx. 10 MB</span></p>
            </div>
          </div>
        </div>
      </div>

      {/* Action buttons */}
      <div style={{ display:"flex", gap:10 }}>
        <button onClick={handleDraft} style={{ flex:1, padding:"14px 0", borderRadius:14, cursor:"pointer", background: hasDraft?"rgba(34,197,94,0.08)":"rgba(255,255,255,0.04)", borderTop: hasDraft?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(255,255,255,0.12)", borderRight: hasDraft?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(255,255,255,0.12)", borderBottom: hasDraft?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(255,255,255,0.12)", borderLeft: hasDraft?"1px solid rgba(34,197,94,0.35)":"1px solid rgba(255,255,255,0.12)", color: hasDraft?"#22c55e":"rgba(148,163,184,0.5)", fontSize:"0.85rem", fontWeight:600, fontFamily:"Poppins, sans-serif", transition:"all 0.2s ease" }}>
          {hasDraft ? "✓ Borrador guardado" : "Guardar borrador"}
        </button>
        <button onClick={handleSubmit} disabled={!canSubmit || submitting} style={{ flex:2, padding:"14px 0", borderRadius:14, border:"none", cursor: canSubmit&&!submitting?"pointer":"not-allowed", fontWeight:800, fontSize:"0.95rem", fontFamily:"Poppins, sans-serif", letterSpacing:"0.03em", transition:"all 0.2s ease", background: canSubmit?"linear-gradient(135deg, #E53BF6, #8b5cf6, #3BF6E5)":"rgba(255,255,255,0.06)", color: canSubmit?"white":"rgba(148,163,184,0.3)", boxShadow: canSubmit?"0 8px 28px rgba(229,59,246,0.4)":"none", opacity: submitting?0.8:1, display:"flex", alignItems:"center", justifyContent:"center", gap:8 }}>
          {submitting
            ? <><span style={{ width:16, height:16, borderRadius:"50%", borderTop:"2px solid white", borderRight:"2px solid rgba(255,255,255,0.25)", borderBottom:"2px solid rgba(255,255,255,0.25)", borderLeft:"2px solid rgba(255,255,255,0.25)", animation:"cpSpin 0.7s linear infinite", display:"inline-block" }}/> Enviando…</>
            : <><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg> Enviar Solicitud</>}
        </button>
      </div>

      {/* Validation hint */}
      {!canSubmit && (
        <p style={{ textAlign:"center", fontSize:"0.65rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif", marginTop:6 }}>Completa el tipo de establecimiento y el motivo para enviar.</p>
      )}
    </div>
  );
}

/* ──────────────────────────────────────────────────────────── */
/* ── Section: Mis Solicitudes ─────────────────────────────── */
/* ──────────────────────────────────────────────────────────── */
function SolicitudesSection({ solicitudes, newId, onToast, onNavigate }: {
  solicitudes: Solicitud[];
  newId: string | null;
  onToast: (m:string) => void;
  onNavigate: (t:Tab) => void;
}) {
  const [filter, setFilter] = useState<SolStatus|"Todas">("Todas");
  const filtered = filter==="Todas" ? solicitudes : solicitudes.filter(s=>s.status===filter);

  const counts = {
    "Todas": solicitudes.length,
    "Pendiente de Asignación": solicitudes.filter(s=>s.status==="Pendiente de Asignación").length,
    "En Revisión":             solicitudes.filter(s=>s.status==="En Revisión").length,
    "Aprobado":                solicitudes.filter(s=>s.status==="Aprobado").length,
    "Rechazado":               solicitudes.filter(s=>s.status==="Rechazado").length,
  };

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16, maxWidth:860 }}>
      {/* Header */}
      <div style={{ display:"flex", alignItems:"flex-end", justifyContent:"space-between", gap:12 }}>
        <div>
          <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>EVALIA · PORTAL EMPRESA</p>
          <h2 style={{ fontSize:"1.1rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em" }}>Mis Solicitudes</h2>
          <p style={{ fontSize:"0.72rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif", marginTop:2 }}>{solicitudes.length} solicitudes registradas</p>
        </div>
        <button onClick={()=>onNavigate("nueva-solicitud")} style={{ flexShrink:0, display:"flex", alignItems:"center", gap:7, padding:"9px 16px", borderRadius:12, cursor:"pointer", background:"linear-gradient(135deg, #E53BF6, #8b5cf6)", border:"none", color:"white", fontSize:"0.72rem", fontWeight:700, fontFamily:"Poppins, sans-serif", boxShadow:"0 4px 16px rgba(229,59,246,0.35)" }}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><path d="M12 5v14M5 12h14" stroke="white" strokeWidth="2.5" strokeLinecap="round"/></svg>
          Nueva Solicitud
        </button>
      </div>

      {/* Summary stats */}
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(130px, 1fr))", gap:9 }}>
        {(["Pendiente de Asignación","En Revisión","Aprobado","Rechazado"] as SolStatus[]).map(s=>{
          const st=STATUS[s];
          return (
            <button key={s} onClick={()=>setFilter(f=>f===s?"Todas":s)} style={{ padding:"11px 12px", borderRadius:13, textAlign:"left", cursor:"pointer", background: filter===s?`${st.dot}14`:"rgba(10,18,36,0.7)", borderTop:`1px solid ${filter===s?st.border:"rgba(255,255,255,0.06)"}`, borderRight:`1px solid ${filter===s?st.border:"rgba(255,255,255,0.04)"}`, borderBottom:`1px solid ${filter===s?st.border:"rgba(255,255,255,0.04)"}`, borderLeft:`2px solid ${filter===s?st.dot+"80":"rgba(255,255,255,0.08)"}` }}>
              <p style={{ fontSize:"1.5rem", fontWeight:900, color:st.color, fontFamily:"Poppins, sans-serif", lineHeight:1, textShadow:`0 0 14px ${st.dot}40` }}>{counts[s]}</p>
              <p style={{ fontSize:"0.53rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif", fontWeight:600, textTransform:"uppercase", letterSpacing:"0.07em", marginTop:3, lineHeight:1.3 }}>{s}</p>
            </button>
          );
        })}
      </div>

      {/* Filter chips */}
      <div className="hide-scroll" style={{ display:"flex", gap:7, overflowX:"auto" }}>
        {(["Todas","Pendiente de Asignación","En Revisión","Aprobado","Rechazado"] as const).map(f=>{
          const active=filter===f;
          const c=f==="Todas"?"rgba(148,163,184,0.7)":STATUS[f as SolStatus].color;
          return (
            <button key={f} onClick={()=>setFilter(f)} style={{ flexShrink:0, padding:"5px 14px", borderRadius:99, fontSize:"0.65rem", fontWeight:600, fontFamily:"Poppins, sans-serif", cursor:"pointer", background:active?`${c}18`:"rgba(255,255,255,0.04)", borderTop:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderRight:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderBottom:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderLeft:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", color:active?c:"rgba(148,163,184,0.45)" }}>{f}</button>
          );
        })}
      </div>

      {/* Cards grid */}
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(300px,1fr))", gap:12 }}>
        {filtered.length > 0
          ? filtered.map(sol=><SolicitudCard key={sol.id} sol={sol} onToast={onToast} isNew={sol.id===newId}/>)
          : <div style={{ gridColumn:"1/-1", textAlign:"center", padding:"48px 0" }}><p style={{ fontSize:"0.88rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif" }}>Sin solicitudes en esta categoría</p></div>
        }
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────── */
/* ── Section: Perfil ──────────────────────────────────────── */
/* ──────────────────────────────────────────────────────────── */
function PerfilSection({ isMobile, onToast }: { isMobile:boolean; onToast:(m:string)=>void }) {
  const [rnc,       setRnc]       = useState(COMPANY_DATA.rnc);
  const [razon,     setRazon]     = useState(COMPANY_DATA.razonSocial);
  const [comercial, setComercial] = useState(COMPANY_DATA.comercial);
  const [actividad, setActividad] = useState(COMPANY_DATA.actividad);
  const [provincia, setProvincia] = useState(COMPANY_DATA.provincia);
  const [municipio, setMunicipio] = useState(COMPANY_DATA.municipio);
  const [telefono,  setTelefono]  = useState(COMPANY_DATA.telefono);
  const [correo,    setCorreo]    = useState(COMPANY_DATA.correo);
  const [repLName,  setRepLName]  = useState(COMPANY_DATA.repLegal.nombre);
  const [repLCed,   setRepLCed]   = useState(COMPANY_DATA.repLegal.cedula);
  const [repCName,  setRepCName]  = useState(COMPANY_DATA.repCalidad.nombre);
  const [repCTel,   setRepCTel]   = useState(COMPANY_DATA.repCalidad.telefono);
  const [repKName,  setRepKName]  = useState(COMPANY_DATA.repContacto.nombre);
  const [repKMail,  setRepKMail]  = useState(COMPANY_DATA.repContacto.correo);
  const [saving,    setSaving]    = useState(false);
  const [saved,     setSaved]     = useState(false);

  const handleSave = () => {
    setSaving(true); setSaved(false);
    setTimeout(()=>{ setSaving(false); setSaved(true); onToast("Cambios guardados correctamente"); setTimeout(()=>setSaved(false),2500); },1600);
  };

  const RepCard = ({ role, accent, nameVal, nameSet, fieldLabel, fieldVal, fieldSet }: { role:string; accent:string; nameVal:string; nameSet:(v:string)=>void; fieldLabel:string; fieldVal:string; fieldSet:(v:string)=>void }) => (
    <div style={{ borderRadius:14, padding:"14px 14px 12px", background:"rgba(12,20,40,0.7)", backdropFilter:"blur(16px)", WebkitBackdropFilter:"blur(16px)", borderTop:`1px solid rgba(255,255,255,0.07)`, borderRight:`1px solid rgba(255,255,255,0.05)`, borderBottom:`1px solid rgba(255,255,255,0.05)`, borderLeft:`3px solid ${accent}66` }}>
      <p style={{ fontSize:"0.6rem", fontWeight:700, color:accent, fontFamily:"Poppins, sans-serif", letterSpacing:"0.08em", textTransform:"uppercase", marginBottom:10 }}>{role}</p>
      <div style={{ display:"flex", flexDirection:"column", gap:9 }}>
        <FloatInput id={`${role}-name`} label="Nombre completo" value={nameVal} onChange={nameSet} />
        <FloatInput id={`${role}-field`} label={fieldLabel} value={fieldVal} onChange={fieldSet} />
      </div>
    </div>
  );

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16, maxWidth:820 }}>
      <div style={{ marginBottom:4 }}>
        <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>EVALIA · PORTAL EMPRESA</p>
        <h2 style={{ fontSize:"1.1rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em" }}>Perfil de Empresa</h2>
      </div>
      <div style={{ borderRadius:20, overflow:"hidden", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(32px) saturate(160%)", WebkitBackdropFilter:"blur(32px) saturate(160%)", borderTop:"1px solid rgba(59,246,229,0.18)", borderRight:"1px solid rgba(255,255,255,0.06)", borderBottom:"1px solid rgba(255,255,255,0.06)", borderLeft:"1px solid rgba(255,255,255,0.06)" }}>
        <div style={{ height:2.5, background:"linear-gradient(90deg,#3BF6E5,#8b5cf6,#E53BF6)" }} />
        <div style={{ padding: isMobile?"18px 16px":"24px 26px" }}>
          <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>DATOS DEL ESTABLECIMIENTO</p>
          <div style={{ display:"grid", gridTemplateColumns: isMobile?"1fr":"1fr 1fr", gap:12 }}>
            <div style={{ gridColumn: isMobile?"1":"1/-1" }}><FloatInput id="rnc-p" label="RNC" value={rnc} onChange={setRnc} readOnly mono /></div>
            <FloatInput id="razon-p" label="Razón Social" value={razon} onChange={setRazon} />
            <FloatInput id="comercial-p" label="Nombre Comercial" value={comercial} onChange={setComercial} />
            <div style={{ gridColumn: isMobile?"1":"1/-1" }}><FloatInput id="actividad-p" label="Actividad Económica" value={actividad} onChange={setActividad} /></div>
            <FloatInput id="provincia-p" label="Provincia" value={provincia} onChange={setProvincia} />
            <FloatInput id="municipio-p" label="Municipio" value={municipio} onChange={setMunicipio} />
            <FloatInput id="telefono-p" label="Teléfono" value={telefono} onChange={setTelefono} />
            <FloatInput id="correo-p" label="Correo" value={correo} onChange={setCorreo} type="email" />
          </div>
        </div>
      </div>
      <div style={{ borderRadius:20, overflow:"hidden", background:"rgba(10,18,36,0.78)", backdropFilter:"blur(28px)", WebkitBackdropFilter:"blur(28px)", borderTop:"1px solid rgba(229,59,246,0.15)", borderRight:"1px solid rgba(255,255,255,0.06)", borderBottom:"1px solid rgba(255,255,255,0.06)", borderLeft:"1px solid rgba(255,255,255,0.06)" }}>
        <div style={{ height:2.5, background:"linear-gradient(90deg,#E53BF6,#8b5cf6,#3BF6E5)" }} />
        <div style={{ padding: isMobile?"18px 16px":"24px 26px" }}>
          <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(148,163,184,0.3)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>REPRESENTANTES</p>
          <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(220px, 1fr))", gap:12 }}>
            <RepCard role="Representante Legal" accent="#E53BF6" nameVal={repLName} nameSet={setRepLName} fieldLabel="Cédula" fieldVal={repLCed} fieldSet={setRepLCed} />
            <RepCard role="Rep. Calidad" accent="#F6E53B" nameVal={repCName} nameSet={setRepCName} fieldLabel="Teléfono" fieldVal={repCTel} fieldSet={setRepCTel} />
            <RepCard role="Contacto Principal" accent="#3BF6E5" nameVal={repKName} nameSet={setRepKName} fieldLabel="Correo" fieldVal={repKMail} fieldSet={setRepKMail} />
          </div>
        </div>
      </div>
      <button onClick={handleSave} disabled={saving} style={{ width:"100%", height:52, borderRadius:14, border:"none", cursor:saving?"not-allowed":"pointer", fontWeight:800, fontSize:"0.95rem", fontFamily:"Poppins, sans-serif", background: saved?"rgba(34,197,94,0.12)":"linear-gradient(135deg,#3BF6E5,#06b6d4,#8b5cf6)", color: saved?"#22c55e":"#0F172A", boxShadow: saved?"0 8px 24px rgba(34,197,94,0.2)":"0 8px 28px rgba(59,246,229,0.3)", borderTop: saved?"1px solid rgba(34,197,94,0.35)":"none", borderRight: saved?"1px solid rgba(34,197,94,0.35)":"none", borderBottom: saved?"1px solid rgba(34,197,94,0.35)":"none", borderLeft: saved?"1px solid rgba(34,197,94,0.35)":"none", display:"flex", alignItems:"center", justifyContent:"center", gap:10, transition:"all 0.25s ease" }}>
        {saving ? <><span style={{ width:16,height:16,borderRadius:"50%",borderTop:"2px solid #0F172A",borderRight:"2px solid rgba(0,0,0,0.2)",borderBottom:"2px solid rgba(0,0,0,0.2)",borderLeft:"2px solid rgba(0,0,0,0.2)",animation:"cpSpin 0.7s linear infinite",display:"inline-block" }}/>Guardando…</>
        : saved ? <>✓ Cambios guardados</> : <>Guardar Cambios</>}
      </button>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────── */
/* ── Section: Evaluaciones (historial) ───────────────────── */
/* ──────────────────────────────────────────────────────────── */
function EvaluacionesSection({ onOpenReport }: { onOpenReport:(r:SignedReport)=>void }) {
  const EVS = [
    { id:"EBR-2026-0087", tipo:"Auditoría Calidad",    fecha:"28 ago 2026", tecnico:"Ing. R. Méndez",    estado:"Aprobado",    riesgo:"Alto",     color:"#22c55e" },
    { id:"EBR-2026-0083", tipo:"Evaluación General",   fecha:"22 ago 2026", tecnico:"Ing. M. Santos",    estado:"En Revisión", riesgo:"Crítico",  color:"#8b5cf6" },
    { id:"EBR-2026-0071", tipo:"Inspección BPM",       fecha:"10 ago 2026", tecnico:"Lic. C. Vargas",    estado:"Aprobado",    riesgo:"Moderado", color:"#22c55e" },
    { id:"EBR-2026-0065", tipo:"Inspección Eléctrica", fecha:"02 ago 2026", tecnico:"Lic. A. Fernández", estado:"Aprobado",    riesgo:"Bajo",     color:"#22c55e" },
    { id:"EBR-2026-0051", tipo:"Auditoría Calidad",    fecha:"15 jul 2026", tecnico:"Ing. R. Méndez",    estado:"Rechazado",   riesgo:"Alto",     color:"#E53BF6" },
    { id:"EBR-2026-0040", tipo:"Inspección General",   fecha:"01 jul 2026", tecnico:"Ing. M. Santos",    estado:"Aprobado",    riesgo:"Moderado", color:"#22c55e" },
  ];
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16, maxWidth:820 }}>
      <div style={{ marginBottom:4 }}>
        <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>EVALIA · PORTAL EMPRESA</p>
        <h2 style={{ fontSize:"1.1rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em" }}>Historial de Evaluaciones</h2>
      </div>
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(120px, 1fr))", gap:9 }}>
        {[{label:"Total",v:6,c:"rgba(148,163,184,0.6)"},{label:"Aprobadas",v:4,c:"#22c55e"},{label:"En Revisión",v:1,c:"#8b5cf6"},{label:"Rechazadas",v:1,c:"#E53BF6"}].map(s=>(
          <div key={s.label} style={{ borderRadius:13, padding:"12px 14px", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(20px)", WebkitBackdropFilter:"blur(20px)", borderTop:`1px solid ${s.c}18`, borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:`2px solid ${s.c}55` }}>
            <p style={{ fontSize:"1.6rem", fontWeight:900, color:s.c, fontFamily:"Poppins, sans-serif", lineHeight:1, textShadow:`0 0 16px ${s.c}40` }}>{s.v}</p>
            <p style={{ fontSize:"0.52rem", color:"rgba(148,163,184,0.35)", fontFamily:"Poppins, sans-serif", textTransform:"uppercase", letterSpacing:"0.08em", marginTop:3 }}>{s.label}</p>
          </div>
        ))}
      </div>
      <div style={{ display:"flex", flexDirection:"column", gap:9 }}>
        {EVS.map(ev=>(
          <div key={ev.id} style={{ borderRadius:15, padding:"14px 16px", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(20px)", WebkitBackdropFilter:"blur(20px)", borderTop:"1px solid rgba(255,255,255,0.07)", borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:`2px solid ${ev.color}55`, display:"flex", alignItems:"center", gap:14 }}>
            <div style={{ flex:1, minWidth:0 }}>
              <span style={{ fontSize:"0.48rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.22)" }}>{ev.id}</span>
              <p style={{ fontSize:"0.88rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{ev.tipo}</p>
              <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{ev.tecnico} · {ev.fecha}</p>
            </div>
            <div style={{ display:"flex", flexDirection:"column", alignItems:"flex-end", gap:5 }}>
              <span style={{ padding:"2px 9px", borderRadius:99, fontSize:"0.6rem", fontWeight:700, color:ev.color, background:`${ev.color}14`, borderTop:`1px solid ${ev.color}40`, borderRight:`1px solid ${ev.color}40`, borderBottom:`1px solid ${ev.color}40`, borderLeft:`1px solid ${ev.color}40`, fontFamily:"Poppins, sans-serif" }}>{ev.estado}</span>
              <span style={{ fontSize:"0.55rem", color:"rgba(148,163,184,0.3)", fontFamily:"Poppins, sans-serif" }}>{ev.riesgo}</span>
            </div>
            {ev.estado==="Aprobado" && <button onClick={()=>onOpenReport({ report:{ id:ev.id, empresa:COMPANY_DATA.razonSocial, tipo:ev.tipo, riesgo:ev.riesgo as ReportRisk, fecha:ev.fecha, tecnico:ev.tecnico }, signature:{ signerName:ev.tecnico, signedAt:ev.fecha } })} style={{ width:32, height:32, borderRadius:9, cursor:"pointer", background:"rgba(34,197,94,0.08)", borderTop:"1px solid rgba(34,197,94,0.3)", borderRight:"1px solid rgba(34,197,94,0.3)", borderBottom:"1px solid rgba(34,197,94,0.3)", borderLeft:"1px solid rgba(34,197,94,0.3)", color:"#22c55e", display:"flex", alignItems:"center", justifyContent:"center", flexShrink:0 }}><svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5 5 5-5M12 15V3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg></button>}
          </div>
        ))}
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────── */
/* ── Section: Configuración ───────────────────────────────── */
/* ──────────────────────────────────────────────────────────── */
function ConfiguracionSection({ onToast }: { onToast:(m:string)=>void }) {
  const [nombre,   setNombre]   = useState("Gerencia García");
  const [correo,   setCorreo]   = useState("gerencia@empresa.com");
  const [telefono, setTelefono] = useState(COMPANY_DATA.telefono);
  const [n1,setN1]=useState(true); const [n2,setN2]=useState(true); const [n3,setN3]=useState(false);
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:14, maxWidth:660 }}>
      <div style={{ marginBottom:4 }}>
        <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>EVALIA · PORTAL EMPRESA</p>
        <h2 style={{ fontSize:"1.1rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em" }}>Configuración</h2>
      </div>
      <div style={{ borderRadius:18, overflow:"hidden", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(28px)", WebkitBackdropFilter:"blur(28px)", borderTop:"1px solid rgba(59,246,229,0.18)", borderRight:"1px solid rgba(255,255,255,0.06)", borderBottom:"1px solid rgba(255,255,255,0.06)", borderLeft:"2px solid rgba(59,246,229,0.5)" }}>
        <div style={{ height:2.5, background:"linear-gradient(90deg,#3BF6E5,#06b6d4)" }} />
        <div style={{ padding:"20px 24px" }}>
          <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(59,246,229,0.5)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>CUENTA DE USUARIO</p>
          <div style={{ display:"flex", flexDirection:"column", gap:11 }}>
            {[{l:"Nombre completo",v:nombre,s:setNombre},{l:"Correo electrónico",v:correo,s:setCorreo},{l:"Teléfono",v:telefono,s:setTelefono}].map(f=>(
              <div key={f.l} style={{ position:"relative" }}>
                <input value={f.v} onChange={e=>f.s(e.target.value)} style={{ width:"100%", padding:"1.2rem 1rem 0.5rem", borderRadius:14, outline:"none", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.1)", borderRight:"1px solid rgba(255,255,255,0.1)", borderBottom:"1px solid rgba(255,255,255,0.1)", borderLeft:"1px solid rgba(255,255,255,0.1)", color:"#f1f5f9", fontFamily:"Poppins, sans-serif", fontSize:"0.875rem", boxSizing:"border-box" as const }}/>
                <label style={{ position:"absolute", top:"0.5rem", left:"1rem", fontSize:"0.55rem", fontWeight:700, color:"rgba(59,246,229,0.6)", textTransform:"uppercase" as const, letterSpacing:"0.08em", fontFamily:"Poppins, sans-serif", pointerEvents:"none" as const }}>{f.l}</label>
              </div>
            ))}
          </div>
          <button onClick={()=>onToast("Perfil actualizado")} style={{ marginTop:14, width:"100%", padding:"11px 0", borderRadius:12, cursor:"pointer", background:"linear-gradient(135deg,#3BF6E5,#06b6d4)", border:"none", color:"#0F172A", fontFamily:"Poppins, sans-serif", fontWeight:800, fontSize:"0.85rem" }}>Guardar cambios</button>
        </div>
      </div>
      <div style={{ borderRadius:18, overflow:"hidden", background:"rgba(10,18,36,0.82)", backdropFilter:"blur(28px)", WebkitBackdropFilter:"blur(28px)", borderTop:"1px solid rgba(229,59,246,0.15)", borderRight:"1px solid rgba(255,255,255,0.06)", borderBottom:"1px solid rgba(255,255,255,0.06)", borderLeft:"2px solid rgba(229,59,246,0.5)" }}>
        <div style={{ height:2.5, background:"linear-gradient(90deg,#E53BF6,#8b5cf6)" }} />
        <div style={{ padding:"20px 24px" }}>
          <p style={{ fontSize:"0.58rem", fontWeight:700, color:"rgba(229,59,246,0.5)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>NOTIFICACIONES</p>
          {[{l:"Nueva evaluación programada",v:n1,s:setN1},{l:"Resultado de solicitud BPM",v:n2,s:setN2},{l:"Alertas del sistema",v:n3,s:setN3}].map(n=>(
            <div key={n.l} style={{ display:"flex", justifyContent:"space-between", alignItems:"center", padding:"10px 0", borderBottom:"1px solid rgba(255,255,255,0.05)" }}>
              <span style={{ fontSize:"0.78rem", color:"rgba(148,163,184,0.65)", fontFamily:"Poppins, sans-serif" }}>{n.l}</span>
              <button onClick={()=>n.s(!n.v)} style={{ width:44, height:24, borderRadius:99, cursor:"pointer", border:"none", background:n.v?"linear-gradient(135deg,#E53BF6,#8b5cf6)":"rgba(255,255,255,0.08)", position:"relative", transition:"all 0.2s ease" }}>
                <span style={{ position:"absolute", top:3, left:n.v?22:3, width:18, height:18, borderRadius:"50%", background:"white", transition:"left 0.2s ease" }}/>
              </button>
            </div>
          ))}
        </div>
      </div>
      <button onClick={()=>onToast("Contraseña: revisa tu correo")} style={{ padding:"12px 0", borderRadius:14, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.1)", borderRight:"1px solid rgba(255,255,255,0.1)", borderBottom:"1px solid rgba(255,255,255,0.1)", borderLeft:"1px solid rgba(255,255,255,0.1)", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", fontSize:"0.8rem", fontWeight:600 }}>Cambiar contraseña</button>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════ */
/* ── Main Component ──────────────────────────────────────────── */
/* ══════════════════════════════════════════════════════════════ */
export default function CompanyPortal({
  onBack, userName = "Gerencia García", accessToken,
}: {
  onBack?: () => void;
  userName?: string;
  accessToken: string;
}) {
  const width    = useWidth();
  const isMobile = width < 768;
  const isTablet = width >= 768 && width < 1100;
  const expanded = !isMobile && !isTablet;

  const [activeTab,   setActiveTab]   = useState<Tab>("dashboard");
  const [navOpen,     setNavOpen]     = useState(false);
  const [solicitudes, setSolicitudes] = useState<Solicitud[]>([]);
  const [companyId, setCompanyId] = useState<number | null>(null);
  const [newEntryId,  setNewEntryId]  = useState<string|null>(null);
  const [toast,       setToast]       = useState<string|null>(null);
  /* Informe ya firmado por el técnico: la empresa solo lo descarga o imprime. */
  const [signedReport, setSignedReport] = useState<SignedReport|null>(null);

  const showToast = (msg: string) => { setToast(msg); setTimeout(()=>setToast(null), 2800); };

  useEffect(() => {
    void Promise.all([listCompanies(accessToken), listBpmRequests(accessToken)])
      .then(([companies, requests]) => {
        setCompanyId(companies[0]?.id ?? null);
        setSolicitudes(requests.map(toSolicitud));
      })
      .catch(error => showToast(error instanceof Error ? error.message : "No fue posible cargar el portal."));
  }, [accessToken]);

  const navigate = (tab: Tab) => {
    setActiveTab(tab);
    if (isMobile) setNavOpen(false);
  };

  const handleFormSubmit = async (sol: Solicitud) => {
    if (companyId === null) { showToast("Tu usuario no tiene una empresa vinculada."); return; }
    try {
      const result = await createAndSubmitRequest(accessToken, {
        companyId, establishmentType: sol.tipo, reason: sol.motivo, observations: sol.observaciones ?? "",
      });
      const persisted = toSolicitud(result.request);
      setSolicitudes(prev => [persisted, ...prev]);
      setNewEntryId(persisted.id);
      setActiveTab("solicitudes");
      showToast(`Solicitud ${persisted.id} enviada · Pendiente de Asignación`);
      setTimeout(()=>setNewEntryId(null), 4000);
    } catch (error) {
      showToast(error instanceof Error ? error.message : "No fue posible enviar la solicitud.");
    }
  };

  const pendingCount = solicitudes.filter(s=>s.status==="Pendiente de Asignación").length;
  const currentNav   = NAV_ITEMS.find(n=>n.id===activeTab) ?? NAV_ITEMS[0];

  return (
    <>
      <style>{`
        @keyframes cpPulse { 0%,100%{transform:scale(1);opacity:1} 50%{transform:scale(1.5);opacity:0.45} }
        @keyframes cpSpin  { to{transform:rotate(360deg)} }
        @keyframes cpToast { from{opacity:0;transform:translateY(10px) scale(0.96)} to{opacity:1;transform:none} }
        @keyframes cpNewIn { from{opacity:0;transform:translateY(-12px)} to{opacity:1;transform:none} }
        .cp-new-entry { animation: cpNewIn 0.4s cubic-bezier(.22,1,.36,1) both; }
        .hide-scroll::-webkit-scrollbar{display:none}
        .hide-scroll{-ms-overflow-style:none;scrollbar-width:none}
      `}</style>

      <div style={{ display:"flex", height:"100svh", background:"#0F172A", fontFamily:"Poppins, sans-serif", overflow:"hidden", position:"relative" }}>
        {/* Orbs */}
        <div className="mesh-orb"            style={{ width:500, height:500, background:"rgba(229,59,246,0.18)", top:"-130px", left:"-60px", zIndex:0 }}/>
        <div className="mesh-orb mesh-orb-2" style={{ width:420, height:420, background:"rgba(59,246,229,0.15)", bottom:"-80px", right:"-60px", zIndex:0 }}/>
        <div className="mesh-orb mesh-orb-3" style={{ width:250, height:250, background:"rgba(139,92,246,0.12)", top:"40%", left:"45%", zIndex:0 }}/>

        {/* Mobile backdrop */}
        {isMobile && navOpen && <div onClick={()=>setNavOpen(false)} style={{ position:"fixed", inset:0, background:"rgba(0,0,0,0.65)", zIndex:40, backdropFilter:"blur(3px)" }}/>}

        {/* ── Sidebar ── */}
        <aside style={{
          position: "relative", top:0, left:0, height:"100%",
          width: isTablet ? 64 : 220, flexShrink:0, zIndex: 10,
          transition: "width 0.25s ease",
          background:"rgba(7,12,24,0.96)", backdropFilter:"blur(36px)", WebkitBackdropFilter:"blur(36px)",
          borderRight:"1px solid rgba(255,255,255,0.07)", display: isMobile ? "none" : "flex", flexDirection:"column",
        }}>
          {/* Company avatar */}
          <div style={{ padding: expanded?"22px 18px 16px":"20px 0 14px", display:"flex", alignItems:"center", gap:11, justifyContent: expanded?"flex-start":"center", borderBottom:"1px solid rgba(255,255,255,0.06)", flexShrink:0 }}>
            <div style={{ width:38, height:38, borderRadius:11, flexShrink:0, background:"linear-gradient(135deg,rgba(229,59,246,0.3),rgba(59,246,229,0.3))", borderTop:"1.5px solid rgba(229,59,246,0.5)", borderRight:"1.5px solid rgba(59,246,229,0.4)", borderBottom:"1.5px solid rgba(59,246,229,0.4)", borderLeft:"1.5px solid rgba(229,59,246,0.5)", display:"flex", alignItems:"center", justifyContent:"center" }}>
              <span style={{ fontSize:"0.8rem", fontWeight:800, background:"linear-gradient(135deg,#E53BF6,#3BF6E5)", WebkitBackgroundClip:"text", WebkitTextFillColor:"transparent", backgroundClip:"text" }}>GC</span>
            </div>
            {expanded && <div style={{ minWidth:0 }}>
              <p style={{ fontSize:"0.8rem", fontWeight:700, color:"#f1f5f9", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }}>García Constructores</p>
              <p style={{ fontSize:"0.52rem", color:"rgba(148,163,184,0.3)", letterSpacing:"0.07em", textTransform:"uppercase" }}>Admin de Empresa</p>
            </div>}
          </div>

          {/* Nav */}
          <nav className="hide-scroll" style={{ flex:1, padding: expanded?"14px 10px":"14px 0", display:"flex", flexDirection:"column", gap:2, overflowY:"auto" }}>
            {expanded && <p style={{ fontSize:"0.52rem", fontWeight:700, color:"rgba(148,163,184,0.22)", letterSpacing:"0.14em", textTransform:"uppercase", padding:"0 6px 8px" }}>MENÚ</p>}
            {NAV_ITEMS.map(item=>{
              const active = activeTab===item.id || (item.id==="solicitudes" && activeTab==="nueva-solicitud");
              const c = active ? "#E53BF6" : "rgba(148,163,184,0.4)";
              return (
                <button key={item.id} onClick={()=>navigate(item.id)} title={!expanded?item.label:undefined}
                  style={{ display:"flex", alignItems:"center", gap:10, padding: expanded?"9px 12px":"10px", justifyContent: expanded?"flex-start":"center", borderRadius:11, border:"none", cursor:"pointer", background: active?"rgba(229,59,246,0.1)":"transparent", color:c, fontFamily:"Poppins, sans-serif", fontWeight: active?600:400, fontSize:"0.78rem", transition:"all 0.18s ease", boxShadow: active?"inset 0 0 0 1px rgba(229,59,246,0.18)":"none", position:"relative", width:"100%" }}>
                  {active && <div style={{ position:"absolute", left:-10, top:"50%", transform:"translateY(-50%)", width:3, height:16, borderRadius:99, background:"#E53BF6", boxShadow:"0 0 10px #E53BF6" }}/>}
                  <span>{item.icon(c)}</span>
                  {expanded && <span style={{ flex:1, textAlign:"left" }}>{item.label}</span>}
                  {expanded && item.id==="solicitudes" && pendingCount>0 &&
                    <span style={{ padding:"1px 6px", borderRadius:99, background:"rgba(229,59,246,0.15)", borderTop:"1px solid rgba(229,59,246,0.35)", borderRight:"1px solid rgba(229,59,246,0.35)", borderBottom:"1px solid rgba(229,59,246,0.35)", borderLeft:"1px solid rgba(229,59,246,0.35)", fontSize:"0.5rem", fontWeight:800, color:"#E53BF6" }}>{pendingCount}</span>}
                  {!expanded && item.id==="solicitudes" && pendingCount>0 &&
                    <span style={{ position:"absolute", top:6, right:6, width:7, height:7, borderRadius:"50%", background:"#E53BF6" }}/>}
                </button>
              );
            })}
          </nav>

          {/* User + logout */}
          <div style={{ padding: expanded?"12px 10px":"12px 0", borderTop:"1px solid rgba(255,255,255,0.06)", flexShrink:0 }}>
            {expanded && <div style={{ padding:"8px 10px", borderRadius:10, background:"rgba(255,255,255,0.03)", display:"flex", alignItems:"center", gap:9, marginBottom:8 }}>
              <div style={{ width:28, height:28, borderRadius:8, background:"linear-gradient(135deg,#E53BF6,#3BF6E5)", padding:1.5, flexShrink:0 }}>
                <div style={{ width:"100%", height:"100%", borderRadius:6, background:"#1e293b", display:"flex", alignItems:"center", justifyContent:"center", fontSize:"0.65rem", fontWeight:800, color:"#f1f5f9" }}>{userName[0]}</div>
              </div>
              <div style={{ flex:1, minWidth:0 }}>
                <p style={{ fontSize:"0.72rem", fontWeight:700, color:"#f1f5f9", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }}>{userName}</p>
                <p style={{ fontSize:"0.52rem", color:"rgba(148,163,184,0.35)" }}>Admin de Empresa</p>
              </div>
            </div>}
            <button onClick={onBack} title={!expanded?"Salir":undefined} style={{ display:"flex", alignItems:"center", gap:9, padding: expanded?"9px 10px":"10px", borderRadius:10, border:"none", cursor:"pointer", background:"rgba(229,59,246,0.06)", borderTop:"1px solid rgba(229,59,246,0.18)", borderRight:"1px solid rgba(229,59,246,0.18)", borderBottom:"1px solid rgba(229,59,246,0.18)", borderLeft:"1px solid rgba(229,59,246,0.18)", color:"#E53BF6", fontFamily:"Poppins, sans-serif", fontSize:"0.72rem", fontWeight:600, justifyContent: expanded?"flex-start":"center", width:"100%" }}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#E53BF6" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
              {expanded && "Cerrar sesión"}
            </button>
          </div>
        </aside>

        {/* ── Main ── */}
        <main style={{ flex:1, display:"flex", flexDirection:"column", overflow:"hidden", position:"relative", zIndex:1, minWidth:0 }}>
          {/* Header */}
          <header style={{ flexShrink:0, display:"flex", alignItems:"center", justifyContent:"space-between", padding: isMobile?"13px 16px":"14px 28px", background:"rgba(7,12,24,0.85)", backdropFilter:"blur(24px)", WebkitBackdropFilter:"blur(24px)", borderBottom:"1px solid rgba(255,255,255,0.06)" }}>
            <div style={{ display:"flex", alignItems:"center", gap:12 }}>
              <div>
                <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(229,59,246,0.5)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif" }}>EVALIA · PORTAL EMPRESA</p>
                <h1 style={{ fontSize: isMobile?"1rem":"1.1rem", fontWeight:800, color:"#f1f5f9", letterSpacing:"-0.02em", fontFamily:"Poppins, sans-serif" }}>
                  {activeTab==="dashboard"       ? "Dashboard"
                   : activeTab==="nueva-solicitud" ? "Nueva Solicitud BPM"
                   : activeTab==="solicitudes"     ? "Mis Solicitudes"
                   : activeTab==="perfil"           ? "Perfil de Empresa"
                   : activeTab==="evaluaciones"     ? "Evaluaciones"
                   :                                 "Configuración"}
                </h1>
              </div>
            </div>
            <div style={{ display:"flex", alignItems:"center", gap:8 }}>
              <div style={{ display:"flex", alignItems:"center", gap:6, padding:"5px 11px", borderRadius:99, background:"rgba(34,197,94,0.08)", borderTop:"1px solid rgba(34,197,94,0.25)", borderRight:"1px solid rgba(34,197,94,0.25)", borderBottom:"1px solid rgba(34,197,94,0.25)", borderLeft:"1px solid rgba(34,197,94,0.25)" }}>
                <span style={{ width:6, height:6, borderRadius:"50%", background:"#22c55e", boxShadow:"0 0 6px #22c55e", display:"inline-block", animation:"cpPulse 2.5s ease-in-out infinite" }}/>
                <span style={{ fontSize:"0.6rem", fontWeight:700, color:"#22c55e", fontFamily:"Poppins, sans-serif" }}>Activo</span>
              </div>
              {isMobile && (
                <button onClick={onBack} title="Cerrar sesión" style={{ width:36, height:36, borderRadius:10, background:"rgba(229,59,246,0.08)", border:"1px solid rgba(229,59,246,0.25)", display:"flex", alignItems:"center", justifyContent:"center", cursor:"pointer" }}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#E53BF6" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
                </button>
              )}
            </div>
          </header>

          {/* Content area */}
          <div key={activeTab} className="hide-scroll" style={{ flex:1, overflowY:"auto", padding: isMobile?"16px 14px 84px":"24px 28px 36px" }}>
            {activeTab==="dashboard"       && <DashboardSection solicitudes={solicitudes} onNavigate={navigate} onToast={showToast}/>}
            {activeTab==="nueva-solicitud" && <NuevaSolicitudSection onSubmit={handleFormSubmit} onCancel={()=>navigate("dashboard")}/>}
            {activeTab==="solicitudes"     && <SolicitudesSection solicitudes={solicitudes} newId={newEntryId} onToast={showToast} onNavigate={navigate}/>}
            {activeTab==="perfil"          && <PerfilSection isMobile={isMobile} onToast={showToast}/>}
            {activeTab==="evaluaciones"    && <EvaluacionesSection onOpenReport={setSignedReport}/>}
            {activeTab==="configuracion"   && <ConfiguracionSection onToast={showToast}/>}
          </div>
        </main>

        {/* Toast */}
        {/* Bottom tab bar — mobile only */}
        {isMobile && (
          <div style={{ position:"fixed", bottom:0, left:0, right:0, zIndex:60, background:"rgba(7,12,24,0.97)", backdropFilter:"blur(28px)", WebkitBackdropFilter:"blur(28px)", borderTop:"1px solid rgba(255,255,255,0.09)", display:"flex", alignItems:"stretch", height:66, paddingBottom:"env(safe-area-inset-bottom,0px)" }}>
            {([
              { id:"dashboard"    as Tab, label:"Inicio",       icon:(c:string)=><svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" stroke={c} strokeWidth="1.75"/><polyline points="9,22 9,12 15,12 15,22" stroke={c} strokeWidth="1.75"/></svg> },
              { id:"solicitudes"  as Tab, label:"Solicitudes",  icon:(c:string)=><svg width="16" height="16" viewBox="0 0 24 24" fill="none"><rect x="8" y="2" width="8" height="4" rx="1" stroke={c} strokeWidth="1.5"/><path d="M8 3H6a2 2 0 00-2 2v14a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2h-2" stroke={c} strokeWidth="1.5"/><path d="M9 12h6M9 16h4" stroke={c} strokeWidth="1.5" strokeLinecap="round"/></svg> },
              { id:"nueva-solicitud" as Tab, label:"+ Nueva",  icon:(c:string)=><svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M12 5v14M5 12h14" stroke={c} strokeWidth="2.5" strokeLinecap="round"/></svg> },
              { id:"evaluaciones" as Tab, label:"Evaluaciones", icon:(c:string)=><svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 11l3 3L22 4" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" stroke={c} strokeWidth="1.75"/></svg> },
              { id:"perfil"       as Tab, label:"Perfil",       icon:(c:string)=><svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" stroke={c} strokeWidth="1.75"/><circle cx="12" cy="7" r="4" stroke={c} strokeWidth="1.75"/></svg> },
            ]).map(tab=>{
              const active = activeTab===tab.id || (tab.id==="solicitudes" && activeTab==="nueva-solicitud" && false);
              const isNew = tab.id==="nueva-solicitud";
              const accent = "#E53BF6";
              return (
                <button key={tab.id} onClick={()=>navigate(tab.id)} style={{ flex:1, display:"flex", flexDirection:"column", alignItems:"center", justifyContent:"center", gap:2, border:"none", cursor:"pointer", background:"transparent", color: isNew?"#E53BF6":(active?accent:"rgba(148,163,184,0.4)"), position:"relative", paddingTop:isNew?0:4 }}>
                  {active && !isNew && <div style={{ position:"absolute", top:0, left:"50%", transform:"translateX(-50%)", width:30, height:2.5, borderRadius:"0 0 3px 3px", background:accent, boxShadow:`0 0 8px ${accent}99` }}/>}
                  {isNew
                    ? <div style={{ width:40, height:40, borderRadius:13, background:"linear-gradient(135deg,#E53BF6,#8b5cf6,#3BF6E5)", display:"flex", alignItems:"center", justifyContent:"center", boxShadow:"0 4px 16px rgba(229,59,246,0.5)", marginTop:-4 }}>{tab.icon("white")}</div>
                    : <div style={{ position:"relative" }}>{tab.icon(active?accent:"rgba(148,163,184,0.4)")}</div>
                  }
                  <span style={{ fontSize:"0.48rem", fontWeight:active||isNew?700:400, fontFamily:"Poppins, sans-serif", letterSpacing:"0.02em" }}>{tab.label}</span>
                </button>
              );
            })}
          </div>
        )}

        {signedReport && <ReportViewer report={signedReport.report} signature={signedReport.signature} canSign={false} onClose={()=>setSignedReport(null)} onToast={showToast}/>}

        {toast && (
          <div style={{ position:"fixed", bottom:isMobile?74:24, left:"50%", transform:"translateX(-50%)", zIndex:90, display:"flex", alignItems:"center", gap:10, padding:"12px 20px", borderRadius:14, whiteSpace:"nowrap", background:"rgba(8,14,28,0.97)", backdropFilter:"blur(24px)", borderTop:"1px solid rgba(59,246,229,0.4)", borderRight:"1px solid rgba(59,246,229,0.4)", borderBottom:"1px solid rgba(59,246,229,0.4)", borderLeft:"1px solid rgba(59,246,229,0.4)", boxShadow:"0 16px 48px rgba(0,0,0,0.6), 0 0 24px rgba(59,246,229,0.15)", animation:"cpToast 0.3s cubic-bezier(.22,1,.36,1) both" }}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#3BF6E5" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
            <span style={{ fontSize:"0.8rem", fontWeight:600, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{toast}</span>
          </div>
        )}
      </div>
    </>
  );
}

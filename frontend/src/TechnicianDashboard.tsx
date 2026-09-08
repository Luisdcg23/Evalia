import { useState, useEffect } from "react";

function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

/* ── Types ──────────────────────────────────────────────── */
type Section    = "inicio" | "evaluaciones" | "calendario" | "reportes" | "configuracion";
type EvalStatus = "Pendiente" | "En Curso" | "Completado";
type RiskLevel  = "Bajo" | "Moderado" | "Alto" | "Crítico";
type ReportState = "idle" | "gen" | "done";

/* ── Color maps ─────────────────────────────────────────── */
const RISK: Record<RiskLevel, { color:string; bg:string }> = {
  Bajo:     { color:"#22c55e", bg:"rgba(34,197,94,0.12)"   },
  Moderado: { color:"#f59e0b", bg:"rgba(245,158,11,0.12)"  },
  Alto:     { color:"#E53BF6", bg:"rgba(229,59,246,0.12)"  },
  Crítico:  { color:"#ef4444", bg:"rgba(239,68,68,0.12)"   },
};
const STATUS_COLOR: Record<EvalStatus,{ color:string; bg:string; border:string }> = {
  Pendiente:  { color:"rgba(148,163,184,0.7)", bg:"rgba(148,163,184,0.07)", border:"rgba(148,163,184,0.2)" },
  "En Curso": { color:"#F6E53B", bg:"rgba(246,229,59,0.1)",  border:"rgba(246,229,59,0.35)" },
  Completado: { color:"#22c55e", bg:"rgba(34,197,94,0.1)",   border:"rgba(34,197,94,0.35)"  },
};

/* ── Data ───────────────────────────────────────────────── */
interface Evaluation {
  id:string; empresa:string; hora:string; horaFin:string;
  direccion:string; municipio:string; tipo:string;
  riesgo:RiskLevel; status:EvalStatus; duracion:string;
  contacto:string; telefono:string;
}
interface ReportItem { id:string; empresa:string; fechaLabel:string; tipo:string; overdue:boolean; daysAgo:number; riesgo:RiskLevel }

const EVALUACIONES: Evaluation[] = [
  { id:"EVA-0892", empresa:"Restaurante El Buen Sabor", hora:"09:00", horaFin:"11:00", direccion:"C/ Independencia 45", municipio:"Santo Domingo", tipo:"Inspección Sanitaria", riesgo:"Moderado", status:"Pendiente", duracion:"~2h", contacto:"Sr. Pérez", telefono:"+1 809 555 0191" },
  { id:"EVA-0893", empresa:"Farmacia Central Esperanza", hora:"11:30", horaFin:"13:00", direccion:"Av. Churchill 122", municipio:"Santo Domingo", tipo:"Revisión LAPCH", riesgo:"Alto", status:"Pendiente", duracion:"~1.5h", contacto:"Lic. Rodríguez", telefono:"+1 809 555 0204" },
  { id:"EVA-0895", empresa:"Almacén Don Carlos S.A.", hora:"14:30", horaFin:"16:30", direccion:"C/ Las Carreras 8", municipio:"Santiago", tipo:"Inspección Eléctrica", riesgo:"Bajo", status:"Pendiente", duracion:"~2h", contacto:"Ing. Montoya", telefono:"+1 809 555 0317" },
  { id:"EVA-0880", empresa:"Panadería Artesanal Cruz", hora:"08:00", horaFin:"10:00", direccion:"C/ Duarte 210", municipio:"La Vega", tipo:"Evaluación Higiénica", riesgo:"Moderado", status:"Completado", duracion:"2h 15m", contacto:"Sra. Cruz", telefono:"+1 809 555 0422" },
  { id:"EVA-0875", empresa:"Laboratorio Diagnostics RD", hora:"13:00", horaFin:"15:30", direccion:"Av. Luperón 55", municipio:"Santo Domingo", tipo:"Inspección BPM", riesgo:"Crítico", status:"Completado", duracion:"2h 30m", contacto:"Dr. Méndez", telefono:"+1 809 555 0518" },
];

const INFORMES: ReportItem[] = [
  { id:"INF-0875", empresa:"Laboratorio Diagnostics RD", fechaLabel:"Hace 3 días", tipo:"Inspección BPM",     overdue:true,  daysAgo:3, riesgo:"Crítico"  },
  { id:"INF-0860", empresa:"Distribuidora Caribe",        fechaLabel:"Hace 6 días", tipo:"Evaluación General", overdue:true,  daysAgo:6, riesgo:"Alto"     },
  { id:"INF-0850", empresa:"Industria Montoya S.A.",      fechaLabel:"Hace 8 días", tipo:"Revisión Normativa", overdue:true,  daysAgo:8, riesgo:"Moderado" },
  { id:"INF-0880", empresa:"Panadería Artesanal Cruz",    fechaLabel:"Hoy",          tipo:"Evaluación Higiénica",overdue:false,daysAgo:0, riesgo:"Moderado" },
];

/* Calendar: September 2026 (Sep 1 = Tuesday → offset 1) */
const CAL_EVENTS: Record<number, { label:string; color:string }[]> = {
  1:  [{ label:"EVA-0892", color:"#3BF6E5" }, { label:"EVA-0893", color:"#E53BF6" }],
  2:  [{ label:"EVA-0895", color:"#3BF6E5" }],
  5:  [{ label:"EVA-0896", color:"#22c55e" }],
  8:  [{ label:"EVA-0897", color:"#3BF6E5" }],
  10: [{ label:"EVA-0898", color:"#F6E53B" }],
  12: [{ label:"EVA-0899", color:"#3BF6E5" }, { label:"EVA-0900", color:"#E53BF6" }],
  15: [{ label:"EVA-0901", color:"#22c55e" }],
  17: [{ label:"EVA-0902", color:"#3BF6E5" }],
  22: [{ label:"EVA-0903", color:"#F6E53B" }],
  25: [{ label:"EVA-0904", color:"#3BF6E5" }],
  29: [{ label:"EVA-0905", color:"#22c55e" }],
};

/* ── Nav items ──────────────────────────────────────────── */
const NAV: { id:Section; label:string; badge?:number; icon:(c:string)=>React.ReactNode }[] = [
  { id:"inicio",       label:"Inicio",             icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" stroke={c} strokeWidth="1.75"/><polyline points="9,22 9,12 15,12 15,22" stroke={c} strokeWidth="1.75"/></svg> },
  { id:"evaluaciones", label:"Evaluaciones",       badge:3, icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M9 11l3 3L22 4" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" stroke={c} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg> },
  { id:"calendario",   label:"Calendario",         icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><rect x="3" y="4" width="18" height="18" rx="2" stroke={c} strokeWidth="1.75"/><path d="M16 2v4M8 2v4M3 10h18" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg> },
  { id:"reportes",     label:"Pendientes informe", badge:3, icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" stroke={c} strokeWidth="1.75"/><polyline points="14,2 14,8 20,8" stroke={c} strokeWidth="1.75"/><line x1="16" y1="13" x2="8" y2="13" stroke={c} strokeWidth="1.75" strokeLinecap="round"/></svg> },
  { id:"configuracion",label:"Configuración",      icon:c=><svg width="15" height="15" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="3" stroke={c} strokeWidth="1.75"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z" stroke={c} strokeWidth="1.75"/></svg> },
];

/* ── Shared UI ──────────────────────────────────────────── */
function Chip({ label, color, bg, border }: { label:string; color:string; bg:string; border:string }) {
  return <span style={{ display:"inline-flex", alignItems:"center", gap:5, padding:"2px 9px", borderRadius:99, background:bg, borderTop:`1px solid ${border}`, borderRight:`1px solid ${border}`, borderBottom:`1px solid ${border}`, borderLeft:`1px solid ${border}`, fontSize:"0.58rem", fontWeight:700, color, fontFamily:"Poppins, sans-serif", whiteSpace:"nowrap" }}><span style={{ width:5, height:5, borderRadius:"50%", background:color, flexShrink:0 }}/>{label}</span>;
}

function GlassCard({ children, accent, style }: { children:React.ReactNode; accent?:string; style?:React.CSSProperties }) {
  return <div style={{ borderRadius:16, background:"rgba(10,18,36,0.82)", backdropFilter:"blur(24px) saturate(160%)", WebkitBackdropFilter:"blur(24px) saturate(160%)", borderTop:`1px solid ${accent?accent+"22":"rgba(255,255,255,0.07)"}`, borderRight:"1px solid rgba(255,255,255,0.05)", borderBottom:"1px solid rgba(255,255,255,0.05)", borderLeft:`2px solid ${accent?accent+"55":"rgba(255,255,255,0.07)"}`, overflow:"hidden", ...style }}>{children}</div>;
}

function SectionHeader({ title, subtitle, action }: { title:string; subtitle?:string; action?:React.ReactNode }) {
  return (
    <div style={{ display:"flex", alignItems:"flex-end", justifyContent:"space-between", marginBottom:20, gap:12 }}>
      <div>
        <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(59,246,229,0.5)", letterSpacing:"0.14em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>EVALIA · TÉCNICO</p>
        <h2 style={{ fontSize:"1.1rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", letterSpacing:"-0.02em" }}>{title}</h2>
        {subtitle && <p style={{ fontSize:"0.72rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif", marginTop:2 }}>{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}

/* ── Weekly strip (Inicio) ──────────────────────────────── */
const WEEK_DAYS = [
  { label:"L", date:31, month:"ago", evals:0,  isToday:false },
  { label:"M", date:1,  month:"sep", evals:2,  isToday:true  },
  { label:"X", date:2,  month:"sep", evals:1,  isToday:false },
  { label:"J", date:3,  month:"sep", evals:0,  isToday:false },
  { label:"V", date:4,  month:"sep", evals:1,  isToday:false },
  { label:"S", date:5,  month:"sep", evals:0,  isToday:false },
  { label:"D", date:6,  month:"sep", evals:0,  isToday:false },
];

/* ── Evaluation card (interactive) ─────────────────────── */
function EvalCard({ ev, onToast }: { ev:Evaluation; onToast:(m:string)=>void }) {
  const [status, setStatus] = useState<EvalStatus>(ev.status);
  const [sheetOpen, setSheetOpen] = useState(false);
  const rk = RISK[ev.riesgo];
  const st = STATUS_COLOR[status];

  return (
    <>
      {sheetOpen && (
        <div onClick={()=>setSheetOpen(false)} style={{ position:"fixed", inset:0, background:"rgba(0,0,0,0.65)", zIndex:60, backdropFilter:"blur(5px)" }}>
          <div onClick={e=>e.stopPropagation()} style={{ position:"absolute", bottom:0, left:0, right:0, maxWidth:560, margin:"0 auto", borderRadius:"20px 20px 0 0", background:"rgba(8,14,28,0.97)", backdropFilter:"blur(40px)", borderTop:"1px solid rgba(59,246,229,0.3)", borderRight:"1px solid rgba(255,255,255,0.07)", borderLeft:"1px solid rgba(255,255,255,0.07)", padding:"16px 20px 44px" }}>
            <div style={{ display:"flex", justifyContent:"center", marginBottom:12 }}><div style={{ width:36, height:4, borderRadius:99, background:"rgba(255,255,255,0.12)" }}/></div>
            <div style={{ height:2.5, background:"linear-gradient(90deg,#3BF6E5,#06b6d4)", borderRadius:99, marginBottom:16 }}/>
            <p style={{ fontSize:"0.85rem", fontWeight:800, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", marginBottom:4 }}>{ev.empresa}</p>
            <p style={{ fontSize:"0.65rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif", marginBottom:16 }}>{ev.tipo}</p>
            {[
              ["Horario", `${ev.hora} – ${ev.horaFin} (${ev.duracion})`],
              ["Dirección", `${ev.direccion}, ${ev.municipio}`],
              ["Contacto", `${ev.contacto} · ${ev.telefono}`],
            ].map(([k,v])=>(
              <div key={k} style={{ padding:"10px 0", borderBottom:"1px solid rgba(255,255,255,0.05)" }}>
                <p style={{ fontSize:"0.55rem", color:"rgba(59,246,229,0.5)", fontFamily:"Poppins, sans-serif", textTransform:"uppercase", letterSpacing:"0.1em", marginBottom:3 }}>{k}</p>
                <p style={{ fontSize:"0.78rem", color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{v}</p>
              </div>
            ))}
            <button onClick={()=>{ setStatus("En Curso"); setSheetOpen(false); onToast("Evaluación iniciada · "+ev.id); }} style={{ marginTop:20, width:"100%", padding:"14px 0", borderRadius:14, border:"none", cursor:"pointer", background:"linear-gradient(135deg,#3BF6E5,#06b6d4)", color:"#0F172A", fontFamily:"Poppins, sans-serif", fontWeight:800, fontSize:"0.9rem" }}>Confirmar Inicio</button>
          </div>
        </div>
      )}

      <GlassCard accent={status==="En Curso"?"#F6E53B":status==="Completado"?"#22c55e":"#3BF6E5"}>
        <div style={{ padding:"16px 18px", display:"flex", flexDirection:"column", gap:12 }}>
          <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start" }}>
            <div>
              <span style={{ fontSize:"0.52rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.25)" }}>{ev.id}</span>
              <p style={{ fontSize:"0.92rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif", marginBottom:2 }}>{ev.empresa}</p>
              <p style={{ fontSize:"0.65rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{ev.tipo} · {ev.municipio}</p>
            </div>
            <Chip label={status} color={st.color} bg={st.bg} border={st.border} />
          </div>

          <div style={{ display:"flex", gap:10, alignItems:"center" }}>
            <div style={{ display:"flex", alignItems:"center", gap:6 }}>
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="10" stroke="rgba(148,163,184,0.35)" strokeWidth="1.75"/><path d="M12 6v6l4 2" stroke="rgba(148,163,184,0.35)" strokeWidth="1.75" strokeLinecap="round"/></svg>
              <span style={{ fontSize:"0.65rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif" }}>{ev.hora} – {ev.horaFin}</span>
            </div>
            <Chip label={ev.riesgo} color={rk.color} bg={rk.bg} border={rk.color+"40"} />
          </div>

          {status === "Pendiente" && (
            <button onClick={()=>setSheetOpen(true)} style={{ width:"100%", padding:"12px 0", borderRadius:12, border:"none", cursor:"pointer", background:"linear-gradient(135deg,#3BF6E5 0%,#06b6d4 100%)", color:"#0F172A", fontFamily:"Poppins, sans-serif", fontWeight:800, fontSize:"0.85rem", boxShadow:"0 8px 24px rgba(59,246,229,0.35)" }}>
              Iniciar Evaluación
            </button>
          )}
          {status === "En Curso" && (
            <button onClick={()=>{ setStatus("Completado"); onToast("Evaluación finalizada · "+ev.id); }} style={{ width:"100%", padding:"12px 0", borderRadius:12, border:"none", cursor:"pointer", background:"linear-gradient(135deg,#F6E53B,#f59e0b)", color:"#0F172A", fontFamily:"Poppins, sans-serif", fontWeight:800, fontSize:"0.85rem", boxShadow:"0 8px 24px rgba(246,229,59,0.35)" }}>
              Finalizar Evaluación
            </button>
          )}
          {status === "Completado" && (
            <div style={{ display:"flex", alignItems:"center", justifyContent:"center", gap:7, padding:"10px", borderRadius:11, background:"rgba(34,197,94,0.08)", borderTop:"1px solid rgba(34,197,94,0.25)", borderRight:"1px solid rgba(34,197,94,0.25)", borderBottom:"1px solid rgba(34,197,94,0.25)", borderLeft:"1px solid rgba(34,197,94,0.25)" }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#22c55e" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
              <span style={{ fontSize:"0.75rem", fontWeight:700, color:"#22c55e", fontFamily:"Poppins, sans-serif" }}>Completada · Generar informe</span>
            </div>
          )}
        </div>
      </GlassCard>
    </>
  );
}

/* ── Report card (interactive) ──────────────────────────── */
function ReportCard({ rep, onToast }: { rep:ReportItem; onToast:(m:string)=>void }) {
  const [state, setState] = useState<ReportState>("idle");
  const [progress, setProgress] = useState(0);
  const rk = RISK[rep.riesgo];

  const handleGenerate = () => {
    setState("gen");
    let p = 0;
    const iv = setInterval(()=>{ p += 7 + Math.random()*8; if(p >= 100){ clearInterval(iv); setProgress(100); setTimeout(()=>{ setState("done"); onToast("Informe generado · "+rep.id); },300); } else setProgress(p); },140);
  };

  return (
    <GlassCard accent={rep.overdue ? "#E53BF6" : "#22c55e"}>
      <div style={{ padding:"16px 18px", display:"flex", flexDirection:"column", gap:10, position:"relative", overflow:"hidden" }}>
        {state==="done" && <div style={{ position:"absolute", inset:0, background:"rgba(34,197,94,0.1)", display:"flex", alignItems:"center", justifyContent:"center", gap:10, zIndex:5 }}><svg width="20" height="20" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#22c55e" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg><span style={{ fontFamily:"Poppins, sans-serif", fontWeight:700, color:"#22c55e", fontSize:"0.88rem" }}>Informe generado</span></div>}

        <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start" }}>
          <div>
            <span style={{ fontSize:"0.52rem", fontFamily:"'Courier New',monospace", color:"rgba(148,163,184,0.25)" }}>{rep.id}</span>
            <p style={{ fontSize:"0.88rem", fontWeight:700, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{rep.empresa}</p>
            <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{rep.tipo}</p>
          </div>
          <div style={{ display:"flex", flexDirection:"column", alignItems:"flex-end", gap:5 }}>
            <Chip label={rep.riesgo} color={rk.color} bg={rk.bg} border={rk.color+"40"} />
            {rep.overdue && <span style={{ fontSize:"0.55rem", fontWeight:700, color:"#E53BF6", fontFamily:"Poppins, sans-serif" }}>⚠ Vencido</span>}
          </div>
        </div>

        <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center" }}>
          <span style={{ fontSize:"0.6rem", color:"rgba(148,163,184,0.35)", fontFamily:"Poppins, sans-serif" }}>{rep.fechaLabel} · {rep.daysAgo>0 ? `${rep.daysAgo} días pendiente` : "Reciente"}</span>
          {state==="idle" && (
            <button onClick={handleGenerate} style={{ padding:"7px 14px", borderRadius:10, cursor:"pointer", background:"linear-gradient(135deg,#3BF6E5,#06b6d4)", border:"none", color:"#0F172A", fontSize:"0.7rem", fontWeight:800, fontFamily:"Poppins, sans-serif", boxShadow:"0 4px 16px rgba(59,246,229,0.3)" }}>Generar</button>
          )}
        </div>

        {state==="gen" && (
          <div>
            <div style={{ height:4, borderRadius:99, background:"rgba(255,255,255,0.06)", overflow:"hidden" }}>
              <div style={{ height:"100%", width:`${progress}%`, background:"linear-gradient(90deg,#3BF6E5,#06b6d4)", borderRadius:99, transition:"width 0.12s linear", boxShadow:"0 0 10px rgba(59,246,229,0.5)" }}/>
            </div>
            <p style={{ fontSize:"0.58rem", color:"rgba(59,246,229,0.6)", fontFamily:"Poppins, sans-serif", marginTop:4, textAlign:"right" }}>Generando… {Math.round(progress)}%</p>
          </div>
        )}
      </div>
    </GlassCard>
  );
}

/* ── Section: Inicio ────────────────────────────────────── */
function InicioSection({ onNavigate, onToast }: { onNavigate:(s:Section)=>void; onToast:(m:string)=>void }) {
  const pending   = EVALUACIONES.filter(e=>e.status==="Pendiente").length;
  const overdueCt = INFORMES.filter(r=>r.overdue).length;

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:20 }}>
      {/* Overdue alert banner */}
      {overdueCt > 0 && (
        <div style={{ display:"flex", alignItems:"center", gap:12, padding:"12px 16px", borderRadius:13, background:"rgba(229,59,246,0.08)", borderTop:"1px solid rgba(229,59,246,0.3)", borderRight:"1px solid rgba(229,59,246,0.3)", borderBottom:"1px solid rgba(229,59,246,0.3)", borderLeft:"2px solid rgba(229,59,246,0.6)" }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="#E53BF6" strokeWidth="2"/><line x1="12" y1="9" x2="12" y2="13" stroke="#E53BF6" strokeWidth="2" strokeLinecap="round"/><line x1="12" y1="17" x2="12.01" y2="17" stroke="#E53BF6" strokeWidth="2.5" strokeLinecap="round"/></svg>
          <span style={{ fontSize:"0.8rem", fontWeight:600, color:"#E53BF6", fontFamily:"Poppins, sans-serif", flex:1 }}>{overdueCt} informe{overdueCt>1?"s":""} vencido{overdueCt>1?"s":""} pendiente{overdueCt>1?"s":""} de entrega</span>
          <button onClick={()=>onNavigate("reportes")} style={{ padding:"5px 12px", borderRadius:9, cursor:"pointer", background:"rgba(229,59,246,0.15)", borderTop:"1px solid rgba(229,59,246,0.4)", borderRight:"1px solid rgba(229,59,246,0.4)", borderBottom:"1px solid rgba(229,59,246,0.4)", borderLeft:"1px solid rgba(229,59,246,0.4)", color:"#E53BF6", fontSize:"0.62rem", fontWeight:700, fontFamily:"Poppins, sans-serif" }}>Ver informes</button>
        </div>
      )}

      {/* Quick stats */}
      <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill, minmax(140px, 1fr))", gap:10 }}>
        {[
          { label:"Evaluaciones hoy", value:EVALUACIONES.filter(e=>e.status!=="Completado").length, color:"#3BF6E5" },
          { label:"En curso",         value:EVALUACIONES.filter(e=>e.status==="En Curso").length,    color:"#F6E53B" },
          { label:"Completadas",      value:EVALUACIONES.filter(e=>e.status==="Completado").length,  color:"#22c55e" },
          { label:"Informes pend.",   value:overdueCt,                                                color:"#E53BF6" },
        ].map(s=>(
          <GlassCard key={s.label} accent={s.color}>
            <div style={{ padding:"14px 16px" }}>
              <p style={{ fontSize:"1.8rem", fontWeight:900, color:s.color, fontFamily:"Poppins, sans-serif", lineHeight:1, textShadow:`0 0 16px ${s.color}50` }}>{s.value}</p>
              <p style={{ fontSize:"0.62rem", color:"rgba(148,163,184,0.45)", fontFamily:"Poppins, sans-serif", marginTop:4 }}>{s.label}</p>
            </div>
          </GlassCard>
        ))}
      </div>

      {/* Weekly strip */}
      <GlassCard accent="#F6E53B">
        <div style={{ padding:"16px 18px" }}>
          <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:12 }}>SEMANA · 01–07 SEP 2026</p>
          <div style={{ display:"flex", gap:6 }}>
            {WEEK_DAYS.map(d=>(
              <div key={d.label} style={{ flex:1, display:"flex", flexDirection:"column", alignItems:"center", gap:5 }}>
                <span style={{ fontSize:"0.6rem", fontWeight:600, color: d.isToday ? "#F6E53B" : "rgba(148,163,184,0.35)", fontFamily:"Poppins, sans-serif", textTransform:"uppercase" }}>{d.label}</span>
                <div style={{ width:36, height:36, borderRadius:10, display:"flex", alignItems:"center", justifyContent:"center", background: d.isToday ? "rgba(246,229,59,0.18)" : "rgba(255,255,255,0.03)", borderTop: d.isToday ? "1px solid rgba(246,229,59,0.55)" : "1px solid rgba(255,255,255,0.05)", borderRight: d.isToday ? "1px solid rgba(246,229,59,0.55)" : "1px solid rgba(255,255,255,0.05)", borderBottom: d.isToday ? "1px solid rgba(246,229,59,0.55)" : "1px solid rgba(255,255,255,0.05)", borderLeft: d.isToday ? "1px solid rgba(246,229,59,0.55)" : "1px solid rgba(255,255,255,0.05)", boxShadow: d.isToday ? "0 0 14px rgba(246,229,59,0.25)" : "none" }}>
                  <span style={{ fontSize:"0.75rem", fontWeight: d.isToday ? 800 : 400, color: d.isToday ? "#F6E53B" : "rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif" }}>{d.date}</span>
                </div>
                {d.evals > 0 && <div style={{ display:"flex", gap:3 }}>{Array.from({length:d.evals}).map((_,i)=><span key={i} style={{ width:5, height:5, borderRadius:"50%", background:"#3BF6E5", boxShadow:"0 0 4px #3BF6E5" }}/>)}</div>}
              </div>
            ))}
          </div>
        </div>
      </GlassCard>

      {/* Today's evaluations */}
      <div>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:12 }}>EVALUACIONES DE HOY</p>
        <div style={{ display:"flex", flexDirection:"column", gap:10 }}>
          {EVALUACIONES.filter(e=>e.status!=="Completado").map(ev=>(
            <EvalCard key={ev.id} ev={ev} onToast={onToast} />
          ))}
        </div>
      </div>
    </div>
  );
}

/* ── Section: Evaluaciones (all) ────────────────────────── */
function EvaluacionesSection({ onToast }: { onToast:(m:string)=>void }) {
  const [filter, setFilter] = useState<EvalStatus|"Todas">("Todas");
  const filtered = filter==="Todas" ? EVALUACIONES : EVALUACIONES.filter(e=>e.status===filter);
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <SectionHeader title="Evaluaciones" subtitle={`${EVALUACIONES.length} en total`} />
      <div className="hide-scroll" style={{ display:"flex", gap:7, overflowX:"auto" }}>
        {(["Todas","Pendiente","En Curso","Completado"] as const).map(f=>{
          const active=filter===f; const c=f==="Todas"?"rgba(148,163,184,0.7)":STATUS_COLOR[f].color;
          return <button key={f} onClick={()=>setFilter(f)} style={{ flexShrink:0, padding:"5px 14px", borderRadius:99, fontSize:"0.65rem", fontWeight:600, fontFamily:"Poppins, sans-serif", cursor:"pointer", background:active?`${c}18`:"rgba(255,255,255,0.04)", borderTop:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderRight:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderBottom:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", borderLeft:active?`1px solid ${c}50`:"1px solid rgba(255,255,255,0.09)", color:active?c:"rgba(148,163,184,0.45)" }}>{f}</button>;
        })}
      </div>
      <div style={{ display:"flex", flexDirection:"column", gap:10 }}>
        {filtered.map(ev=><EvalCard key={ev.id} ev={ev} onToast={onToast}/>)}
      </div>
    </div>
  );
}

/* ── Section: Calendario ────────────────────────────────── */
function CalendarioSection() {
  const days = ["L","M","X","J","V","S","D"];
  const cells: (number|null)[] = [null, ...Array.from({length:30},(_,i)=>i+1)];
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:20 }}>
      <SectionHeader title="Calendario" subtitle="Septiembre 2026" />
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
            {cells.map((day, i)=>{
              const events = day ? CAL_EVENTS[day] : null;
              const isToday = day===1;
              return (
                <div key={i} style={{ aspectRatio:"1", borderRadius:10, display:"flex", flexDirection:"column", alignItems:"center", justifyContent:"center", gap:3, cursor:day?"pointer":"default", background:isToday?"rgba(246,229,59,0.15)":day?"rgba(255,255,255,0.03)":"transparent", borderTop:isToday?"1px solid rgba(246,229,59,0.5)":"1px solid transparent", borderRight:isToday?"1px solid rgba(246,229,59,0.5)":"1px solid transparent", borderBottom:isToday?"1px solid rgba(246,229,59,0.5)":"1px solid transparent", borderLeft:isToday?"1px solid rgba(246,229,59,0.5)":"1px solid transparent" }}>
                  {day && <>
                    <span style={{ fontSize:"0.7rem", fontWeight:isToday?800:400, color:isToday?"#F6E53B":"rgba(148,163,184,0.65)", fontFamily:"Poppins, sans-serif" }}>{day}</span>
                    {events && <div style={{ display:"flex", gap:2 }}>{events.slice(0,2).map((ev,ei)=><span key={ei} style={{ width:5, height:5, borderRadius:"50%", background:ev.color, boxShadow:`0 0 4px ${ev.color}` }}/>)}</div>}
                  </>}
                </div>
              );
            })}
          </div>
        </div>
      </GlassCard>
      <GlassCard>
        <div style={{ padding:"16px 20px" }}>
          <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(148,163,184,0.3)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif", marginBottom:12 }}>PRÓXIMAS EVALUACIONES</p>
          <div style={{ display:"flex", flexDirection:"column", gap:8 }}>
            {[
              { day:"01 sep", label:"EVA-0892 – Restaurante El Buen Sabor · 09:00", color:"#3BF6E5" },
              { day:"01 sep", label:"EVA-0893 – Farmacia Central Esperanza · 11:30", color:"#E53BF6" },
              { day:"02 sep", label:"EVA-0895 – Almacén Don Carlos S.A. · 14:30", color:"#3BF6E5" },
              { day:"05 sep", label:"EVA-0896 – Pendiente confirmación · TBD", color:"#22c55e" },
              { day:"08 sep", label:"EVA-0897 – Por asignar", color:"#3BF6E5" },
            ].map(ev=>(
              <div key={ev.day+ev.label} style={{ display:"flex", alignItems:"center", gap:10 }}>
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

/* ── Section: Reportes ──────────────────────────────────── */
function ReportesSection({ onToast }: { onToast:(m:string)=>void }) {
  const overdue = INFORMES.filter(r=>r.overdue);
  const recent  = INFORMES.filter(r=>!r.overdue);
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <SectionHeader title="Pendientes de Informe" subtitle={`${INFORMES.length} reportes · ${overdue.length} vencidos`} />
      {overdue.length > 0 && <>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(229,59,246,0.5)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif" }}>⚠ VENCIDOS</p>
        {overdue.map(r=><ReportCard key={r.id} rep={r} onToast={onToast}/>)}
      </>}
      {recent.length > 0 && <>
        <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.1em", fontFamily:"Poppins, sans-serif", marginTop:8 }}>RECIENTES</p>
        {recent.map(r=><ReportCard key={r.id} rep={r} onToast={onToast}/>)}
      </>}
    </div>
  );
}

/* ── Section: Configuración ─────────────────────────────── */
function ConfiguracionSection({ userName, onToast }:{ userName:string; onToast:(m:string)=>void }) {
  const [nombre,   setNombre]   = useState(userName);
  const [correo,   setCorreo]   = useState("tecnico@evalia.com");
  const [telefono, setTelefono] = useState("+1 (809) 555-0210");
  const [online,   setOnline]   = useState(true);
  const [notifNew, setNotifNew] = useState(true);
  const [notifOv,  setNotifOv]  = useState(true);

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16, maxWidth:680 }}>
      <SectionHeader title="Configuración" subtitle="Preferencias de la cuenta" />
      <GlassCard accent="#3BF6E5">
        <div style={{ padding:"20px" }}>
          <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(59,246,229,0.5)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>PERFIL DE TÉCNICO</p>
          {[{label:"Nombre completo",v:nombre,s:setNombre},{label:"Correo electrónico",v:correo,s:setCorreo},{label:"Teléfono",v:telefono,s:setTelefono}].map(f=>(
            <div key={f.label} style={{ position:"relative", marginBottom:11 }}>
              <input value={f.v} onChange={e=>f.s(e.target.value)} style={{ width:"100%", padding:"1.2rem 1rem 0.5rem", borderRadius:11, outline:"none", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(59,246,229,0.2)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"#f1f5f9", fontFamily:"Poppins, sans-serif", fontSize:"0.875rem", boxSizing:"border-box" as const }}/>
              <label style={{ position:"absolute", top:"0.5rem", left:"1rem", fontSize:"0.55rem", fontWeight:700, color:"rgba(59,246,229,0.6)", textTransform:"uppercase", letterSpacing:"0.08em", fontFamily:"Poppins, sans-serif", pointerEvents:"none" }}>{f.label}</label>
            </div>
          ))}
          <button onClick={()=>onToast("Perfil actualizado correctamente")} style={{ marginTop:4, width:"100%", padding:"11px 0", borderRadius:12, cursor:"pointer", background:"linear-gradient(135deg,#3BF6E5,#06b6d4)", border:"none", color:"#0F172A", fontFamily:"Poppins, sans-serif", fontWeight:800, fontSize:"0.85rem" }}>Guardar cambios</button>
        </div>
      </GlassCard>

      <GlassCard accent="#F6E53B">
        <div style={{ padding:"20px" }}>
          <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(246,229,59,0.5)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>ESTADO PWA</p>
          <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center" }}>
            <div>
              <p style={{ fontSize:"0.82rem", fontWeight:600, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>Disponibilidad en campo</p>
              <p style={{ fontSize:"0.65rem", color:"rgba(148,163,184,0.4)", fontFamily:"Poppins, sans-serif" }}>{online?"Apareces como disponible para nuevas asignaciones":"Marcado como no disponible"}</p>
            </div>
            <button onClick={()=>setOnline(!online)} style={{ width:52, height:28, borderRadius:99, cursor:"pointer", border:"none", background:online?"linear-gradient(135deg,#22c55e,#16a34a)":"rgba(255,255,255,0.08)", position:"relative", transition:"all 0.2s ease" }}>
              <span style={{ position:"absolute", top:4, left:online?26:4, width:20, height:20, borderRadius:"50%", background:"white", transition:"left 0.2s ease" }}/>
            </button>
          </div>
        </div>
      </GlassCard>

      <GlassCard accent="#E53BF6">
        <div style={{ padding:"20px" }}>
          <p style={{ fontSize:"0.6rem", fontWeight:700, color:"rgba(229,59,246,0.5)", textTransform:"uppercase", letterSpacing:"0.12em", fontFamily:"Poppins, sans-serif", marginBottom:14 }}>NOTIFICACIONES</p>
          {[{label:"Nuevas evaluaciones asignadas",v:notifNew,s:setNotifNew},{label:"Informes vencidos",v:notifOv,s:setNotifOv}].map(n=>(
            <div key={n.label} style={{ display:"flex", justifyContent:"space-between", alignItems:"center", padding:"10px 0", borderBottom:"1px solid rgba(255,255,255,0.05)" }}>
              <span style={{ fontSize:"0.78rem", color:"rgba(148,163,184,0.65)", fontFamily:"Poppins, sans-serif" }}>{n.label}</span>
              <button onClick={()=>n.s(!n.v)} style={{ width:44, height:24, borderRadius:99, cursor:"pointer", border:"none", background:n.v?"linear-gradient(135deg,#E53BF6,#8b5cf6)":"rgba(255,255,255,0.08)", position:"relative", transition:"all 0.2s ease" }}>
                <span style={{ position:"absolute", top:3, left:n.v?22:3, width:18, height:18, borderRadius:"50%", background:"white", transition:"left 0.2s ease" }}/>
              </button>
            </div>
          ))}
        </div>
      </GlassCard>

      <button onClick={()=>onToast("Contraseña: revisa tu correo")} style={{ padding:"12px 0", borderRadius:12, cursor:"pointer", background:"rgba(255,255,255,0.04)", borderTop:"1px solid rgba(255,255,255,0.09)", borderRight:"1px solid rgba(255,255,255,0.09)", borderBottom:"1px solid rgba(255,255,255,0.09)", borderLeft:"1px solid rgba(255,255,255,0.09)", color:"rgba(148,163,184,0.5)", fontFamily:"Poppins, sans-serif", fontSize:"0.8rem", fontWeight:600 }}>Cambiar contraseña</button>
    </div>
  );
}

/* ══ Main Component ═════════════════════════════════════════ */
export default function TechnicianDashboard({
  onBack, userName = "Carlos Méndez",
}: {
  onBack?: () => void;
  userName?: string;
}) {
  const width    = useWidth();
  const isMobile = width < 768;
  const isTablet = width >= 768 && width < 1100;
  const expanded = !isMobile && !isTablet;

  const [section,    setSection]   = useState<Section>("inicio");
  const [drawerOpen, setDrawer]    = useState(false);
  const [toast,      setToast]     = useState<string|null>(null);

  const showToast = (msg:string) => { setToast(msg); setTimeout(()=>setToast(null),2800); };

  const currentNav = NAV.find(n=>n.id===section)!;
  const totalBadge = NAV.reduce((s,n)=>s+(n.badge??0),0);

  return (
    <>
      <style>{`
        @keyframes tdPulse { 0%,100%{transform:scale(1);opacity:1} 50%{transform:scale(1.5);opacity:0.5} }
        @keyframes tdToast { from{opacity:0;transform:translateY(10px) scale(0.96)} to{opacity:1;transform:none} }
        .hide-scroll::-webkit-scrollbar{display:none}
        .hide-scroll{-ms-overflow-style:none;scrollbar-width:none}
      `}</style>

      <div style={{ display:"flex", height:"100svh", background:"#0F172A", fontFamily:"Poppins, sans-serif", overflow:"hidden", position:"relative" }}>
        <div className="mesh-orb"            style={{ width:450, height:450, background:"rgba(59,246,229,0.14)",  top:"-100px", left:"-60px", zIndex:0 }}/>
        <div className="mesh-orb mesh-orb-2" style={{ width:380, height:380, background:"rgba(246,229,59,0.1)",   bottom:"-80px", right:"-60px", zIndex:0 }}/>
        <div className="mesh-orb mesh-orb-3" style={{ width:220, height:220, background:"rgba(229,59,246,0.1)",   top:"40%", left:"45%", zIndex:0 }}/>

        {isMobile && drawerOpen && <div onClick={()=>setDrawer(false)} style={{ position:"fixed", inset:0, background:"rgba(0,0,0,0.65)", zIndex:40, backdropFilter:"blur(3px)" }}/>}

        {/* ── Sidebar ── */}
        <aside style={{ position:"relative", top:0, left:0, height:"100%", width:isTablet?64:220, flexShrink:0, zIndex:10, transition:"width 0.25s ease", background:"rgba(7,12,24,0.96)", backdropFilter:"blur(36px)", WebkitBackdropFilter:"blur(36px)", borderRight:"1px solid rgba(255,255,255,0.07)", display:isMobile?"none":"flex", flexDirection:"column" }}>

          <div style={{ padding:expanded?"20px 18px 16px":"18px 0 14px", display:"flex", alignItems:"center", gap:11, justifyContent:expanded?"flex-start":"center", borderBottom:"1px solid rgba(255,255,255,0.06)", flexShrink:0 }}>
            <div style={{ width:36, height:36, borderRadius:10, background:"linear-gradient(135deg,rgba(59,246,229,0.35),rgba(246,229,59,0.25))", borderTop:"1.5px solid rgba(59,246,229,0.5)", borderRight:"1.5px solid rgba(246,229,59,0.4)", borderBottom:"1.5px solid rgba(246,229,59,0.4)", borderLeft:"1.5px solid rgba(59,246,229,0.5)", display:"flex", alignItems:"center", justifyContent:"center", flexShrink:0 }}>
              <span style={{ fontSize:"0.75rem", fontWeight:900, background:"linear-gradient(135deg,#3BF6E5,#F6E53B)", WebkitBackgroundClip:"text", WebkitTextFillColor:"transparent", backgroundClip:"text" }}>TE</span>
            </div>
            {expanded && <div><p style={{ fontSize:"0.8rem", fontWeight:700, color:"#f1f5f9" }}>Técnico Evaluador</p><p style={{ fontSize:"0.52rem", color:"rgba(148,163,184,0.3)", textTransform:"uppercase", letterSpacing:"0.07em" }}>Evalia BPM</p></div>}
          </div>

          <nav style={{ flex:1, padding:expanded?"14px 10px":"14px 0", display:"flex", flexDirection:"column", gap:2, overflowY:"auto" }} className="hide-scroll">
            {expanded && <p style={{ fontSize:"0.52rem", fontWeight:700, color:"rgba(148,163,184,0.22)", letterSpacing:"0.14em", textTransform:"uppercase", padding:"0 6px 8px" }}>NAVEGACIÓN</p>}
            {NAV.map(item=>{
              const active=section===item.id;
              const color=active?"#3BF6E5":"rgba(148,163,184,0.4)";
              return (
                <button key={item.id} onClick={()=>{ setSection(item.id); if(isMobile)setDrawer(false); }} title={!expanded?item.label:undefined}
                  style={{ display:"flex", alignItems:"center", gap:10, padding:expanded?"9px 12px":"10px", justifyContent:expanded?"flex-start":"center", borderRadius:10, border:"none", cursor:"pointer", background:active?"rgba(59,246,229,0.1)":"transparent", color, fontFamily:"Poppins, sans-serif", fontWeight:active?600:400, fontSize:"0.78rem", transition:"all 0.18s ease", boxShadow:active?"inset 0 0 0 1px rgba(59,246,229,0.18)":"none", position:"relative", width:"100%" }}>
                  {active && <div style={{ position:"absolute", left:-10, top:"50%", transform:"translateY(-50%)", width:3, height:16, borderRadius:99, background:"#3BF6E5", boxShadow:"0 0 10px #3BF6E5" }}/>}
                  <span>{item.icon(active?"#3BF6E5":"rgba(148,163,184,0.4)")}</span>
                  {expanded && <span style={{ flex:1, textAlign:"left" }}>{item.label}</span>}
                  {expanded && item.badge ? <span style={{ padding:"1px 6px", borderRadius:99, background:"rgba(59,246,229,0.15)", borderTop:"1px solid rgba(59,246,229,0.35)", borderRight:"1px solid rgba(59,246,229,0.35)", borderBottom:"1px solid rgba(59,246,229,0.35)", borderLeft:"1px solid rgba(59,246,229,0.35)", fontSize:"0.5rem", fontWeight:800, color:"#3BF6E5" }}>{item.badge}</span> : null}
                  {!expanded && item.badge ? <span style={{ position:"absolute", top:6, right:6, width:7, height:7, borderRadius:"50%", background:"#3BF6E5" }}/> : null}
                </button>
              );
            })}
          </nav>

          <div style={{ padding:expanded?"12px 10px":"12px 0", borderTop:"1px solid rgba(255,255,255,0.06)", flexShrink:0 }}>
            {expanded && <div style={{ padding:"8px 10px", borderRadius:10, background:"rgba(255,255,255,0.03)", display:"flex", alignItems:"center", gap:9, marginBottom:8 }}>
              <div style={{ width:28, height:28, borderRadius:8, background:"linear-gradient(135deg,#3BF6E5,#F6E53B)", padding:1.5, flexShrink:0 }}>
                <div style={{ width:"100%", height:"100%", borderRadius:6, background:"#1e293b", display:"flex", alignItems:"center", justifyContent:"center", fontSize:"0.65rem", fontWeight:800, color:"#f1f5f9" }}>{userName[0]}</div>
              </div>
              <div style={{ flex:1, minWidth:0 }}>
                <p style={{ fontSize:"0.72rem", fontWeight:700, color:"#f1f5f9", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }}>{userName}</p>
                <p style={{ fontSize:"0.52rem", color:"rgba(148,163,184,0.35)" }}>Técnico Evaluador</p>
              </div>
            </div>}
            <button onClick={onBack} title={!expanded?"Salir":undefined} style={{ display:"flex", alignItems:"center", gap:9, padding:expanded?"9px 10px":"10px", borderRadius:10, border:"none", cursor:"pointer", background:"rgba(59,246,229,0.06)", borderTop:"1px solid rgba(59,246,229,0.18)", borderRight:"1px solid rgba(59,246,229,0.18)", borderBottom:"1px solid rgba(59,246,229,0.18)", borderLeft:"1px solid rgba(59,246,229,0.18)", color:"#3BF6E5", fontFamily:"Poppins, sans-serif", fontSize:"0.72rem", fontWeight:600, justifyContent:expanded?"flex-start":"center", width:"100%" }}>
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#3BF6E5" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
              {expanded && "Cerrar sesión"}
            </button>
          </div>
        </aside>

        {/* ── Main ── */}
        <main style={{ flex:1, display:"flex", flexDirection:"column", overflow:"hidden", position:"relative", zIndex:1, minWidth:0 }}>
          <header style={{ flexShrink:0, display:"flex", alignItems:"center", justifyContent:"space-between", padding:isMobile?"13px 16px":"14px 28px", background:"rgba(7,12,24,0.85)", backdropFilter:"blur(24px)", WebkitBackdropFilter:"blur(24px)", borderBottom:"1px solid rgba(255,255,255,0.06)" }}>
            <div style={{ display:"flex", alignItems:"center", gap:12 }}>
              <div>
                <p style={{ fontSize:"0.56rem", fontWeight:700, color:"rgba(59,246,229,0.5)", letterSpacing:"0.12em", textTransform:"uppercase", fontFamily:"Poppins, sans-serif" }}>EVALIA · TÉCNICO</p>
                <h1 style={{ fontSize:isMobile?"1rem":"1.1rem", fontWeight:800, color:"#f1f5f9", letterSpacing:"-0.02em", fontFamily:"Poppins, sans-serif" }}>{currentNav.label}</h1>
              </div>
            </div>
            <div style={{ display:"flex", alignItems:"center", gap:8 }}>
              <div style={{ display:"flex", alignItems:"center", gap:6, padding:"5px 11px", borderRadius:99, background:"rgba(34,197,94,0.08)", borderTop:"1px solid rgba(34,197,94,0.25)", borderRight:"1px solid rgba(34,197,94,0.25)", borderBottom:"1px solid rgba(34,197,94,0.25)", borderLeft:"1px solid rgba(34,197,94,0.25)" }}>
                <span style={{ width:6, height:6, borderRadius:"50%", background:"#22c55e", boxShadow:"0 0 6px #22c55e", display:"inline-block", animation:"tdPulse 2.5s ease-in-out infinite" }}/>
                <span style={{ fontSize:"0.6rem", fontWeight:700, color:"#22c55e", fontFamily:"Poppins, sans-serif" }}>Online</span>
              </div>
              {isMobile && (
                <button onClick={onBack} title="Cerrar sesión" style={{ width:36, height:36, borderRadius:10, background:"rgba(59,246,229,0.08)", border:"1px solid rgba(59,246,229,0.25)", display:"flex", alignItems:"center", justifyContent:"center", cursor:"pointer" }}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" stroke="#3BF6E5" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"/></svg>
                </button>
              )}
            </div>
          </header>

          <div key={section} className="hide-scroll" style={{ flex:1, overflowY:"auto", padding:isMobile?"16px 14px 84px":"24px 28px 36px" }}>
            {section==="inicio"        && <InicioSection onNavigate={setSection} onToast={showToast}/>}
            {section==="evaluaciones"  && <EvaluacionesSection onToast={showToast}/>}
            {section==="calendario"    && <CalendarioSection/>}
            {section==="reportes"      && <ReportesSection onToast={showToast}/>}
            {section==="configuracion" && <ConfiguracionSection userName={userName} onToast={showToast}/>}
          </div>
        </main>

        {/* Bottom tab bar — mobile only */}
        {isMobile && (
          <div style={{ position:"fixed", bottom:0, left:0, right:0, zIndex:60, background:"rgba(7,12,24,0.97)", backdropFilter:"blur(28px)", WebkitBackdropFilter:"blur(28px)", borderTop:"1px solid rgba(255,255,255,0.09)", display:"flex", alignItems:"stretch", height:66, paddingBottom:"env(safe-area-inset-bottom,0px)" }}>
            {([
              { id:"inicio"       as Section, label:"Inicio",   badge:0 },
              { id:"evaluaciones" as Section, label:"Eval.",    badge:3 },
              { id:"calendario"   as Section, label:"Agenda",   badge:0 },
              { id:"reportes"     as Section, label:"Informes", badge:3 },
              { id:"configuracion"as Section, label:"Config.",  badge:0 },
            ]).map(tab=>{
              const active = section===tab.id;
              const navItem = NAV.find(n=>n.id===tab.id)!;
              const accent = "#3BF6E5";
              return (
                <button key={tab.id} onClick={()=>setSection(tab.id)} style={{ flex:1, display:"flex", flexDirection:"column", alignItems:"center", justifyContent:"center", gap:3, border:"none", cursor:"pointer", background:"transparent", color:active?accent:"rgba(148,163,184,0.4)", position:"relative", paddingTop:4 }}>
                  {active && <div style={{ position:"absolute", top:0, left:"50%", transform:"translateX(-50%)", width:30, height:2.5, borderRadius:"0 0 3px 3px", background:accent, boxShadow:`0 0 8px ${accent}99` }}/>}
                  <div style={{ position:"relative" }}>
                    {navItem.icon(active?accent:"rgba(148,163,184,0.4)")}
                    {tab.badge>0 && !active && <span style={{ position:"absolute", top:-3, right:-5, minWidth:13, height:13, borderRadius:99, background:accent, color:"#0F172A", fontSize:"0.4rem", fontWeight:900, display:"flex", alignItems:"center", justifyContent:"center", padding:"0 2px" }}>{tab.badge}</span>}
                  </div>
                  <span style={{ fontSize:"0.5rem", fontWeight:active?700:400, fontFamily:"Poppins, sans-serif", letterSpacing:"0.02em" }}>{tab.label}</span>
                </button>
              );
            })}
          </div>
        )}

        {toast && <div style={{ position:"fixed", bottom:isMobile?74:24, left:"50%", transform:"translateX(-50%)", zIndex:90, display:"flex", alignItems:"center", gap:10, padding:"11px 18px", borderRadius:13, whiteSpace:"nowrap", background:"rgba(8,14,28,0.97)", backdropFilter:"blur(24px)", borderTop:"1px solid rgba(59,246,229,0.4)", borderRight:"1px solid rgba(59,246,229,0.4)", borderBottom:"1px solid rgba(59,246,229,0.4)", borderLeft:"1px solid rgba(59,246,229,0.4)", boxShadow:"0 16px 48px rgba(0,0,0,0.6)", animation:"tdToast 0.3s cubic-bezier(.22,1,.36,1) both" }}>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none"><path d="M20 6L9 17l-5-5" stroke="#3BF6E5" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
          <span style={{ fontSize:"0.78rem", fontWeight:600, color:"#f1f5f9", fontFamily:"Poppins, sans-serif" }}>{toast}</span>
        </div>}
      </div>
    </>
  );
}

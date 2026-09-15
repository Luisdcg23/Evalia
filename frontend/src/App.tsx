import { useState, useEffect } from "react";
import evaliaLogo from "@/imports/Disen_o_sin_ti_tulo.png";
import ResponsiveDashboard, { UserInfo } from "./ResponsiveDashboard";
import RiskEngine from "./RiskEngine";
import ForgotPassword from "./ForgotPassword";
import ChangePassword from "./ChangePassword";
import UserRegistration from "./UserRegistration";
import RegistrationPending from "./RegistrationPending";
import UserValidationModal from "./UserValidationModal";
import CoordinatorDashboard from "./CoordinatorDashboard";
import TechnicianDashboard from "./TechnicianDashboard";
import CompanyPortal from "./CompanyPortal";
import { login, logout } from "./auth/api";
import { clearSession, loadSession, saveSession } from "./auth/session";
import CompanyManagement from "./features/companies/CompanyManagement";

type AppView = "dashboard" | "risk" | "coordinator-dashboard" | "technician-dashboard" | "company-portal" | "user-validation" | "data-management";
const initialViewForRole = (role: string): AppView => {
  if (role === "Coordinador") return "coordinator-dashboard";
  if (role === "Técnico Evaluador") return "technician-dashboard";
  if (role === "Admin de Empresa" || role === "Usuario Delegado") return "company-portal";
  return "dashboard";
};

/* ── Responsive hook ──────────────────────────────────────── */
function useWidth() {
  const [w, setW] = useState(() => window.innerWidth);
  useEffect(() => {
    const fn = () => setW(window.innerWidth);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return w;
}

/* ── Floating label input ─────────────────────────────────── */
function FloatingInput({
  id, label, type = "text", value, onChange, disabled,
}: {
  id: string; label: string; type?: string;
  value: string; onChange: (v: string) => void; disabled?: boolean;
}) {
  const [focused, setFocused] = useState(false);
  const active = focused || value.length > 0;

  return (
    <div style={{ position: "relative" }}>
      <input
        id={id}
        type={type}
        value={value}
        disabled={disabled}
        autoComplete={type === "password" ? "current-password" : "email"}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          background: focused ? "rgba(229,59,246,0.06)" : "rgba(255,255,255,0.04)",
          borderTop: focused ? "1px solid rgba(229,59,246,0.6)" : "1px solid rgba(255,255,255,0.1)",
          borderRight: focused ? "1px solid rgba(229,59,246,0.6)" : "1px solid rgba(255,255,255,0.1)",
          borderBottom: focused ? "1px solid rgba(229,59,246,0.6)" : "1px solid rgba(255,255,255,0.1)",
          borderLeft: focused ? "1px solid rgba(229,59,246,0.6)" : "1px solid rgba(255,255,255,0.1)",
          boxShadow: focused ? "0 0 0 3px rgba(229,59,246,0.12)" : "none",
          color: "#f1f5f9",
          fontFamily: "Poppins, sans-serif",
          fontSize: "0.9375rem",
          padding: "1.25rem 1.125rem 0.5rem",
          borderRadius: 12,
          outline: "none",
          transition: "all 0.22s ease",
          boxSizing: "border-box",
          opacity: disabled ? 0.6 : 1,
        }}
      />
      <label
        htmlFor={id}
        style={{
          position: "absolute",
          left: "1.125rem",
          top: active ? "0.6rem" : "50%",
          transform: active ? "none" : "translateY(-50%)",
          fontSize: active ? "0.6875rem" : "0.9375rem",
          fontWeight: active ? 500 : 400,
          color: active ? "rgba(229,59,246,0.9)" : "rgba(148,163,184,0.8)",
          letterSpacing: active ? "0.04em" : "normal",
          pointerEvents: "none",
          transition: "all 0.2s ease",
          fontFamily: "Poppins, sans-serif",
        }}
      >
        {label}
      </label>
    </div>
  );
}

/* ── Passkey icon ─────────────────────────────────────────── */
function PasskeyIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
      <defs>
        <linearGradient id="pkG" x1="0" y1="0" x2="24" y2="24" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor="#E53BF6"/>
          <stop offset="100%" stopColor="#3BF6E5"/>
        </linearGradient>
      </defs>
      <circle cx="9" cy="7" r="4" stroke="url(#pkG)" strokeWidth="1.75" fill="none"/>
      <path d="M3 21v-2a6 6 0 016-6h2" stroke="url(#pkG)" strokeWidth="1.75" strokeLinecap="round"/>
      <path d="M17 12a3 3 0 110 6 3 3 0 010-6Z" stroke="url(#pkG)" strokeWidth="1.75" fill="none"/>
      <path d="M20 18l2 2" stroke="url(#pkG)" strokeWidth="1.75" strokeLinecap="round"/>
    </svg>
  );
}

/* ── App ──────────────────────────────────────────────────── */
export default function App() {
  const width     = useWidth();
  const isMobile  = width < 768;

  const [loggedUser, setLoggedUser] = useState<UserInfo | null>(() => loadSession()?.user ?? null);
  const [view,      setView]      = useState<AppView>(() => {
    const session = loadSession();
    return session ? initialViewForRole(session.user.role) : "dashboard";
  });
  const [authView,  setAuthView]  = useState<"login" | "forgot" | "change-password" | "register" | "registration-pending">("login");
  const [email,    setEmail]    = useState("");
  const [password, setPassword] = useState("");
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState("");
  const [recovery, setRecovery] = useState<{ email: string; code: string } | null>(null);

  if (!loggedUser && authView === "forgot") {
    return (
      <ForgotPassword
        onBack={() => setAuthView("login")}
        onChangePassword={(recoveryEmail, recoveryCode) => {
          setRecovery({ email: recoveryEmail, code: recoveryCode });
          setAuthView("change-password");
        }}
      />
    );
  }

  if (!loggedUser && authView === "change-password") {
    if (!recovery) {
      return null;
    }
    return <ChangePassword onBack={() => setAuthView("login")} email={recovery.email} recoveryCode={recovery.code} />;
  }

  if (!loggedUser && authView === "register") {
    return (
      <UserRegistration
        onBack={() => setAuthView("login")}
        onSuccess={() => setAuthView("registration-pending")}
      />
    );
  }

  if (!loggedUser && authView === "registration-pending") {
    return <RegistrationPending onBack={() => setAuthView("login")} />;
  }

  const doLogout = () => {
    const session = loadSession();
    clearSession();
    if (session) void logout(session.accessToken, session.refreshToken);
    setLoggedUser(null);
    setEmail("");
    setPassword("");
    setView("dashboard");
  };

  if (loggedUser) {
    if (view === "user-validation") {
      return <UserValidationModal accessToken={loadSession()?.accessToken ?? ""} onClose={() => setView("dashboard")} />;
    }
    if (view === "data-management") {
      return <CompanyManagement accessToken={loadSession()?.accessToken ?? ""} onBack={() => setView("dashboard")} />;
    }
    if (view === "risk") {
      return <RiskEngine onBack={() => setView(initialViewForRole(loggedUser.role))} />;
    }
    if (view === "coordinator-dashboard") {
      return (
        <CoordinatorDashboard
          userName={loggedUser.name}
          onBack={doLogout}
          onCompanyProfile={() => {}}
          accessToken={loadSession()?.accessToken ?? ""}
        />
      );
    }
    if (view === "technician-dashboard") {
      return (
        <TechnicianDashboard
          userName={loggedUser.name}
          onBack={doLogout}
          accessToken={loadSession()?.accessToken ?? ""}
        />
      );
    }
    if (view === "company-portal") {
      return (
        <CompanyPortal
          userName={loggedUser.name}
          accessToken={loadSession()?.accessToken ?? ""}
          onBack={doLogout}
        />
      );
    }
    /* Administrador del Sistema fallback */
    return (
      <ResponsiveDashboard
        user={loggedUser}
        accessToken={loadSession()?.accessToken ?? ""}
        onBack={doLogout}
        onRisk={() => setView("risk")}
        onCompanyProfile={() => {}}
        onCompanyForm={() => setView("data-management")}
        onCompanyDashboard={() => {}}
        onCoordinatorDashboard={() => setView("coordinator-dashboard")}
        onUserValidation={() => setView("user-validation")}
      />
    );
  }

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const session = await login(email, password);
      saveSession(session);
      setLoggedUser(session.user);
      setView(initialViewForRole(session.user.role));
    } catch (loginError) {
      setError(loginError instanceof Error ? loginError.message : "No fue posible iniciar sesión.");
    } finally {
      setLoading(false);
    }
  };

  /* Layout values */
  const cardPad   = isMobile ? "28px 20px 24px" : "40px 36px 36px";
  const logoH     = isMobile ? 48 : 58;
  const headingFs = isMobile ? "1.4rem" : "1.65rem";

  return (
    <div
      className="mesh-bg"
      style={{
        minHeight: "100svh",
        display: "flex",
        alignItems: isMobile ? "flex-start" : "center",
        justifyContent: "center",
        padding: isMobile ? "0" : "32px 20px",
        position: "relative",
        overflow: "hidden",
      }}
    >
      {/* Mesh orbs */}
      <div className="mesh-orb"      style={{ width: isMobile ? 340 : 500, height: isMobile ? 340 : 500, background: "rgba(229,59,246,0.5)",  top: "-100px", left: "-80px" }}/>
      <div className="mesh-orb mesh-orb-2" style={{ width: isMobile ? 300 : 420, height: isMobile ? 300 : 420, background: "rgba(59,246,229,0.38)", bottom: "-80px", right: "-60px" }}/>
      <div className="mesh-orb mesh-orb-3" style={{ width: isMobile ? 220 : 300, height: isMobile ? 220 : 300, background: "rgba(140,59,246,0.32)", top: "45%", left: "38%" }}/>

      {/* Grain */}
      <div className="absolute inset-0 opacity-[0.055]" style={{ backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.85' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`, backgroundSize: "180px", pointerEvents: "none" }}/>

      {/* Card */}
      <div
        className="glass-card animate-fade-in"
        style={{
          position: "relative",
          zIndex: 1,
          width: "100%",
          maxWidth: 480,
          minHeight: isMobile ? "100svh" : "auto",
          borderRadius: isMobile ? 0 : 28,
          padding: cardPad,
          display: "flex",
          flexDirection: "column",
          justifyContent: isMobile ? "center" : "flex-start",
          /* on mobile override the glass-card rounded corners */
          ...(isMobile ? { borderTop: "none", borderBottom: "none", borderLeft: "none", borderRight: "none" } : {}),
        }}
      >
        {/* Logo */}
        <div className="animate-fade-in-up" style={{ display: "flex", justifyContent: "center", marginBottom: 28 }}>
          <img src={evaliaLogo} alt="Evalia" style={{ height: logoH, width: "auto" }}/>
        </div>

        {/* Heading */}
        <div className="animate-fade-in-up delay-100" style={{ textAlign: "center", marginBottom: 28 }}>
          <h2 style={{ fontSize: headingFs, fontWeight: 800, color: "#f1f5f9", letterSpacing: "-0.02em", marginBottom: 6, fontFamily: "Poppins, sans-serif" }}>
            Bienvenido de vuelta
          </h2>
          <p style={{ fontSize: "0.8rem", color: "rgba(148,163,184,0.75)", fontWeight: 300, fontFamily: "Poppins, sans-serif" }}>
            Inicia sesión en tu espacio de trabajo
          </p>
        </div>

        {/* Form */}
        <form onSubmit={handleLogin} style={{ display: "flex", flexDirection: "column", gap: 14 }} className="animate-fade-in-up delay-200">
          <FloatingInput id="email" label="Correo electrónico" type="email" value={email} onChange={(v) => { setEmail(v); setError(""); }} disabled={loading}/>
          <FloatingInput id="password" label="Contraseña" type="password" value={password} onChange={(v) => { setPassword(v); setError(""); }} disabled={loading}/>

          {/* Error message */}
          {error && (
            <div style={{ display: "flex", alignItems: "center", gap: 7, padding: "10px 14px", borderRadius: 10, background: "rgba(229,59,246,0.08)", borderTop: "1px solid rgba(229,59,246,0.25)", borderRight: "1px solid rgba(229,59,246,0.25)", borderBottom: "1px solid rgba(229,59,246,0.25)", borderLeft: "1px solid rgba(229,59,246,0.25)" }}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none"><path d="M12 9v4M12 17h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="#E53BF6" strokeWidth="2" strokeLinecap="round"/></svg>
              <span style={{ fontSize: "0.75rem", color: "#E53BF6", fontFamily: "Poppins, sans-serif", fontWeight: 500 }}>{error}</span>
            </div>
          )}

          {/* Forgot */}
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <button type="button" onClick={() => setAuthView("forgot")} style={{ fontSize: "0.78rem", fontWeight: 500, background: "linear-gradient(135deg, #E53BF6, #3BF6E5)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text", border: "none", cursor: "pointer", fontFamily: "Poppins, sans-serif", opacity: 0.85 }}>
              ¿Olvidaste tu contraseña?
            </button>
          </div>

          {/* Submit */}
          <button
            type="submit"
            disabled={loading}
            className="gradient-btn"
            style={{ width: "100%", padding: isMobile ? "14px 0" : "15px 0", borderRadius: 14, color: "white", fontWeight: 700, fontSize: "0.95rem", fontFamily: "Poppins, sans-serif", border: "none", cursor: loading ? "not-allowed" : "pointer", display: "flex", alignItems: "center", justifyContent: "center", gap: 8, letterSpacing: "0.04em", opacity: loading ? 0.8 : 1, marginTop: 4 }}
          >
            {loading ? (
              <>
                <span style={{ display: "inline-block", width: 16, height: 16, borderRadius: "50%", borderTop: "2px solid white", borderRight: "2px solid rgba(255,255,255,0.3)", borderBottom: "2px solid rgba(255,255,255,0.3)", borderLeft: "2px solid rgba(255,255,255,0.3)", animation: "spin 0.7s linear infinite" }}/>
                Autenticando…
              </>
            ) : "Iniciar sesión"}
          </button>
        </form>

        {/* Divider */}
        <div className="animate-fade-in-up delay-300" style={{ display: "flex", alignItems: "center", gap: 12, margin: "22px 0" }}>
          <div style={{ flex: 1, height: 1, background: "rgba(255,255,255,0.07)" }}/>
          <span style={{ fontSize: "0.7rem", color: "rgba(148,163,184,0.45)", fontFamily: "Poppins, sans-serif" }}>o continúa con</span>
          <div style={{ flex: 1, height: 1, background: "rgba(255,255,255,0.07)" }}/>
        </div>

        {/* Passkey */}
        <button
          type="button"
          className="passkey-btn animate-fade-in-up delay-400"
          style={{ width: "100%", padding: isMobile ? "13px 0" : "14px 0", borderRadius: 14, display: "flex", alignItems: "center", justifyContent: "center", gap: 10, color: "#cbd5e1", fontSize: "0.82rem", fontWeight: 500, fontFamily: "Poppins, sans-serif", cursor: "pointer" }}
        >
          <PasskeyIcon/>
          Iniciar sesión con Biometría / Passkey
        </button>

        {/* Sign-up footer */}
        <p className="animate-fade-in-up delay-500" style={{ marginTop: 22, fontSize: "0.75rem", color: "rgba(148,163,184,0.5)", textAlign: "center", fontFamily: "Poppins, sans-serif" }}>
          ¿No tienes cuenta?{" "}
          <button
            type="button"
            onClick={() => setAuthView("register")}
            style={{ fontWeight: 600, background: "linear-gradient(135deg, #E53BF6, #3BF6E5)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent", backgroundClip: "text", border: "none", cursor: "pointer", fontFamily: "Poppins, sans-serif", fontSize: "0.75rem" }}
          >
            Solicitar acceso
          </button>
        </p>

      </div>
    </div>
  );
}

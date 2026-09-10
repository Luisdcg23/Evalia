# Evalia — Evaluación Basada en Riesgo

Aplicación web local para gestionar evaluaciones basadas en riesgo e inspecciones de Buenas Prácticas de Manufactura (BPM). El proyecto usa una API ASP.NET Core, una interfaz React y PostgreSQL. Su ejecución está diseñada para Windows y no requiere Docker.

> Estado actual: versión en desarrollo. Existen módulos funcionales comprobables, pero todavía no está implementado el ciclo operativo completo hasta el cierre oficial del expediente.

## Tecnologías

- .NET SDK 9 y ASP.NET Core
- Entity Framework Core
- PostgreSQL 18 y pgAdmin 4
- React 19, TypeScript y Vite
- Node.js 20, 22 o 24 y pnpm
- MinIO local para el almacenamiento de evidencias
- QuestPDF (licencia Community) para el informe oficial en PDF
- xUnit y Vitest

## Funcionalidades disponibles

- Autenticación, recuperación y cambio de contraseña.
- Sesiones JWT con rotación de refresh tokens.
- Autorización para administrador, administrador de empresa, usuario delegado, coordinador y técnico evaluador.
- Registro y aprobación administrativa de usuarios.
- Empresas, representantes y aislamiento de información por empresa.
- Catálogos de alimentos, niveles y factores de riesgo.
- Cálculo reproducible del riesgo y matriz de frecuencia de inspección.
- Plantillas EBR versionadas con una estructura jerárquica editable.
- Solicitudes BPM en borrador y envío.
- Creación idempotente de expedientes e historial de estados.
- Registro y decisión de alertas LAPCH y denuncias.
- Asignación y reasignación de técnico evaluador con historial completo.
- Programación, reprogramación, cancelación y agenda de evaluaciones.
- Ejecución de la evaluación en campo con autosave por pregunta y envío que la
  bloquea de forma inmutable.
- Cálculo del porcentaje BPM, no conformidades por severidad y riesgo integrado
  con la versión de reglas congelada en la evaluación.
- Evidencias de la evaluación con subida y descarga autorizadas.
- Instalación como PWA y cola local de respuestas en IndexedDB con
  sincronización idempotente al recuperar la red.

## Pendiente de implementación

- Completar edición de todos los datos ampliados de empresas y catálogos.
- Conectar la pantalla del técnico con la captura real: la cola sin conexión y el
  cliente de respuestas están disponibles, pero las evaluaciones que se listan en
  esa pantalla siguen siendo datos simulados.
- Informes PDF, revisión del coordinador, correcciones y cierre inmutable.
- Notificaciones, dashboards con métricas reales y consulta histórica completa.
- Sustituir los datos simulados que permanecen en algunas pantallas.
- Pruebas de extremo a extremo del ciclo completo.

## Requisitos locales

Antes de iniciar se necesita:

1. Windows 10 u 11.
2. .NET SDK 9.
3. Node.js 20, 22 o 24.
4. pnpm.
5. PostgreSQL 18 en ejecución.
6. Una base de datos PostgreSQL llamada `ebr_bpm`.

Para revisar los requisitos detectables automáticamente:

```powershell
git clone https://github.com/Luisdcg23/Evalia.git
cd Evalia
.\scripts\check-prerequisites.ps1
```

## Configuración inicial

### 1. Crear la base de datos

Desde pgAdmin 4, conéctese al servidor PostgreSQL local y cree una base de datos llamada:

```text
ebr_bpm
```

### 2. Crear la configuración privada

```powershell
Copy-Item ".env.example" ".env"
notepad ".env"
```

En `.env` se debe:

- Sustituir la contraseña de ejemplo por la contraseña local del usuario `postgres`.
- Definir una clave `Jwt__SigningKey` privada de al menos 32 caracteres.
- Sustituir el secreto local de MinIO.

El archivo `.env` está excluido de Git y nunca debe publicarse.

### 3. Cargar las variables en la terminal

```powershell
Get-Content ".env" | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#")) {
        $name, $value = $line -split "=", 2
        [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), "Process")
    }
}
```

### 4. Aplicar las migraciones

```powershell
dotnet ef database update `
  --project "backend\src\EBR.Infrastructure\EBR.Infrastructure.csproj" `
  --startup-project "backend\src\EBR.Api\EBR.Api.csproj"
```

## Arrancar la aplicación

Desde la raíz del proyecto:

```powershell
.\scripts\start-local.ps1
```

Servicios locales:

| Servicio | Dirección |
|---|---|
| Aplicación web | http://localhost:5173 |
| API | http://localhost:5080 |
| Salud de la API | http://localhost:5080/health |
| Salud de PostgreSQL | http://localhost:5080/health/database |
| Consola de MinIO | http://localhost:9001 |

Los procesos se ejecutan ocultos. Para detenerlos:

```powershell
.\scripts\stop-local.ps1
```

## Usuarios locales de demostración

La migración y el primer arranque en entorno de desarrollo crean cuentas para los cinco roles. La contraseña inicial de todas las cuentas es `EbrLocal2026!`.

| Rol | Correo |
|---|---|
| Administrador | `admin@ebr.local` |
| Administrador de empresa | `empresa@ebr.local` |
| Usuario delegado | `delegado@ebr.local` |
| Coordinador | `coordinador@ebr.local` |
| Técnico evaluador | `tecnico@ebr.local` |

Estas credenciales son únicamente para desarrollo local y deben cambiarse antes de cualquier despliegue real.

## Desarrollo y comprobaciones

Backend:

```powershell
dotnet test "backend\EBR.sln" --no-restore
```

Frontend:

```powershell
pnpm --dir frontend test
pnpm --dir frontend build
```

## Estructura principal

```text
backend/     API, dominio, aplicación, infraestructura y pruebas
frontend/    Aplicación React y pruebas
scripts/     Comprobación, instalación y arranque local
storage/     Binario y datos locales de MinIO, excluidos de Git
```

## Consideraciones

- El sistema se ejecuta completamente en local y no utiliza Docker.
- PostgreSQL conserva datos relacionales y metadatos; los binarios de las evidencias se guardan en MinIO. Si `Minio__Endpoint` está vacío, la API usa el sistema de archivos local (`storage/evidencias`) con la misma abstracción.
- Los expedientes, cálculos y versiones publicadas conservan historial y no deben eliminarse físicamente.
- La interfaz adapta las opciones visibles al rol, pero la autorización efectiva siempre se valida en la API.
- Una compilación satisfactoria no implica que los módulos señalados como pendientes estén terminados.

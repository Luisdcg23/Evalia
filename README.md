# Evalia — Evaluación Basada en Riesgo

Aplicación web local para gestionar evaluaciones basadas en riesgo e inspecciones de Buenas Prácticas de Manufactura (BPM). El proyecto usa una API ASP.NET Core, una interfaz React y PostgreSQL. Su ejecución está diseñada para Windows y no requiere Docker.

> Estado actual: el ciclo operativo está implementado de extremo a extremo en la API —registro, aprobación administrativa, empresa, solicitud, expediente, asignación, programación, ejecución de la evaluación en campo con captura sin conexión, sincronización, cálculo de cumplimiento BPM y de riesgo integrado, informe versionado, revisión del coordinador, corrección, informe oficial en PDF con hash y cierre inmutable del expediente—. La interfaz cubre autenticación, registro y aprobación, portal de empresa, paneles por rol con datos reales, notificaciones y consulta histórica; la captura de la evaluación en campo y las decisiones del informe se ejercen hoy por la API (aún sin pantalla dedicada). Ver "Pendiente" para el detalle.

## Tecnologías

- .NET SDK 9 y ASP.NET Core (Minimal APIs)
- Entity Framework Core y Npgsql
- PostgreSQL 18 y pgAdmin 4
- QuestPDF (licencia Community) para el informe oficial en PDF
- React 19, TypeScript y Vite
- Node.js 20, 22 o 24 y pnpm
- MinIO local para el almacenamiento de evidencias (con respaldo en sistema de archivos)
- xUnit para las pruebas de la API
- Vitest para las pruebas de la interfaz
- Playwright para las pruebas de extremo a extremo (Chromium, Firefox y WebKit)

## Funcionalidades disponibles

- Autenticación con JWT, recuperación y cambio de contraseña, y rotación de refresh tokens.
- Autorización para administrador, administrador de empresa, usuario delegado, coordinador y técnico evaluador.
- Registro de usuarios con la carta de autorización como adjunto obligatorio (solo metadatos) y aprobación o rechazo administrativo.
- Empresas con perfil completo, representantes tipados (legal, calidad, contacto principal), historial append-only y aislamiento de información por empresa, con control de concurrencia optimista en la edición.
- Catálogos de alimentos, niveles y factores de riesgo, y catálogos generales de apoyo (`GET /api/catalogs/{code}`).
- Cálculo reproducible del riesgo del establecimiento y matriz de frecuencia de inspección, siempre contra una versión de reglas publicada y vigente.
- Plantilla de evaluación BPM versionada con árbol jerárquico (capítulos, secciones, subsecciones, agrupadores y 45 preguntas), opciones C/CP/IT/NA y criterios de la guía de llenado con su criticidad; edición bloqueada tras la publicación.
- Solicitudes BPM en borrador con documentación obligatoria (metadatos) y envío que exige al menos un documento.
- Expedientes de inspección con creación idempotente e historial de estados, y orígenes de evaluación: solicitud, alerta LAPCH, denuncia y programación institucional.
- Asignación y reasignación de técnico evaluador con historial completo (`sp_asignar_tecnico`).
- Programación, reprogramación, cancelación y agenda de evaluaciones con validación de solapamiento del técnico (`sp_programar_evaluacion`).
- Ejecución de la evaluación en campo: la instancia congela la plantilla publicada y la versión de reglas de riesgo vigentes al iniciarse, autosave idempotente por pregunta y envío que bloquea la evaluación de forma inmutable.
- Captura sin conexión: la aplicación se instala como PWA y las respuestas se guardan en una cola local en IndexedDB cuando la API no responde; la cola se vacía al recuperar la red con sincronización idempotente por par evaluación/pregunta.
- Cálculo del porcentaje BPM ponderado (excluye del denominador las preguntas No aplica), la calificación de la ficha, las no conformidades por severidad y el nivel de riesgo y la frecuencia de inspección con la versión de reglas congelada en la evaluación. El resultado es una fotografía inmutable.
- Evidencias de la evaluación con subida y descarga autorizadas; en la base solo se guardan los metadatos (nombre, tipo MIME, tamaño, hash SHA-256 y clave del objeto).
- Informe de la evaluación versionado: reemitirlo tras una corrección crea la versión siguiente y conserva intactas las anteriores.
- Revisión del informe por el coordinador (aprobar, devolver o solicitar corrección con observaciones) y gestión de correcciones, con transiciones del expediente y historial inmutable (`sp_revisar_informe`).
- Informe oficial en PDF generado desde la última versión aprobada, determinista e idempotente, con hash SHA-256 y fecha de emisión; el binario se guarda en el almacenamiento de objetos.
- Cierre transaccional e inmutable del expediente (`sp_cerrar_expediente`): exige estado aprobado, informe aprobado y PDF oficial emitido, y deja el expediente en `CLOSED` sin reapertura posible.
- Paneles por rol (técnico, coordinador, portal de empresa y administración) con datos reales de la API, campana de notificaciones con contador de no leídas y marcado persistido en el servidor, y consulta histórica de expedientes con filtros e información del informe oficial y la calificación por caso (RF-20).

## Pendiente de implementación

Dentro del alcance del sistema, aún sin terminar:

- Pantalla del técnico para ejecutar la evaluación en campo (iniciar la instancia, responder las 45 preguntas y enviar). La lógica de captura, la cola sin conexión y el cliente de sincronización están implementados y probados, y se ejercen hoy por la API.
- Pantallas para las decisiones del informe (emitir informe, revisión del coordinador, solicitar y enviar correcciones, emitir el PDF oficial y cerrar el expediente). Todo el ciclo está disponible por la API.
- Datos simulados residuales en algunas pantallas que ningún requisito funcional exige hoy: perfil de empresa del panel de administración, cuenta del usuario autenticado (`PUT /api/users/me`), preferencias de notificación, prioridad/zona/carga del técnico en el DTO de casos, programación desde la pantalla de asignación, formularios de registrar alerta y denuncia (los endpoints existen), y las acciones de descargar PDF y ver observaciones en la tarjeta de solicitud.
- Catálogo `TIPOS_ESTABLECIMIENTO`: no hay tabla ni lista cerrada en las fuentes; queda pendiente hasta que una fuente normativa la defina.
- Series históricas agregadas por mes y repositorio de informes descargables a nivel de administración: no hay endpoint de agregación histórica ni catálogo de informes de administración.

Integraciones externas descritas en el SRS que nunca estuvieron en el alcance de esta entrega:

- Envío de correo electrónico.
- Envío de SMS.
- Firma electrónica.
- Integración con GIS / cartografía.

## Siembra de catálogos normativos

Las migraciones crean el esquema y las cuentas de demostración, pero **no** cargan la matriz de riesgo de alimentos ni publican la plantilla BPM: ese contenido normativo no vive en el repositorio y su carga no se ejecuta al arrancar la aplicación (ver `DATABASE.md`, sección "Importación y calidad de datos").

Para usar el ciclo completo con datos reales hay que sembrar, contra la base ya migrada:

- **Catálogos de riesgo** (`RiskCatalogSeeder`): recibe la extracción textual de la hoja `Categorización_de_alimentos` de la Matriz Riesgo Alimentos, crea las escalas versionadas, los seis factores del establecimiento, las bandas de frecuencia y los peligros por subcategoría, y publica la versión de reglas. Hoy solo se invoca desde las pruebas de integración; no hay comando ni endpoint que lo ejecute.
- **Plantilla BPM** (`BpmTemplateSeeder` / `scripts/import-bpm-template.sql`): el contenido de la ficha vive en `EBR.Infrastructure.Evaluations.BpmTemplateData`; el seeder crea el árbol de 90 nodos, las opciones C/CP/IT/NA y los criterios de la guía, y publica la plantilla. Igual que el anterior, hoy solo se invoca desde las pruebas.

Mientras estos seeders no estén conectados a un comando de arranque o a un endpoint de importación, la ejecución del ciclo de evaluación de extremo a extremo requiere sembrarlos manualmente (por ejemplo, con una prueba puntual o un pequeño programa que instancie el seeder con la fuente textual). El resto de la aplicación —autenticación, empresas, solicitudes, asignación, programación, paneles— funciona sin esa siembra.

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

- Sustituir la contraseña de ejemplo por la contraseña local del usuario `postgres` en `ConnectionStrings__EbrDatabase`.
- Definir una clave `Jwt__SigningKey` privada de al menos 32 caracteres.
- Sustituir el secreto local de MinIO (`Minio__SecretKey`). Si `Minio__Endpoint` se deja vacío, la API usa el sistema de archivos local (`storage/evidencias`).

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

Son 20 migraciones. Aplican limpio sobre una base vacía y dejan 54 tablas, 27 rutinas `sp_`/`fn_` y 21 disparadores.

## Arrancar la aplicación

Desde la raíz del proyecto:

```powershell
.\scripts\start-local.ps1
```

El script carga `.env`, arranca MinIO (si `storage\minio\minio.exe` existe), la API (`dotnet run`) y la interfaz (`pnpm --dir frontend dev`). Si `pnpm` no está disponible, arranque la interfaz aparte con `npm --prefix frontend run dev` y la API con `dotnet run --project backend\src\EBR.Api\EBR.Api.csproj`.

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

El primer arranque en entorno de desarrollo (o el entorno `Testing`) crea cuentas para los cinco roles y una empresa de demostración. La contraseña inicial de todas las cuentas es `EbrLocal2026!`.

| Rol | Correo |
|---|---|
| Administrador | `admin@ebr.local` |
| Administrador de empresa | `empresa@ebr.local` |
| Usuario delegado | `delegado@ebr.local` |
| Coordinador | `coordinador@ebr.local` |
| Técnico evaluador | `tecnico@ebr.local` |

Las cuentas `empresa@ebr.local` y `delegado@ebr.local` quedan vinculadas a la empresa de demostración. Estas credenciales son únicamente para desarrollo local y deben cambiarse antes de cualquier despliegue real.

## Desarrollo y comprobaciones

Backend (220 pruebas unitarias y de integración; usan el proveedor en memoria y no requieren PostgreSQL):

```powershell
dotnet test "backend\EBR.sln" --no-restore
```

Frontend (Vitest, 44 pruebas). Si `pnpm` no está en el PATH, `corepack pnpm` lo resuelve (Node 20+):

```powershell
corepack pnpm --dir frontend test      # o: pnpm --dir frontend test
corepack pnpm --dir frontend build
```

Comprobación de tipos de la interfaz:

```powershell
node frontend\node_modules\typescript\lib\tsc.js -p frontend --noEmit
```

### Pruebas de extremo a extremo (Playwright)

Las especificaciones están en `frontend/e2e/`, organizadas por etapa del ciclo
(`01-autenticacion`, `02-registro-aprobacion`, `03-empresa`, `04-paneles-notificaciones`,
`05-ciclo-completo`). La configuración (`frontend/playwright.config.ts`) define tres proyectos
(Chromium, Firefox y WebKit) y un `webServer` que levanta Vite automáticamente. La API y PostgreSQL
deben estar corriendo aparte. Las specs 01–04 conducen la interfaz; `05-ciclo-completo` recorre todo
el ciclo (registro → cierre) por la API con un `test.step` por etapa, porque la captura en campo y
las decisiones del informe aún no tienen pantalla.

```powershell
# 1. Instalar Playwright y sus navegadores (una sola vez)
corepack pnpm --dir frontend install
node frontend\node_modules\@playwright\test\cli.js install

# 2. Con la API en http://localhost:5080 y la base migrada y sembrada:
corepack pnpm --dir frontend e2e                              # los tres navegadores
corepack pnpm --dir frontend e2e -- --project=chromium        # un solo navegador (rápido)
node frontend\node_modules\@playwright\test\cli.js test --list # solo descubrir y compilar
```

Requisitos para una ejecución completa del ciclo (`05-ciclo-completo`):

1. PostgreSQL 18 corriendo y base `ebr_bpm` con las 20 migraciones aplicadas.
2. API arrancada en `http://localhost:5080` en entorno `Development` (siembra las cuentas de demostración).
3. Catálogos de riesgo y plantilla BPM sembrados (ver "Siembra de catálogos normativos"). Sin esto,
   `05-ciclo-completo` se marca como omitida; las specs 01–04 siguen ejecutándose.
4. Almacenamiento de objetos disponible para el informe oficial en PDF: MinIO corriendo, **o**
   `Minio__Endpoint` vacío en el entorno de la API para usar el sistema de archivos (`storage/`).
5. `E2E_API_URL` opcional si la API no está en el puerto por defecto; `E2E_BASE_URL` para la interfaz.

Nota: el endpoint de autenticación limita a 20 peticiones por minuto. Una corrida de los tres
navegadores hace muchos inicios de sesión y puede tener que esperar a que se abra la ventana (las
specs lo reintentan solo); ejecutar un proyecto a la vez (`--project=chromium`) evita la espera.

## Estructura principal

```text
backend/     API, dominio, aplicación, infraestructura y pruebas
frontend/    Aplicación React, pruebas Vitest y pruebas E2E (frontend/e2e)
scripts/     Comprobación, instalación y arranque local
storage/     Binario y datos locales de MinIO, excluidos de Git
```

## Consideraciones

- El sistema se ejecuta completamente en local y no utiliza Docker.
- PostgreSQL conserva datos relacionales y metadatos; los binarios de las evidencias y del informe oficial se guardan en MinIO o en el sistema de archivos local con la misma abstracción.
- Los expedientes, cálculos, informes y versiones publicadas conservan historial y no deben eliminarse físicamente.
- La interfaz adapta las opciones visibles al rol, pero la autorización efectiva siempre se valida en la API.
- Una compilación satisfactoria no implica que los módulos señalados como pendientes estén terminados.

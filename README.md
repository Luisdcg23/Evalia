# Evalia — Evaluación Basada en Riesgo

Aplicación web para gestionar evaluaciones basadas en riesgo e inspecciones de Buenas Prácticas de Manufactura (BPM). El proyecto usa una API ASP.NET Core, una interfaz React y PostgreSQL. Su ejecución está diseñada para Windows y no requiere Docker.

> Estado actual: el ciclo operativo completo está implementado, con pantalla propia para cada rol y cada paso — registro, aprobación administrativa, empresa, solicitud, expediente (por solicitud, programación institucional, alerta sanitaria o denuncia), asignación, programación, ejecución de la evaluación en campo con captura sin conexión y sincronización, cálculo de cumplimiento BPM y de riesgo integrado, informe versionado, revisión del coordinador, corrección, informe oficial en PDF con hash y cierre inmutable del expediente. Ver "Limitaciones y notas conocidas" para lo que queda fuera de alcance o pendiente.

## Tecnologías

- .NET SDK 9 y ASP.NET Core (Minimal APIs)
- Entity Framework Core y Npgsql
- PostgreSQL 18 y pgAdmin 4
- QuestPDF (licencia Community) para el informe oficial en PDF
- React 19, TypeScript y Vite
- Node.js 20, 22 o 24 y pnpm
- MinIO local para el almacenamiento de evidencias (con respaldo en sistema de archivos)
- MailKit (SMTP) para el envío de correo (recuperación de contraseña y notificaciones), con respaldo que solo registra en el log si no hay proveedor configurado
- xUnit para las pruebas de la API
- Vitest para las pruebas de la interfaz
- Playwright para las pruebas de extremo a extremo (Chromium, Firefox y WebKit)

## Funcionalidades disponibles

- Autenticación con JWT, recuperación y cambio de contraseña, y rotación de refresh tokens.
- Autorización para administrador, administrador de empresa, usuario delegado, coordinador y técnico evaluador.
- Registro de usuarios con la carta de autorización como adjunto obligatorio (solo metadatos) y aprobación o rechazo administrativo, con pantalla propia para ambos pasos.
- Empresas con perfil completo, representantes tipados (legal, calidad, contacto principal), historial append-only y aislamiento de información por empresa, con control de concurrencia optimista en la edición.
- Catálogos de alimentos, niveles y factores de riesgo, y catálogos generales de apoyo (`GET /api/catalogs/{code}`).
- Cálculo reproducible del riesgo del establecimiento y matriz de frecuencia de inspección, siempre contra una versión de reglas publicada y vigente.
- Plantilla de evaluación BPM versionada con árbol jerárquico (capítulos, secciones, subsecciones, agrupadores y 45 preguntas), opciones C/CP/IT/NA y criterios de la guía de llenado con su criticidad; edición bloqueada tras la publicación. Pantalla de administración para crear, editar y publicar plantillas, con un concepto explícito de **plantilla activa** que decide cuál usa una evaluación nueva (independiente de cuál se publicó más recientemente).
- Solicitudes BPM en borrador con documentación obligatoria (metadatos) y envío que exige al menos un documento.
- Expedientes de inspección con creación idempotente e historial de estados, y los cuatro orígenes de evaluación: solicitud, alerta sanitaria, denuncia y programación institucional — cada uno con su formulario propio.
- Asignación y reasignación de técnico evaluador con historial completo (`sp_asignar_tecnico`).
- Programación, reprogramación, cancelación y agenda de evaluaciones con validación de solapamiento del técnico (`sp_programar_evaluacion`), enviada desde la pantalla de asignación del coordinador.
- Ejecución de la evaluación en campo con pantalla propia del técnico: inicia la instancia, responde las 45 preguntas por sección con observaciones, comentarios y evidencias, ve el avance en tiempo real y envía la evaluación (bloqueo inmutable).
- Captura sin conexión: la aplicación se instala como PWA y las respuestas se guardan en una cola local en IndexedDB cuando la API no responde; la cola se vacía al recuperar la red con sincronización idempotente por par evaluación/pregunta.
- Cálculo del porcentaje BPM ponderado (excluye del denominador las preguntas No aplica), la calificación de la ficha, las no conformidades por severidad y el nivel de riesgo y la frecuencia de inspección con la versión de reglas congelada en la evaluación. El resultado es una fotografía inmutable, visible en la pantalla del técnico y en la revisión del coordinador.
- Evidencias de la evaluación con subida y descarga autorizadas desde la propia pantalla de captura; en la base solo se guardan los metadatos (nombre, tipo MIME, tamaño, hash SHA-256 y clave del objeto).
- Informe de la evaluación versionado, emitido por el técnico desde la pantalla de campo: reemitirlo tras una corrección crea la versión siguiente y conserva intactas las anteriores.
- Revisión del informe por el coordinador desde su propia pantalla (aprobar, devolver o solicitar corrección con observaciones) y gestión de correcciones por el técnico, con transiciones del expediente y historial inmutable (`sp_revisar_informe`).
- Informe oficial en PDF generado y descargado desde la pantalla del coordinador, determinista e idempotente, con hash SHA-256 y fecha de emisión.
- Cierre transaccional e inmutable del expediente (`sp_cerrar_expediente`) desde la pantalla del coordinador: exige estado aprobado, informe aprobado y PDF oficial emitido, y deja el expediente en `CLOSED` sin reapertura posible.
- Paneles por rol (técnico, coordinador, portal de empresa y administración) con datos reales de la API, campana de notificaciones con contador de no leídas y marcado persistido en el servidor, y consulta histórica de expedientes con filtros e información del informe oficial y la calificación por caso.

## Guía paso a paso por requisito funcional

Todos los pasos usan las cuentas de demostración de la sección "Usuarios locales de demostración". Cada rol ve solo las secciones que le corresponden en el menú lateral de su panel.

**RF-01 — Autenticación.** En la pantalla de inicio, ingresa correo y contraseña y confirma. "¿Olvidaste tu contraseña?" abre la recuperación (código + contraseña nueva). Desde el panel, la opción de cerrar sesión está en "Configuración".

**RF-02 — Registro de usuarios.** En la pantalla de inicio, "¿No tienes cuenta?" abre el formulario de registro; adjunta los datos de la carta de autorización (obligatorios) y envía — queda "Pendiente de validación". Como Administrador, aprueba o rechaza desde el aviso de solicitudes pendientes de su panel.

**RF-03 — Gestión de empresas.** Como Administrador de empresa o Usuario delegado, entra a "Mi Empresa": ahí se edita dirección, municipio, provincia, teléfono, correo, actividad económica y se consultan los representantes. Guardar aplica el control de concurrencia (si alguien más editó primero, avisa y recarga).

**RF-04 — Paneles (dashboard).** Cada rol ve su panel de inicio con datos reales al iniciar sesión: solicitudes y evaluaciones (empresa), casos pendientes y asignaciones (coordinador), evaluaciones asignadas y calendario (técnico), métricas generales (administrador).

**RF-05 — Solicitudes BPM.** Como empresa/delegado, "Nueva Solicitud" → completa tipo de establecimiento y motivo, adjunta la documentación obligatoria, guarda como borrador o envía (el envío exige al menos un documento).

**RF-06 — Orígenes de caso.** Los cuatro orígenes tienen su propio flujo: solicitud (arriba), programación institucional (panel de coordinador, sección "Alertas" → "+ Programar institucional"), alerta sanitaria y denuncia (ver RF-08/RF-09).

**RF-07 y RF-11 — Programación y calendario.** Como Coordinador, en "Asignaciones" abre un caso pendiente, asigna un técnico y programa fecha/hora en el mismo formulario; la sección "Calendario" muestra la agenda y permite reprogramar o cancelar una programación vigente.

**RF-08 — Alertas sanitarias (LAPCH).** Panel de Coordinador → "Alertas" → "+ Registrar alerta". Sobre cada alerta pendiente hay botones para decidir si procede (genera el expediente) o no procede.

**RF-09 — Denuncias.** Panel de Coordinador → "Denuncias" → "+ Registrar denuncia". Sobre cada una: decidir si procede, no procede o se remite.

**RF-10 — Asignación de evaluador.** Panel de Coordinador → "Asignaciones" → abrir un caso → elegir técnico y motivo → confirmar. El mismo flujo sirve para reasignar un caso ya asignado.

**RF-12 y RF-13 — Ejecución de la evaluación.** Como Técnico evaluador, pestaña "Evaluación en campo": elige un expediente asignado y programado, inicia la evaluación, responde cada pregunta (Cumple / Cumplimiento parcial / Incumplimiento total / No aplica) con observaciones y comentarios — el guardado es automático — y adjunta evidencias por pregunta cuando aplique. El avance se ve en una barra de progreso.

**RF-14 — Motor de riesgo.** Al terminar de responder, "Finalizar evaluación" envía y bloquea las respuestas; la misma pantalla muestra de inmediato el porcentaje BPM, la calificación, el nivel de riesgo y la frecuencia de inspección resultantes.

**RF-15 — Evidencias.** Se adjuntan fotografías (JPEG/PNG/WEBP) o documentos PDF de hasta 15 MB directamente sobre cada pregunta en la pantalla de "Evaluación en campo", incluso sin conexión (se sincronizan al reconectar).

**RF-16 — Informe de evaluación.** Tras calcular el resultado, la misma pantalla del técnico ofrece el formulario de informe (resumen ejecutivo, hallazgos, recomendaciones) para emitirlo.

**RF-17 — Revisión del coordinador.** Panel de Coordinador → "Reportes": abre un expediente en revisión, ve el informe y el resultado calculado, y decide aprobar, devolver o solicitar corrección (con observaciones).

**RF-18 — Gestión de correcciones.** Si el coordinador pide corrección, el técnico ve el aviso con las observaciones en su pantalla de "Evaluación en campo" y puede emitir una nueva versión del informe desde ahí mismo.

**RF-19 — Cierre de expediente.** Con el informe aprobado, en "Reportes" del panel del Coordinador aparecen los botones para generar el PDF oficial, descargarlo, y cerrar el expediente (acción irreversible, con confirmación).

**RF-20 — Consulta histórica.** Panel de Coordinador → "Consulta histórica": filtra por empresa, origen, estado y fecha; la tabla incluye si el expediente tiene informe oficial emitido y su calificación.

## Limitaciones y notas conocidas

Dentro del alcance del sistema, quedan pendientes o fuera de esta entrega:

- **Siembra de catálogos normativos** (ver más abajo): no hay un comando ni un botón para cargar la matriz de riesgo de alimentos ni publicar la plantilla BPM; hoy se hace de forma manual una sola vez por instalación.
- **Doble factor de autenticación** (RF-01, marcado como opcional en la especificación): no implementado.
- **Video corto y geolocalización en evidencias** (RF-15, marcados como opcionales): la evidencia admite foto y documento; video y geolocalización quedan fuera de esta entrega.
- **Catálogo cerrado de tipos de establecimiento**: ninguna fuente normativa entregada define una lista cerrada, así que el campo queda como texto libre en la solicitud BPM.
- Algunos datos secundarios sin requisito funcional asociado siguen sin cablear a la API: el perfil de empresa propio dentro del panel de administración, la edición de los datos de la cuenta del usuario autenticado, las preferencias de notificación, y la prioridad/zona/carga de trabajo del técnico en la pantalla de asignación (el caso no expone esos campos hoy).
- El panel de administración no tiene todavía series históricas agregadas por mes ni un repositorio de informes descargables a ese nivel; el informe oficial se descarga por expediente desde la pantalla del coordinador.
- La validación de solapamiento de horario del técnico se aplica dentro de la transacción de cada solicitud; no hay una restricción declarativa a nivel de base de datos, así que dos solicitudes verdaderamente simultáneas fuera de esa ruta transaccional podrían, en teoría, colarse.
- Varias reglas de negocio existen por partida doble: una vez en el endpoint (que usan las pruebas automatizadas) y otra vez como rutina de PostgreSQL (para que la base sea consistente si algún día se accede fuera de la API). Si se modifica una regla, hay que actualizar ambas.
- Integraciones externas descritas en la especificación pero fuera del alcance de esta entrega: SMS, firma electrónica e integración con GIS/cartografía. El correo electrónico ya está implementado (SMTP vía MailKit): recuperación de contraseña y las notificaciones de `Notification.cs` (asignación, programación, revisión de informe, cierre de expediente) se envían también por correo al destinatario registrado.

Ninguna de estas limitaciones bloquea el ciclo principal descrito arriba.

## Siembra de catálogos normativos

Las migraciones crean el esquema y las cuentas de demostración, pero **no** cargan la matriz de riesgo de alimentos ni publican la plantilla BPM: ese contenido normativo no se distribuye con el código y su carga no se ejecuta al arrancar la aplicación (ver `DATABASE.md`, sección "Importación y calidad de datos").

Para usar el ciclo completo con datos reales hay que sembrar, una sola vez contra la base ya migrada:

- **Catálogos de riesgo**: crea las escalas versionadas, los seis factores del establecimiento, las bandas de frecuencia y los peligros por subcategoría de alimento a partir de la matriz de riesgo de alimentos, y publica la versión de reglas.
- **Plantilla BPM**: crea el árbol de 90 nodos, las opciones C/CP/IT/NA y los criterios de la guía a partir de la ficha oficial, y publica la plantilla. `scripts/import-bpm-template.sql` es la vía equivalente en SQL puro para el equipo de datos.

Hoy no existe un comando ni un endpoint que ejecute esta siembra directamente: se hace escribiendo un pequeño programa que invoque los servicios de importación correspondientes con el contenido normativo, o ejecutando las pruebas de integración que ya los ejercitan como referencia de uso. Una vez sembrada, cualquier evaluación nueva usa automáticamente la plantilla marcada como **activa** (panel de administración → "Plantillas"); el resto de la aplicación —autenticación, empresas, solicitudes, asignación, programación, paneles— funciona sin esta siembra.

## Requisitos

Antes de iniciar se necesita:

1. Windows 10 u 11.
2. .NET SDK 9.
3. Node.js 20, 22 o 24.
4. pnpm (se resuelve con `corepack pnpm` si no está instalado aparte; Node 20+ trae Corepack).
5. PostgreSQL 18 en ejecución.
6. Una base de datos PostgreSQL llamada `ebr_bpm`.

Para revisar los requisitos detectables automáticamente, desde la raíz del proyecto:

```powershell
.\scripts\check-prerequisites.ps1
```

## Instalación paso a paso

### 1. Obtener el código

Clona o descarga el repositorio y ubícate en su raíz.

### 2. Crear la base de datos

Desde pgAdmin 4 (o `psql`), conéctate al servidor PostgreSQL local y crea una base de datos llamada:

```text
ebr_bpm
```

### 3. Crear la configuración privada

```powershell
Copy-Item ".env.example" ".env"
notepad ".env"
```

En `.env`:

- Sustituye la contraseña de ejemplo por la contraseña local del usuario `postgres` en `ConnectionStrings__EbrDatabase`.
- Define una clave `Jwt__SigningKey` privada de al menos 32 caracteres.
- Sustituye el secreto local de MinIO (`Minio__SecretKey`). Si dejas `Minio__Endpoint` vacío, la API usa el sistema de archivos local (`storage/evidencias`) en vez de MinIO.

El archivo `.env` está excluido del control de versiones y nunca debe publicarse.

### 4. Cargar las variables en la terminal

```powershell
Get-Content ".env" | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#")) {
        $name, $value = $line -split "=", 2
        [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), "Process")
    }
}
```

### 5. Aplicar las migraciones

```powershell
dotnet ef database update `
  --project "backend\src\EBR.Infrastructure\EBR.Infrastructure.csproj" `
  --startup-project "backend\src\EBR.Api\EBR.Api.csproj"
```

Son 22 migraciones. Aplican limpio sobre una base vacía y dejan 55 tablas, 27 rutinas `sp_`/`fn_` y 21 disparadores.

### 6. Instalar las dependencias de la interfaz

```powershell
corepack pnpm --dir frontend install
```

### 7. Sembrar los catálogos normativos

Ver "Siembra de catálogos normativos" arriba — necesario antes de poder iniciar una evaluación real; el resto de la aplicación funciona sin este paso.

### 8. Arrancar la aplicación

Desde la raíz del proyecto:

```powershell
.\scripts\start-local.ps1
```

El script carga `.env`, arranca MinIO (si `storage\minio\minio.exe` existe), la API (`dotnet run`) y la interfaz. Si prefieres arrancarlos por separado:

```powershell
dotnet run --project backend\src\EBR.Api\EBR.Api.csproj
corepack pnpm --dir frontend dev
```

Servicios locales:

| Servicio | Dirección |
|---|---|
| Aplicación web | http://localhost:5183 |
| API | http://localhost:5080 |
| Salud de la API | http://localhost:5080/health |
| Salud de PostgreSQL | http://localhost:5080/health/database |
| Consola de MinIO | http://localhost:9001 |

Para detener los procesos iniciados con `start-local.ps1`:

```powershell
.\scripts\stop-local.ps1
```

## Usuarios locales de demostración

El primer arranque en entorno de desarrollo crea cuentas para los cinco roles y una empresa de demostración. La contraseña inicial de todas las cuentas es `EbrLocal2026!`.

| Rol | Correo |
|---|---|
| Administrador | `admin@ebr.local` |
| Administrador de empresa | `empresa@ebr.local` |
| Usuario delegado | `delegado@ebr.local` |
| Coordinador | `coordinador@ebr.local` |
| Técnico evaluador | `tecnico@ebr.local` |

Las cuentas `empresa@ebr.local` y `delegado@ebr.local` quedan vinculadas a la empresa de demostración. Estas credenciales son únicamente para desarrollo local y deben cambiarse antes de cualquier despliegue real.

## Desarrollo y comprobaciones

Backend (226 pruebas unitarias y de integración; usan el proveedor en memoria y no requieren PostgreSQL):

```powershell
dotnet test "backend\EBR.sln" --no-restore
```

Frontend (Vitest, 133 pruebas):

```powershell
corepack pnpm --dir frontend test
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
deben estar corriendo aparte.

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

1. PostgreSQL 18 corriendo y base `ebr_bpm` con las 22 migraciones aplicadas.
2. API arrancada en `http://localhost:5080` en entorno `Development` (siembra las cuentas de demostración).
3. Catálogos de riesgo y plantilla BPM sembrados (ver "Siembra de catálogos normativos"). Sin esto,
   `05-ciclo-completo` se marca como omitida; las specs 01–04 siguen ejecutándose.
4. Almacenamiento de objetos disponible para el informe oficial en PDF: MinIO corriendo, **o**
   `Minio__Endpoint` vacío en el entorno de la API para usar el sistema de archivos (`storage/`).
5. `E2E_API_URL` opcional si la API no está en el puerto por defecto; `E2E_BASE_URL` para la interfaz.

Nota: el endpoint de autenticación limita a 20 peticiones por minuto. Una corrida de los tres
navegadores hace muchos inicios de sesión y puede tener que esperar a que se abra la ventana (las
specs lo reintentan solas); ejecutar un proyecto a la vez (`--project=chromium`) evita la espera.

## Estructura principal

```text
backend/     API, dominio, aplicación, infraestructura y pruebas
frontend/    Aplicación React, pruebas Vitest y pruebas E2E (frontend/e2e)
scripts/     Comprobación, instalación y arranque local
storage/     Binario y datos locales de MinIO, excluidos del control de versiones
```

## Consideraciones

- El sistema se ejecuta completamente en local y no utiliza Docker.
- PostgreSQL conserva datos relacionales y metadatos; los binarios de las evidencias y del informe oficial se guardan en MinIO o en el sistema de archivos local con la misma abstracción.
- Los expedientes, cálculos, informes y versiones publicadas conservan historial y no deben eliminarse físicamente.
- La interfaz adapta las opciones visibles al rol, pero la autorización efectiva siempre se valida en la API.
- Una compilación satisfactoria no implica que los módulos señalados como pendientes estén terminados.

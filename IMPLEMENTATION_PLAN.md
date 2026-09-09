# Plan de implementación del ciclo EBR/BPM

## Objetivo

Completar el ciclo operativo descrito por RF-01 a RF-20 sobre .NET 9, React, PostgreSQL y almacenamiento local de objetos, manteniendo trazabilidad, uso offline e interfaces diferenciadas por rol.

## Restricciones globales

- PostgreSQL es el único motor de base de datos.
- La aplicación se ejecuta localmente sin Docker.
- `DATABASE.md` define el contrato de datos.
- Los Excel entregados son la fuente de reglas y catálogos; el SRS define alcance y flujos.
- Los datos normativos incompletos no se completan por inferencia.
- Cada cambio funcional comienza con una prueba que falla por la ausencia del comportamiento.
- Los cambios locales preexistentes `.env.example` eliminado y `scripts/seed-bpm-template.sql` sin seguimiento se preservan hasta decidir su integración.

## Fase 1: línea base, esquema e importadores

### Tarea 1.1: estabilizar pruebas de integración

**Archivos:** `backend/src/EBR.Api/Program.cs`, `backend/tests/EBR.IntegrationTests/EbrApiFactory.cs`, pruebas de salud.

**Resultado:** el host de pruebas no registra en Windows Event Log y las 36 pruebas backend pueden ejecutarse sin privilegios administrativos.

### Tarea 1.2: normalizar catálogos y reglas de riesgo

**Archivos:** nuevos modelos en `backend/src/EBR.Domain/RiskCatalogs`, configuración de `EbrDbContext`, migración EF, servicio de cálculo y pruebas unitarias/integración.

**Interfaces producidas:** escalas versionadas, riesgos por tipo, reglas de agregación, bandas inclusivas y procedimiento `sp_registrar_calculo_riesgo`.

**Pruebas obligatorias:** escalas 2/4/8 y 1/2/3 independientes; máximo micro/químico; seis pesos suman 1; límites 3.6 y 6.3; importación rechaza filas incompletas.

### Tarea 1.3: importar la plantilla BPM completa

**Archivos:** modelos de criterios/opciones/reglas, configuración EF, migración, `scripts/import-bpm-template.sql` y pruebas de publicación.

**Interfaces producidas:** árbol de 90 nodos, 45 preguntas, opciones C/CP/IT/NA, criterios de guía y funciones/procedimientos documentados en `DATABASE.md`.

**Pruebas obligatorias:** códigos únicos; padres en la misma plantilla; ausencia de ciclos; textos íntegros; 45 preguntas; CP=0.5; NA fuera del denominador; publicación inmutable.

### Tarea 1.4: catálogos generales y perfil de usuario

**Archivos:** modelos de catálogos, migración, repositorio de consultas, `UserEndpoints`, endpoints de catálogos y pruebas.

**Interfaces producidas:** `GET /api/catalogs/{code}` y perfil con empresas autorizadas; función `fn_perfil_usuario`.

## Fase 2: empresas, solicitudes y orígenes

### Tarea 2.1: empresa completa e historial

Agregar dirección, municipio, provincia, teléfono, correo, actividad económica y representantes tipados. Incorporar actualización con control de concurrencia e historial append-only.

### Tarea 2.2: documentos de registro y solicitudes

Agregar metadatos de carta de autorización y documentación BPM obligatoria. Validar presencia antes del envío sin guardar binarios en PostgreSQL.

### Tarea 2.3: programación institucional y cierre de orígenes

Permitir crear casos institucionales y cerrar correctamente alertas o denuncias que no proceden.

## Fase 3: asignación, agenda y ejecución

### Tarea 3.1: asignación histórica

Crear asignación vigente e historial. Solo coordinadores asignan o reasignan técnicos activos. Ejecutar la operación mediante `sp_asignar_tecnico`.

### Tarea 3.2: programación y calendario

Crear agenda con programación, reprogramación y cancelación versionadas. Exponer consultas día/semana/mes y evitar solapamientos del técnico.

### Tarea 3.3: instancia de evaluación y respuestas

Crear una instancia desde una plantilla publicada, capturar respuestas y autosave idempotente, calcular progreso y bloquear respuestas al enviar.

### Tarea 3.4: cálculo BPM y riesgo integrado

Calcular porcentaje BPM, no conformidades por severidad y riesgo/frecuencia usando la versión de reglas vinculada a la evaluación.

## Fase 4: evidencias y modo offline

### Tarea 4.1: almacenamiento de evidencias

Implementar una abstracción de objetos compatible con MinIO local, metadatos PostgreSQL, validación de tipo/tamaño/hash y descargas autorizadas.

### Tarea 4.2: PWA y outbox

Agregar manifest, service worker, IndexedDB y cola idempotente. Permitir guardar respuestas y evidencias pendientes sin conexión y sincronizarlas al recuperar conectividad.

## Fase 5: informes, revisión y cierre

### Tarea 5.1: informe versionado

Generar resumen, hallazgos, no conformidades, recomendaciones y referencias de evidencias. Mantener versiones después de cada corrección.

### Tarea 5.2: revisión y correcciones

Permitir al coordinador aprobar, devolver o solicitar correcciones. El técnico ve observaciones, crea una versión corregida y reenvía.

### Tarea 5.3: PDF oficial y cierre

Generar el PDF desde datos persistidos, registrar hash y fecha de emisión y cerrar el expediente mediante `sp_cerrar_expediente`.

## Fase 6: frontend real, consulta y validación final

### Tarea 6.1: reemplazar datos simulados

Crear clientes y hooks por módulo. Sustituir constantes operativas de los dashboards de administrador, empresa, coordinador y técnico por respuestas reales de la API con estados de carga, vacío y error.

### Tarea 6.2: consulta histórica y notificaciones

Agregar filtros por empresa, solicitud, evaluación, fecha y estado, además de notificaciones persistidas leídas/no leídas.

### Tarea 6.3: pruebas de extremo a extremo

Cubrir registro, aprobación, empresa, solicitud, asignación, programación, evaluación offline, sincronización, informe, corrección, aprobación y cierre. Ejecutar el frontend en Chromium, Firefox y WebKit.

### Tarea 6.4: documentación y ejecución local

Actualizar `README.md`, mantener `DATABASE.md`, documentar migraciones, seeds, MinIO, respaldo/restauración, diagnóstico y limitaciones verificadas.

## Criterio de terminación

Una fase termina únicamente cuando sus migraciones se aplican en una base limpia, pasan las pruebas unitarias e integración relacionadas, el frontend compila sin datos simulados del módulo y el comportamiento puede reproducirse mediante API o interfaz.

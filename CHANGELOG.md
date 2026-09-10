# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato sigue las convenciones de [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [Sin publicar]

### Agregado

- Escalas de riesgo versionadas (riesgo de producto y frecuencia) y versiones
  de reglas de riesgo, con procedimiento transaccional para registrar un
  cálculo (`sp_registrar_calculo_riesgo`) y bloqueo de modificación sobre
  cálculos ya registrados.
- Peligros microbiológicos y químicos por subcategoría de alimento, con
  importador que valida la matriz de riesgo y descarta filas incompletas en
  lugar de completarlas por inferencia.
- Plantilla de evaluación BPM completa: árbol jerárquico de capítulos,
  secciones, subsecciones, agrupadores y preguntas, con opciones de respuesta
  (Cumple / Cumplimiento parcial / Incumplimiento total / No aplica) y
  criterios de guía asociados a cada pregunta con su nivel de criticidad.
- Rutinas de publicación de plantillas (`fn_plantilla_arbol`,
  `sp_importar_plantilla_bpm`, `sp_publicar_plantilla`) y bloqueo de edición
  sobre plantillas ya publicadas.
- Endpoint de catálogos generales (`GET /api/catalogs/{code}`) para niveles de
  riesgo, categorías de alimento, roles del sistema y estados de solicitudes
  y expedientes.
- Perfil de usuario autenticado con las empresas realmente autorizadas para
  cada cuenta (`GET /api/users/me`, función `fn_perfil_usuario`).
- Perfil completo de empresa (dirección, municipio, provincia, teléfono,
  correo y actividad económica) con actualización mediante
  `PUT /api/companies/{id}` protegida por control de concurrencia optimista
  (token de versión) y con historial append-only de cambios
  (`GET /api/companies/{id}/history`, disparador
  `tr_empresa_historial_inmutable`).
- Representantes de empresa tipados (legal, calidad, contacto principal), con
  como máximo un representante activo por tipo y por empresa.
- Metadatos de la carta de autorización como adjunto obligatorio del registro
  de usuario (`POST /api/auth/register`), con catálogo cerrado de tipo de
  documento y consulta para el administrador
  (`GET /api/users/{id}/registration-documents`).
- Metadatos de documentación obligatoria para solicitudes BPM
  (`POST /api/bpm-requests/{id}/documents`, `GET /api/bpm-requests/{id}`), con
  validación de presencia de al menos un documento antes de permitir el envío
  de la solicitud. En ambos casos solo se guardan metadatos (nombre de
  archivo, tipo MIME, tamaño, hash y referencia de almacenamiento); no se
  persisten binarios.
- Programación institucional de evaluaciones
  (`POST /api/cases/institutional`, roles `ADMINISTRADOR` y `COORDINADOR`):
  permite crear un expediente de inspección directamente, sin solicitud,
  alerta ni denuncia previa, con motivo y observaciones obligatorios/
  opcionales según el mismo patrón de los demás orígenes.
- Asignación y reasignación de técnico evaluador a un expediente
  (`POST /api/cases/{id}/assign`, rol `COORDINADOR`): valida que el técnico
  tenga el rol Técnico Evaluador y esté activo, transiciona el caso de
  pendiente de asignación a asignado en la primera asignación, y conserva el
  historial completo de asignaciones sin duplicar la asignación vigente
  (`GET /api/cases/{id}/assignments`), con procedimiento transaccional
  `sp_asignar_tecnico`.
- Programación y agenda de evaluaciones (RF-07, RF-11): programar, reprogramar y
  cancelar la fecha de una evaluación con historial versionado
  (`POST /api/cases/{id}/schedule`, `POST /api/cases/{id}/reschedule`,
  `POST /api/cases/{id}/cancel-schedule`, `GET /api/cases/{id}/schedules`), con
  validación de que el técnico asignado no tenga otra programación vigente que
  se solape en el tiempo. Programar exige un técnico ya asignado y transiciona
  el caso a programado; reprogramar y cancelar no cambian el estado del caso.
  Consulta de agenda por rango de fechas con empresa, dirección, fecha y estado
  (`GET /api/cases/schedule?from=&to=`), con procedimiento transaccional
  `sp_programar_evaluacion`.
- Ejecución de la evaluación en campo (RF-12, RF-13): instancia de evaluación
  que congela la plantilla publicada y la versión de reglas de riesgo vigentes
  al iniciarse (`POST /api/cases/{id}/evaluations`), captura de respuestas por
  pregunta con guardado automático idempotente
  (`PUT /api/evaluations/{id}/responses/{itemId}`), consulta de la instancia
  con su progreso de captura (`GET /api/evaluations/{id}`) y envío que bloquea
  la evaluación y transiciona el expediente a pendiente de informe
  (`POST /api/evaluations/{id}/submit`). Las cuatro rutas son exclusivas del
  rol Técnico Evaluador y validan la asignación vigente del expediente. Una
  evaluación enviada es inmutable, garantizado también en la base con los
  disparadores `tr_evaluacion_instancia_inmutable` y
  `tr_evaluacion_respuesta_bloqueada`, con los procedimientos transaccionales
  `sp_iniciar_evaluacion`, `sp_guardar_respuestas` y `sp_enviar_evaluacion`.
- Cálculo BPM y riesgo integrado (RF-14): al enviar una evaluación se registra
  una fotografía inmutable del resultado con el porcentaje BPM ponderado (que
  excluye del denominador las preguntas marcadas No aplica), la calificación
  declarada por la ficha, el conteo de no conformidades por severidad según la
  criticidad de los criterios de guía, y el nivel de riesgo y la frecuencia de
  inspección calculados con la versión de reglas congelada en la evaluación
  (`GET /api/evaluations/{id}/result`). Publicar después otra versión de
  reglas no altera resultados ya registrados, garantizado en la base con el
  disparador `tr_evaluacion_resultado_inmutable`.

- Evidencias de la evaluación en campo: subida y descarga autorizadas
  (`POST /api/evaluations/{id}/evidence`, `GET /api/evaluations/{id}/evidence`,
  `GET /api/evaluations/{id}/evidence/{evidenciaId}/content`) sobre una
  abstracción de almacenamiento de objetos con dos implementaciones, MinIO local
  y sistema de archivos, seleccionadas por configuración. En PostgreSQL solo se
  guardan los metadatos (nombre, tipo MIME, tamaño, hash SHA-256 calculado por el
  servidor y clave del objeto); el binario nunca entra en la base. Se admiten
  únicamente imágenes JPEG/PNG/WEBP y PDF hasta 15 MB, validado también con
  restricciones de comprobación. Adjuntar es exclusivo del técnico asignado y
  solo mientras la evaluación sigue abierta; consultar y descargar los añaden
  Coordinador y Administrador. Los metadatos son inmutables
  (`tr_evaluacion_evidencia_valida`, `tr_evaluacion_evidencia_inmutable`).

- Captura en campo sin conexión: la aplicación se instala como PWA (manifiesto y
  trabajador de servicio con el armazón en caché) y las respuestas capturadas se
  guardan en una cola local en IndexedDB cuando la API no responde. La cola se
  vacía al recuperar la red y la sincronización es idempotente por partida
  doble: la clave de cada entrada es la pregunta —evaluación e ítem—, de modo
  que reguardar reemplaza lo pendiente en lugar de acumular envíos, y la API
  resuelve cada respuesta como alta o actualización sobre ese mismo par. Solo se
  admite una sincronización en curso, y un envío fallido conserva la respuesta
  para el siguiente intento. El trabajador de servicio no almacena nada de
  `/api`: los datos del expediente se consultan siempre contra el servidor.

- Informe de la evaluación versionado (`POST /api/evaluations/{id}/report`,
  `GET /api/evaluations/{id}/report`, `GET /api/evaluations/{id}/report/versions`).
  Emitirlo de nuevo tras una corrección crea la versión siguiente y conserva
  intactas las anteriores: el informe es lo que se comunica al establecimiento y
  una corrección no puede borrar lo ya emitido. Solo lo emite el técnico con la
  asignación vigente del expediente y solo sobre una evaluación ya enviada;
  Coordinador y Administrador consultan cualquier informe y el técnico únicamente
  el de los expedientes que ha tenido asignados. Las cifras del BPM no se copian
  en el informe: se leen del resultado inmutable de la evaluación. Las mismas
  reglas viven en la base (`tr_evaluacion_informe_valido`,
  `tr_evaluacion_informe_inmutable`).

### Cambiado

- Las altas de catálogos de riesgo (`/api/catalogs`) quedan enlazadas a una
  versión de reglas: los peligros de subcategoría y las bandas de frecuencia
  entran en la versión publicada vigente y los factores del establecimiento en
  el borrador en curso, porque una versión publicada exige el juego completo
  de factores con pesos que sumen 1. Con ello se eliminó la ruta de cálculo
  heredada que operaba sobre `puntaje_total` sin versión de reglas: el motor
  solo calcula contra una versión publicada y vigente.

## [0.1.0] - 2026-09-08

### Agregado

- Autenticación con JWT, recuperación y cambio de contraseña.
- Registro y aprobación administrativa de usuarios por rol.
- Gestión de empresas, representantes y aislamiento de datos por empresa.
- Solicitudes BPM, expedientes de inspección y su historial de estados.
- Registro y decisión de alertas sanitarias y denuncias.

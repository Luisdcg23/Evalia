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

## [0.1.0] - 2026-09-08

### Agregado

- Autenticación con JWT, recuperación y cambio de contraseña.
- Registro y aprobación administrativa de usuarios por rol.
- Gestión de empresas, representantes y aislamiento de datos por empresa.
- Solicitudes BPM, expedientes de inspección y su historial de estados.
- Registro y decisión de alertas sanitarias y denuncias.

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

## [0.1.0] - 2026-09-08

### Agregado

- Autenticación con JWT, recuperación y cambio de contraseña.
- Registro y aprobación administrativa de usuarios por rol.
- Gestión de empresas, representantes y aislamiento de datos por empresa.
- Solicitudes BPM, expedientes de inspección y su historial de estados.
- Registro y decisión de alertas sanitarias y denuncias.

# Modelo de datos PostgreSQL

## Propósito y fuentes

PostgreSQL es el único motor soportado. El esquema se administra mediante migraciones de Entity Framework Core y los datos normativos se cargan mediante procesos idempotentes con validación.

El modelo se basa en:

- La especificación `SRS Solucion de Evaluacion Basada en Riesgo.pdf`.
- `Hoja de Cálculo Categorización Establecimiento y Frecuencia de Inspección revisado SBR.xlsx`.
- `Matriz Riesgo Alimentos.xlsx`.
- `Ficha Inspección BPM Revisión Final 23-09-24 revisado FSP-FD octubre 2024_.xlsx`.
- El script heredado de SQL Server `AllItems`, utilizado solo como referencia de orden y jerarquía.

Los archivos fuente no forman parte del repositorio. Las migraciones, scripts de importación y pruebas contienen únicamente los datos necesarios para ejecutar la aplicación.

## Criterios de diseño

- Los nombres funcionales se conservan en español y el código de aplicación permanece en inglés.
- Las claves técnicas son enteros o UUID; los códigos visibles nunca se usan como única identidad técnica.
- Los catálogos y reglas se versionan. Un cálculo histórico conserva la versión utilizada.
- Las publicaciones, evaluaciones enviadas, informes aprobados y expedientes cerrados son inmutables.
- No se eliminan físicamente registros históricos. Se usa vigencia o estado.
- Los textos normativos usan `text` para evitar los truncamientos presentes en el SQL heredado.
- Las lecturas comunes usan vistas o funciones. Los cambios transaccionales con varias invariantes usan procedimientos.
- La API no ejecuta SQL desde los endpoints; utiliza servicios de infraestructura con parámetros tipados.

## Plantilla BPM

`Plantilla_Evaluacion_Item` conserva el árbol recursivo:

| Código fuente | Tipo normalizado | Uso |
|---|---|---|
| C | CHAPTER | Capítulo |
| S | SECTION | Sección |
| SS | SUBSECTION | Subsección |
| A | GROUP | Agrupador dentro de una subsección |
| I | QUESTION | Pregunta evaluable |

La ficha oficial contiene 90 nodos: 7 capítulos, 13 secciones, 19 subsecciones, 6 agrupadores y 45
preguntas. El conteo se verificó nodo a nodo contra la hoja `Ficha Inspección BPM` y coincide con el
total de puntos posibles que la propia hoja declara (`45`). Distribución real por capítulo:

| Capítulo | Secciones | Subsecciones | Agrupadores | Preguntas |
|---|---:|---:|---:|---:|
| 1. Establecimiento, diseño de las instalaciones y equipo | 3 | 10 | 6 | 26 |
| 2. Capacitación y competencia | 3 | 0 | 0 | 3 |
| 3. Mantenimiento, limpieza, desinfección y control de plagas | 2 | 3 | 0 | 4 |
| 4. Higiene personal | 0 | 0 | 0 | 1 |
| 5. Control de las operaciones | 4 | 6 | 0 | 9 |
| 6. Información sobre los productos y sensibilización del consumidor | 1 | 0 | 0 | 1 |
| 7. Transporte | 0 | 0 | 0 | 1 |

Los seis agrupadores son las estructuras internas del punto `1.1.3`: paredes, pisos, techos,
ventanas, puertas y superficies en contacto con los alimentos. Una sección o un capítulo pueden
contener preguntas directamente cuando la ficha no los subdivide, como `1.3`, `4` y `7`.

Cada pregunta pesa `1`, admite `NA` y usa el tipo de respuesta `BPM_OPTION`. Las descripciones se
copian literalmente de la celda de origen y solo se recortan los espacios iniciales y finales; la
columna `descripcion` es `text`, de modo que ya no existe el truncamiento a 255 caracteres del SQL
heredado. La celda de procedencia de cada nodo queda registrada en `reglas`
(`{"hoja": ..., "celda": ...}`).

Las opciones evaluables se guardan por versión de plantilla en `Plantilla_Evaluacion_Opcion`:

| Código | Valor | Cuenta en el denominador | Efecto |
|---|---:|---|---|
| C | 1.0 | Sí | Cumple |
| CP | 0.5 | Sí | Cumplimiento parcial |
| IT | 0.0 | Sí | Incumplimiento total |
| NA | nulo | No | Se excluye del denominador |

La guía de llenado se normaliza en `Plantilla_Evaluacion_Criterio`: 162 criterios asociados al nodo
que evalúan, con criticidad `CRITICAL`, `MAJOR` o `MINOR` —traducción de las marcas `C`, `M` y
`m`/`Me` de la hoja— y la referencia de hoja y celda de origen. La criticidad es nula cuando la
guía no la declara; no se infiere.

`Plantilla_Evaluacion_Regla` conserva las cuatro bandas de calificación de la ficha:

| Porcentaje | Clasificación | Acción |
|---|---|---|
| ≤ 60% | Condiciones inaceptables | Considerar cierre |
| > 60% y ≤ 70% | Condiciones deficientes | Urge corregir |
| > 70% y ≤ 80% | Condiciones regulares | Necesario hacer correcciones |
| > 80% | Buenas condiciones | Hacer algunas correcciones |

El porcentaje BPM se calcula así:

```text
puntos = suma(peso * valor de respuesta por pregunta aplicable)
denominador = suma(peso de preguntas aplicables)
porcentaje = puntos / denominador * 100
```

Una evaluación sin todas las respuestas obligatorias permanece en estado pendiente; nunca se
representa como cero ni como error matemático. Si todas las preguntas se responden `NA` el
denominador es cero y el porcentaje queda sin valor, nunca en `0`.

Una versión publicada es inmutable. La invariante está duplicada: en la aplicación, dentro de
`SaveChanges`, y en PostgreSQL con el disparador `tr_plantilla_publicada_inmutable`, que protege la
cabecera, los ítems, las opciones, los criterios y las reglas de calificación. Solo se admite la
transición `DRAFT` a `PUBLISHED`.

### Anomalías registradas de la ficha oficial

No se corrigen ni se completan; se conservan tal como aparecen en la fuente.

- El capítulo 5 se titula `5. . CONTROL DE LAS OPERACIONES`, con un punto duplicado, y la
  subsección `3.1.3 Monitoreo/seguimiento de la eficacia` omite el punto final de su numeración.
- Varias descripciones y criterios contienen erratas de la fuente. Se copian literalmente.
- En la guía de llenado, el bloque `1.2.6 Iluminación` es único, mientras que la ficha divide ese
  punto en tres preguntas `a`, `b` y `c`. Sus tres criterios se asocian al nodo de la subsección
  `1.2.6` y no a una pregunta concreta, porque la fuente no establece esa correspondencia.
- La celda `B229` de la guía mezcla el criterio `ii` del punto `2.1` con el texto de objetivo y
  justificación del capítulo 2. Se registra únicamente el criterio; el bloque narrativo se descarta
  y se documenta aquí.
- La numeración romana del criterio `D112` de la guía repite `ii`. El texto se conserva literal y el
  criterio se identifica internamente como `iii` por su posición.
- Las marcas de criticidad de la guía aparecen en la fila del criterio o en la inmediata siguiente,
  según cómo estén combinadas las celdas. La lectura respeta esa correspondencia posicional.

## Empresas

`Empresa` incorpora los campos del RF-03: `direccion`, `municipio`, `provincia`, `telefono`,
`correo` y `actividad_economica` (texto, cadena vacía por defecto). El registro inicial
(`POST /api/companies`) sigue exigiendo solo razón social, RNC y nombre comercial —igual que antes
de esta ampliación—, porque el RNC es la identidad fiscal de la empresa y no se ofrece en el alta
mínima ningún flujo para cambiarlo. El resto del perfil se completa con
`PUT /api/companies/{id}`, que sí exige los ocho campos (razón social, nombre comercial, dirección,
municipio, provincia, teléfono, correo con `@` y actividad económica); el RNC no se acepta en esa
actualización y permanece inmutable tras el alta.

`Empresa.version_token` es un token de concurrencia optimista (mismo patrón que
`Solicitud_BPM.version_token`). `PUT /api/companies/{id}` exige el token vigente en el cuerpo de la
solicitud: si no coincide con el valor actual de la fila, la actualización se rechaza con `409` sin
modificar la empresa ni generar historial. Si coincide, la fila se actualiza y el token se
regenera.

Cada actualización exitosa que cambia al menos un campo agrega una fila a `Empresa_Historial`
(`empresa_id`, `fecha_cambio`, `cambiado_por`, `cambios` en `jsonb` con el detalle `{campo: {old,
new}}` de los campos que cambiaron). El historial es append-only: la invariante se refuerza en
`EbrDbContext.SaveChanges` (rechaza `Modified`/`Deleted` sobre `CompanyHistoryEntry`) y se duplica
en PostgreSQL con el disparador `tr_empresa_historial_inmutable`. `GET /api/companies/{id}/history`
expone las filas ordenadas de la más reciente a la más antigua.

`Representante_Empresa.tipo_representante` normaliza los tres tipos del RF-03 (`LEGAL`, `CALIDAD`,
`CONTACTO_PRINCIPAL`), reforzados con `CK_Representante_Tipo`. La regla de negocio es que una
empresa tiene como máximo un representante **activo** por tipo: se valida en el endpoint (`409` si
ya existe uno activo del mismo tipo) y se duplica en la base con un índice único filtrado por
vigencia, `IX_Representante_Empresa_empresa_id_tipo_representante_vigente`
(`UNIQUE (empresa_id, tipo_representante) WHERE vigente`), que permite reactivar el tipo en el
futuro sin que las filas inactivas históricas bloqueen la restricción.

## Documentos de registro y solicitudes (solo metadatos)

RF-02 exige la carta de autorización como adjunto obligatorio del registro de usuario y RF-05 exige
documentación obligatoria para las solicitudes BPM. En ambos casos el sistema solo almacena
**metadatos** del documento (nombre de archivo, tipo MIME, tamaño en bytes, hash y una referencia de
almacenamiento en texto, por ejemplo una ruta o clave que en el futuro apuntará a MinIO). El binario
nunca se persiste en PostgreSQL; el almacenamiento real de objetos queda como tarea futura separada
("almacenamiento de evidencias"). Ninguna de las dos entidades tiene una propiedad de tipo binario.

`Documento_Registro_Usuario` (`UserRegistrationDocument`) se liga al usuario por `usuario_id`.
`tipo_documento` es un catálogo cerrado (`UserRegistrationDocumentTypes`), hoy con un único valor,
`CARTA_AUTORIZACION`, reforzado con `CK_Documento_Registro_Tipo`; se modela como catálogo y no como
texto libre porque el SRS solo define ese adjunto para RF-02, pero deja espacio para agregar otros
tipos de documento de registro sin cambiar el esquema. **Decisión conservadora**: `POST
/api/auth/register` exige los cinco metadatos de la carta de autorización (nombre de archivo, tipo
MIME, tamaño, hash y referencia de almacenamiento) y rechaza el registro completo (`400`) si falta
alguno; el SRS declara ese adjunto como requerido de RF-02, así que no se acepta un registro
incompleto para completarlo después. El metadato se guarda en la misma transacción que crea el
usuario. `GET /api/users/pending` y el nuevo `GET /api/users/{id}/registration-documents` (solo
`ADMINISTRADOR`) exponen esos metadatos para que el administrador los vea al aprobar o rechazar la
solicitud de registro.

`Solicitud_BPM_Documento` (`BpmRequestDocument`) se liga a la solicitud por `solicitud_id` y admite
más de un documento por solicitud. A diferencia del documento de registro, `tipo_documento` es texto
controlado (`character varying(120)`, no vacío) y no un catálogo cerrado: el SRS describe la
"documentación obligatoria" de RF-05 sin enumerar una lista cerrada de tipos de documento, así que no
se inventa esa lista; el control de formato queda en la validación del endpoint
(`POST /api/bpm-requests/{id}/documents`, mismos roles que pueden crear o editar la solicitud), no en
una restricción de base de datos. `GET /api/bpm-requests/{id}` (nuevo) y `GET /api/bpm-requests`
incluyen la lista de documentos de cada solicitud. Los documentos pueden agregarse sin importar el
estado de la solicitud (`DRAFT` o `SUBMITTED`): adjuntar evidencia adicional después del envío no
modifica los campos de la solicitud en sí, así que no se sujeta a la misma regla que bloquea la
edición de una solicitud enviada.

`POST /api/bpm-requests/{id}/submit` ahora exige al menos un documento adjunto: si la solicitud no
tiene ninguna fila en `Solicitud_BPM_Documento`, el envío se rechaza con `409` sin cambiar el estado,
antes de comprobar la transición de estado. El envío repetido de una solicitud ya enviada (con
expediente ya creado) sigue siendo idempotente y no se ve afectado por esta validación, porque esa
rama corta antes de llegar a la comprobación de documentos.

## Casos y orígenes de evaluación (RF-06)

`Caso` (`InspectionCase`) representa el expediente de inspección. Su origen se identifica con
`tipo_origen`/`origen_id` (`SourceType`/`SourceReferenceId`), con un índice único
`IX_Caso_tipo_origen_origen_id` que impide que el mismo origen genere más de un expediente. RF-06
define cuatro orígenes posibles:

| Origen (`tipo_origen`) | Cómo se genera | Endpoint |
|---|---|---|
| `BPM_REQUEST` | Solicitud de empresa enviada (RF-05) | `POST /api/bpm-requests/{id}/submit` |
| `INSTITUTIONAL` | Programación institucional directa | `POST /api/cases/institutional` |
| `ALERT` | Alerta LAPCH decidida como `PROCEED` | `POST /api/alerts/{id}/decision` |
| `COMPLAINT` | Denuncia decidida como `PROCEED` | `POST /api/complaints/{id}/decision` |

`Programacion_Institucional` (`InstitutionalScheduling`) es la tabla de origen del cuarto escenario:
`ADMINISTRADOR` o `COORDINADOR` programan directamente una evaluación sin que exista una solicitud,
alerta o denuncia previa. Guarda `empresa_id`, `motivo` (obligatorio) y `observaciones` (opcional),
igual que el resto de los orígenes documentan su justificación. A diferencia de `Alerta_LAPCH` y
`Denuncia`, no tiene un paso de decisión intermedio: `POST /api/cases/institutional` crea la fila de
`Programacion_Institucional` y el `Caso` (`PENDING_ASSIGNMENT`) en la misma operación, porque la
programación institucional no requiere evaluar si "procede" — la entidad que programa ya decidió que
la inspección debe realizarse. El expediente resultante recorre el mismo `CaseStateMachine` que
cualquier otro origen.

**Cierre de alertas y denuncias que no proceden (verificado, sin cambios de comportamiento):**
`Alerta_LAPCH.resultado` y `Denuncia.resultado` solo aceptan una decisión mientras están en
`PENDING`; `DecideAlertAsync`/`DecideComplaintAsync` comprueban `item.Status != "PENDING"` antes de
aplicar cualquier cambio, así que una alerta o denuncia ya decidida (`NOT_PROCEED`, `PROCEED` o, en
denuncias, `REFERRED`) queda en un estado terminal: una segunda llamada a `/decision` no cambia el
resultado, no reemplaza el motivo ni la fecha de decisión, y no puede generar un expediente adicional
(reforzado además por el índice único de `Caso` sobre `tipo_origen`/`origen_id`). `NOT_PROCEED` y
`REFERRED` son valores de resultado distintos y ambos son terminales; ninguno crea expediente. Esta
sección documenta el comportamiento existente, confirmado con pruebas explícitas
(`AlertAndComplaintEndpointTests`); no fue necesario corregir nada.

## Riesgo y frecuencia

Se manejan escalas separadas y versionadas en `Escala_Riesgo` y `Escala_Riesgo_Nivel`:

- Riesgo del alimento, escala `PRODUCT`: bajo `2`, medio `4`, alto `8`.
- Nivel descriptivo de frecuencia, escala `FREQUENCY`: bajo `1`, medio `2`, alto `3`.
- Factores del establecimiento: opciones `1`, `1.67`, `2.33` y `3`.

Ninguna de las dos escalas se deriva de la otra. La escala del producto determina `RP` y la escala
de frecuencia solo clasifica el resultado en la matriz de inspección.

`Version_Regla_Riesgo` es la unidad de versionado. Cada versión referencia la escala de producto y
la escala de frecuencia que utiliza, declara el método de agregación (`MAX`, único valor admitido) y
agrupa sus factores (`Factor_Riesgo_Establecimiento.version_regla_id`) y sus bandas
(`Matriz_Frecuencia_Inspeccion.version_regla_id`). Reglas de la versión publicada:

- Debe tener exactamente seis factores activos y la suma de sus pesos debe ser exactamente `1`.
- No se puede calcular con una versión no publicada, inactiva o fuera de vigencia.
- Un cálculo conserva `version_regla_id` y el detalle completo en `detalle_factores`. El registro es
  inmutable: se protege en la aplicación y con el disparador `tr_calculo_riesgo_inmutable`.

`Subcategoria_Alimento_Peligro` normaliza el riesgo por tipo de peligro (`MICROBIOLOGICAL`,
`CHEMICAL`) apuntando a un nivel de la escala del producto, con `puntaje_propio` opcional para
sobreescrituras justificadas. `RP` es el mayor de los peligros conocidos de las subcategorías que
elabora la empresa. Si una subcategoría no tiene peligros registrados, el cálculo se rechaza; nunca
se sustituye por el `puntaje_total` heredado en `Subcategoria_Alimento`, que se conserva solo como
dato histórico de la carga anterior.

Factores vigentes del establecimiento:

| Factor | Peso |
|---|---:|
| Volumen de producción | 16% |
| Implementación HACCP | 9% |
| Cumplimiento BPM | 56% |
| Proveedor INABIE | 5% |
| Rechazos sanitarios microbiológicos | 6% |
| Plan microbiológico y laboratorio | 8% |

```text
RE = suma(puntaje_opcion * peso_factor)
RP = mayor riesgo válido de los alimentos elaborados
RT = RP * RE
```

El diagrama E/R y el backend existente establecen que prevalece el mayor riesgo microbiológico o químico. La fórmula `AVERAGE(D,F)` encontrada en `Matriz Riesgo Alimentos.xlsx` se registra como inconsistencia de la fuente y no se usa para el cálculo productivo.

| Riesgo total | Nivel | Frecuencia |
|---|---|---|
| 1.0 a 3.6, ambos inclusive | Bajo | 12 meses |
| Mayor de 3.6 y hasta 6.3 | Medio | 6 meses |
| Mayor de 6.3 | Alto | 3 meses |

Las bandas se guardan con `riesgo_min`, `riesgo_min_incluido` y `riesgo_max`. Solo la primera banda
incluye su mínimo; las demás lo excluyen, de modo que `3.6` pertenece a la banda baja y `6.3` a la
media. Las bandas no pueden solaparse ni dejar huecos dentro de una versión de reglas.

## Importación y calidad de datos

Toda fuente externa entra primero a un lote de preparación con nombre de archivo, hoja, fila o celda, hash, contenido original, estado y detalle del error. La promoción al modelo productivo es atómica e idempotente.

La preparación de una plantilla usa `Plantilla_Importacion_Lote` (archivo, hoja, hash del contenido,
estado, usuario y plantilla resultante) y `Plantilla_Importacion_Fila` (celda de origen, código,
código del padre, descripción, tipo de ítem, orden, estado y detalle del error). Los estados son
`PENDIENTE`, `PROMOVIDO` y `RECHAZADO`. La promoción la realiza `sp_importar_plantilla_bpm`, que
rechaza el lote completo si hay filas sin texto, tipos de ítem desconocidos, padres inexistentes o
ciclos, y que devuelve la plantilla ya creada cuando el lote fue promovido antes.

El contenido normativo de la ficha reside en una sola fuente, `BpmTemplateData`, que alimenta el
proceso idempotente `BpmTemplateSeeder`. `scripts/import-bpm-template.sql` es la vía equivalente
para el equipo de datos: promueve un lote ya cargado y publica la versión resultante, sin repetir
los 90 nodos, para que no puedan divergir dos copias del mismo texto normativo. Ese script sustituye
al `scripts/seed-bpm-template.sql` preliminar, que parafraseaba las preguntas y aplanaba los
agrupadores y quedó eliminado.

El importador de `AllItems` debe detectar y documentar:

- Los tres códigos repetidos `1.1.1.1`.
- El código repetido `3.1.3`.
- Los padres inexistentes `1.2.2` y `3.1.2`.
- El consecutivo ausente `54`.
- Seis descripciones truncadas a 255 caracteres.
- Los elementos ausentes de los capítulos 5.4, 6 y 7 en el seed preliminar.

La matriz de alimentos contiene filas sin riesgo, valores químicos sin puntaje, una subcategoría vacía y una subcategoría con el valor `p`. Esas filas permanecen rechazadas en staging hasta corrección; el sistema no inventa datos normativos.

El importador de la matriz de alimentos recibe la extracción textual de la hoja
`Categorización_de_alimentos` y devuelve las filas aprovechables junto con las rechazadas y su
motivo. Motivos implementados:

| Motivo | Condición en la fuente |
|---|---|
| Categoría ausente | La fila no tiene categoría, incluidos los `#N/A` |
| Subcategoría ausente | La fila tiene categoría pero no subcategoría |
| Subcategoría incompleta o marcador sin significado | Texto de menos de tres caracteres, como el valor `p` |
| Riesgo microbiológico ausente | Falta el nivel o el puntaje microbiológico |
| Riesgo químico sin puntaje | Hay nivel químico sin puntaje |
| Puntaje químico sin nivel de riesgo | Hay puntaje químico sin nivel |
| Nivel de riesgo desconocido | El nivel no es `BAJO`, `MEDIO` ni `ALTO` |
| Puntaje inconsistente con el nivel | El puntaje no corresponde a `2`, `4` u `8` según el nivel |

Solo las filas aceptadas se promueven a `Subcategoria_Alimento` y `Subcategoria_Alimento_Peligro`.
La columna de riesgo total de la fuente se conserva como referencia y se marca cuando difiere del
máximo, porque la hoja original promedia los puntajes. La carga es idempotente: repetir la
importación no duplica escalas, versiones, factores, bandas, subcategorías ni peligros.

Los archivos fuente no se versionan; la extracción se entrega al importador en el momento de la
carga. Por eso el proceso no se ejecuta automáticamente al iniciar la aplicación.

## Catálogos generales y perfil de usuario

`GET /api/catalogs/{code}` expone catálogos de apoyo por código, pensado para poblar listas de
selección de la interfaz sin publicar un endpoint dedicado por cada catálogo. Códigos disponibles:

| Código | Origen | Contenido |
|---|---|---|
| `NIVELES_RIESGO` | Tabla `Nivel_Riesgo` | Los mismos niveles administrados por `/api/catalogs/risk-levels`. |
| `CATEGORIAS_ALIMENTO` | Tabla `Categoria_Alimento` | Las mismas categorías administradas por `/api/catalogs/food-categories`. |
| `ROLES_SISTEMA` | Tabla `AspNetRoles` (ASP.NET Identity) | Los cinco roles del sistema: `ADMINISTRADOR`, `ADMINISTRADOR_EMPRESA`, `USUARIO_DELEGADO`, `COORDINADOR`, `TECNICO_EVALUADOR`. |
| `ESTADOS_SOLICITUD` | Constante `BpmRequestStatuses.All` | Estados de la solicitud BPM: `DRAFT`, `SUBMITTED`. |
| `ESTADOS_CASO` | Constante `CaseStatuses.All` | Estados del expediente de inspección, los mismos que administra `CaseStateMachine`. |

Un código no reconocido responde `404`. `Nivel_Riesgo` y `Categoria_Alimento` no tienen todavía una
columna de vigencia o baja lógica, así que mientras eso no cambie todas sus filas se consideran
activas. `ROLES_SISTEMA`, `ESTADOS_SOLICITUD` y `ESTADOS_CASO` no tienen tabla propia: los primeros
viven en la tabla de identidad y los estados son el contrato ya publicado por las constantes de
dominio correspondientes; se sirven desde el mismo endpoint para no duplicar la lista en el
frontend, sin crear una tabla nueva para datos que no son normativos.

Se evaluó un catálogo `TIPOS_ESTABLECIMIENTO`, pero el sistema no tiene una tabla de tipos de
establecimiento: `Solicitud_BPM.tipo_establecimiento` es un campo de texto libre y ninguna de las
fuentes entregadas define una lista cerrada de valores. No se inventa esa tabla; queda pendiente
hasta que una fuente normativa la defina.

`GET /api/users/me` devuelve la identidad del usuario autenticado, su único rol y las empresas
autorizadas reales, obtenidas con el mismo join que expone `fn_perfil_usuario`
(`Empresa_Usuario` → `Empresa`). Los roles que no se asocian a una empresa concreta
(`ADMINISTRADOR`, `COORDINADOR`, `TECNICO_EVALUADOR`) no tienen filas en `Empresa_Usuario`, así que
obtienen una lista vacía de empresas autorizadas; no se les asigna "todas las empresas" porque el
SRS no describe esa regla y el sistema debe ser conservador ante negocio no especificado.

## Rutinas de base de datos

Funciones de lectura y cálculo:

- `fn_plantilla_arbol(plantilla_id)` devuelve el árbol ordenado de los ítems activos con su
  identificador, padre, código, descripción, tipo, orden, nivel y ruta jerárquica.
- `fn_perfil_usuario(usuario_id)` devuelve `usuario_id`, `correo`, `nombre_completo`, `rol`,
  `empresa_id` y `razon_social`: una fila por cada empresa autorizada del usuario, o una única fila
  con `empresa_id`/`razon_social` en `NULL` cuando el rol no está ligado a ninguna empresa.
- `fn_catalogo_opciones(catalogo_codigo)` devuelve opciones activas.
- `fn_calcular_resultado_bpm(evaluacion_id)` devuelve puntos, denominador, porcentaje y no conformidades.
- `fn_buscar_historial(...)` devuelve el read model histórico filtrado.

Procedimientos transaccionales:

- `sp_importar_plantilla_bpm(lote_id, usuario_id, INOUT plantilla_id)` valida el lote de
  preparación —filas con texto, tipos de ítem conocidos, padres presentes en el mismo lote y
  ausencia de ciclos—, crea la versión en borrador, inserta los ítems resolviendo los padres por
  código, declara las opciones C, CP, IT y NA y marca el lote como promovido. Repetirlo sobre un
  lote ya promovido devuelve la misma plantilla y no duplica nada.
- `sp_publicar_plantilla(plantilla_id, usuario_id)` comprueba que la versión esté en borrador, que
  tenga preguntas con peso positivo, que ningún ítem tenga un padre ajeno a la plantilla y que las
  cuatro opciones evaluables existan; luego la publica y la inmoviliza.
- `sp_registrar_calculo_riesgo(empresa_id, version_regla_id, riesgo_producto, riesgo_establecimiento, detalle_factores, generado_por, INOUT calculo_id)`
  comprueba que la versión de reglas exista, esté publicada, activa y vigente; que tenga seis
  factores activos con pesos que sumen `1`; calcula `RT` redondeado a tres decimales; localiza la
  banda de frecuencia de esa versión e inserta el cálculo con su snapshot en una sola transacción.
  Rechaza un riesgo de producto nulo o no positivo, es decir, un producto sin peligro conocido.
- `sp_asignar_tecnico(caso_id, tecnico_id, usuario_id, motivo)` conserva el historial.
- `sp_programar_evaluacion(...)` crea, reprograma o cancela sin perder historial.
- `sp_guardar_respuestas(...)` aplica control de versión e idempotencia.
- `sp_enviar_evaluacion(...)` calcula y bloquea la evaluación.
- `sp_revisar_informe(...)` registra aprobación, devolución o corrección.
- `sp_cerrar_expediente(...)` comprueba el informe oficial y cierra de forma inmutable.

Disparadores:

- `tr_calculo_riesgo_inmutable` sobre `Calculo_Riesgo` impide actualizar o eliminar un cálculo
  registrado mediante la función `fn_calculo_riesgo_inmutable()`.
- `tr_plantilla_publicada_inmutable` sobre `Plantilla_Evaluacion`, `Plantilla_Evaluacion_Item`,
  `Plantilla_Evaluacion_Opcion`, `Plantilla_Evaluacion_Regla` y `Plantilla_Evaluacion_Criterio`
  impide alterar una versión publicada mediante la función `fn_plantilla_publicada_inmutable()`.
  Solo admite la transición de `DRAFT` a `PUBLISHED` en la cabecera.
- `tr_empresa_historial_inmutable` sobre `Empresa_Historial` impide actualizar o eliminar una fila
  de historial de empresa registrada, mediante la función `fn_empresa_historial_inmutable()`.

Los nombres anteriores representan el contrato estable. Su creación se versiona dentro de una migración de EF Core.

Rutinas ya creadas por migraciones: `sp_registrar_calculo_riesgo`, `fn_calculo_riesgo_inmutable` y
`tr_calculo_riesgo_inmutable`, incorporadas en `AddVersionedRiskRules`; `fn_plantilla_arbol`,
`sp_importar_plantilla_bpm`, `sp_publicar_plantilla`, `fn_plantilla_publicada_inmutable` y
`tr_plantilla_publicada_inmutable`, incorporadas en `AddBpmTemplateCatalogs`; `fn_perfil_usuario`,
incorporada en `AddUserProfileFunction`; `fn_empresa_historial_inmutable` y
`tr_empresa_historial_inmutable`, incorporadas en `AddCompanyProfileAndHistory`. Las tablas
`Documento_Registro_Usuario` y `Solicitud_BPM_Documento` se incorporaron en
`AddRegistrationAndBpmRequestDocuments`; no requirieron rutinas nuevas porque su validación de
presencia se resuelve con una consulta simple en el endpoint. El resto sigue
pendiente de las tareas correspondientes, incluida `fn_catalogo_opciones`: los catálogos generales
descritos arriba se resuelven hoy con consultas EF equivalentes porque las pruebas de integración
corren sobre el proveedor en memoria, que no ejecuta funciones de PostgreSQL.

## Instalación y actualización

```powershell
dotnet ef database update `
  --project "backend\src\EBR.Infrastructure\EBR.Infrastructure.csproj" `
  --startup-project "backend\src\EBR.Api\EBR.Api.csproj"
```

No se deben crear tablas manualmente en pgAdmin. pgAdmin se utiliza para inspeccionar el resultado de las migraciones, ejecutar consultas de diagnóstico y visualizar el diagrama E/R.

## Respaldo y portabilidad

La aplicación no depende de rutas locales. La cadena de conexión y secretos se suministran mediante variables de entorno. Un entorno nuevo se reconstruye con el código, las migraciones y los seeds versionados. Los respaldos se generan con `pg_dump` y se restauran con `pg_restore` en una versión compatible de PostgreSQL.

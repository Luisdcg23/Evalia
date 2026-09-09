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

La ficha oficial contiene 90 nodos: 7 capítulos, 13 secciones, 19 subsecciones, 6 agrupadores y 45 preguntas. Las opciones evaluables son:

| Código | Valor | Efecto |
|---|---:|---|
| C | 1.0 | Cumple |
| CP | 0.5 | Cumplimiento parcial |
| IT | 0.0 | Incumplimiento total |
| NA | nulo | Se excluye del denominador |

La guía de llenado se normaliza en criterios asociados a cada pregunta. Cada criterio puede tener criticidad crítica, mayor o menor y conserva la referencia de hoja y celda de origen.

El porcentaje BPM se calcula así:

```text
puntos = suma(valor de respuesta por pregunta aplicable)
denominador = suma(peso de preguntas aplicables)
porcentaje = puntos / denominador * 100
```

Una evaluación sin todas las respuestas obligatorias permanece en estado pendiente; nunca se representa como cero ni como error matemático.

## Riesgo y frecuencia

Se manejan escalas separadas:

- Riesgo del alimento: bajo `2`, medio `4`, alto `8`.
- Nivel descriptivo de frecuencia: bajo `1`, medio `2`, alto `3`.
- Factores del establecimiento: opciones `1`, `1.67`, `2.33` y `3`.

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

## Importación y calidad de datos

Toda fuente externa entra primero a un lote de preparación con nombre de archivo, hoja, fila o celda, hash, contenido original, estado y detalle del error. La promoción al modelo productivo es atómica e idempotente.

El importador de `AllItems` debe detectar y documentar:

- Los tres códigos repetidos `1.1.1.1`.
- El código repetido `3.1.3`.
- Los padres inexistentes `1.2.2` y `3.1.2`.
- El consecutivo ausente `54`.
- Seis descripciones truncadas a 255 caracteres.
- Los elementos ausentes de los capítulos 5.4, 6 y 7 en el seed preliminar.

La matriz de alimentos contiene filas sin riesgo, valores químicos sin puntaje, una subcategoría vacía y una subcategoría con el valor `p`. Esas filas permanecen rechazadas en staging hasta corrección; el sistema no inventa datos normativos.

## Rutinas de base de datos

Funciones de lectura y cálculo:

- `fn_plantilla_arbol(plantilla_id)` devuelve el árbol ordenado.
- `fn_perfil_usuario(usuario_id)` devuelve identidad, rol y empresas autorizadas.
- `fn_catalogo_opciones(catalogo_codigo)` devuelve opciones activas.
- `fn_calcular_resultado_bpm(evaluacion_id)` devuelve puntos, denominador, porcentaje y no conformidades.
- `fn_buscar_historial(...)` devuelve el read model histórico filtrado.

Procedimientos transaccionales:

- `sp_importar_plantilla_bpm(lote_id, usuario_id)` valida y promueve staging.
- `sp_publicar_plantilla(plantilla_id, usuario_id)` valida e inmoviliza una versión.
- `sp_registrar_calculo_riesgo(...)` verifica la versión de reglas y guarda el snapshot.
- `sp_asignar_tecnico(caso_id, tecnico_id, usuario_id, motivo)` conserva el historial.
- `sp_programar_evaluacion(...)` crea, reprograma o cancela sin perder historial.
- `sp_guardar_respuestas(...)` aplica control de versión e idempotencia.
- `sp_enviar_evaluacion(...)` calcula y bloquea la evaluación.
- `sp_revisar_informe(...)` registra aprobación, devolución o corrección.
- `sp_cerrar_expediente(...)` comprueba el informe oficial y cierra de forma inmutable.

Los nombres anteriores representan el contrato estable. Su creación se versiona dentro de una migración de EF Core.

## Instalación y actualización

```powershell
dotnet ef database update `
  --project "backend\src\EBR.Infrastructure\EBR.Infrastructure.csproj" `
  --startup-project "backend\src\EBR.Api\EBR.Api.csproj"
```

No se deben crear tablas manualmente en pgAdmin. pgAdmin se utiliza para inspeccionar el resultado de las migraciones, ejecutar consultas de diagnóstico y visualizar el diagrama E/R.

## Respaldo y portabilidad

La aplicación no depende de rutas locales. La cadena de conexión y secretos se suministran mediante variables de entorno. Un entorno nuevo se reconstruye con el código, las migraciones y los seeds versionados. Los respaldos se generan con `pg_dump` y se restauran con `pg_restore` en una versión compatible de PostgreSQL.

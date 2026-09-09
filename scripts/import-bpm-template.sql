-- Importación de la Ficha de Inspección BPM a partir de un lote de preparación.
--
-- El contenido normativo de la ficha no se repite aquí: vive en una sola fuente,
-- `EBR.Infrastructure.Evaluations.BpmTemplateData`, que es la que cargan tanto el seeder de la
-- aplicación (`BpmTemplateSeeder`) como las pruebas. Este script es la vía equivalente para el
-- equipo de datos: promueve un lote ya escrito en `Plantilla_Importacion_Lote` y
-- `Plantilla_Importacion_Fila` y publica la versión resultante.
--
-- Requisitos previos: las migraciones de EF Core aplicadas, incluida `AddBpmTemplateCatalogs`,
-- que crea las tablas de preparación y las rutinas `fn_plantilla_arbol`,
-- `sp_importar_plantilla_bpm` y `sp_publicar_plantilla`.
--
-- Uso:
--   psql -v lote_id=1 -v usuario_id="'00000000-0000-0000-0000-000000000000'" -f scripts/import-bpm-template.sql
--
-- Es idempotente: repetir la ejecución sobre un lote ya promovido no crea otra plantilla ni
-- vuelve a publicar la versión existente.

\set ON_ERROR_STOP on

BEGIN;

DO $importacion$
DECLARE
    v_lote_id integer := :lote_id;
    v_usuario_id uuid := :usuario_id;
    v_lote record;
    v_plantilla_id integer;
    v_estado text;
    v_capitulos integer;
    v_secciones integer;
    v_subsecciones integer;
    v_agrupadores integer;
    v_preguntas integer;
BEGIN
    SELECT * INTO v_lote FROM "Plantilla_Importacion_Lote" WHERE "Id" = v_lote_id FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El lote de importación % no existe. Cárguelo antes de ejecutar este script.', v_lote_id;
    END IF;

    CALL sp_importar_plantilla_bpm(v_lote_id, v_usuario_id, v_plantilla_id);

    SELECT estado INTO v_estado FROM "Plantilla_Evaluacion" WHERE "Id" = v_plantilla_id;
    IF v_estado = 'DRAFT' THEN
        CALL sp_publicar_plantilla(v_plantilla_id, v_usuario_id);
        RAISE NOTICE 'Plantilla % publicada a partir del lote %.', v_plantilla_id, v_lote_id;
    ELSE
        RAISE NOTICE 'La plantilla % ya estaba en estado %; no se realizaron cambios.', v_plantilla_id, v_estado;
    END IF;

    SELECT
        count(*) FILTER (WHERE tipo_item = 'CHAPTER'),
        count(*) FILTER (WHERE tipo_item = 'SECTION'),
        count(*) FILTER (WHERE tipo_item = 'SUBSECTION'),
        count(*) FILTER (WHERE tipo_item = 'GROUP'),
        count(*) FILTER (WHERE tipo_item = 'QUESTION')
    INTO v_capitulos, v_secciones, v_subsecciones, v_agrupadores, v_preguntas
    FROM fn_plantilla_arbol(v_plantilla_id);

    RAISE NOTICE 'Árbol importado: % capítulos, % secciones, % subsecciones, % agrupadores y % preguntas.',
        v_capitulos, v_secciones, v_subsecciones, v_agrupadores, v_preguntas;

    IF v_preguntas = 0 THEN
        RAISE EXCEPTION 'La plantilla % quedó sin preguntas evaluables.', v_plantilla_id;
    END IF;
END;
$importacion$;

COMMIT;

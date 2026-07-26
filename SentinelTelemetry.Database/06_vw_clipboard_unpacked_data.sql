/*      
    Descripción: Extrae las el tipo de contenido que se ha copiado. 
	Si es texto, cueanta la longitud del texto. Si no asigna null.
    Además de las demás columnas que se encuentran fuera del JSON.     
*/ 
ALTER VIEW vw_clipboard_unpacked_data AS
	SELECT
		E.id_event,
		E.[timestamp],
		E.id_source,
		E.id_category,
		E.id_version,
		E.event_type,
		JSON_VALUE(data_json, '$.ContentType') Tipo_Contenido,
		JSON_VALUE(data_json, '$.ContentLength') Longitud_Contenido
	FROM Events E
	JOIN Source S ON S.id_source = E.id_source
	WHERE S.source_name = 'ClipboardCollector'
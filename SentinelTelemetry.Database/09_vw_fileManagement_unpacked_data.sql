/*      
    Descripción: Extrae del JSON la ruta donde se está haciendo el cambio al archivo. También la
	ruta antigua, sino existe una, entonces asiga null.
	Ambas hacia una columna por separado.
    Además de las demás columnas que se encuentran fuera del JSON.     
*/ 
CREATE VIEW vw_fileManagement_unpacked_data AS
	SELECT 
		E.id_event,
		E.[timestamp],
		E.id_source,
		E.id_category,
		E.id_version,
		E.event_type,
		JSON_VALUE(data_json, '$.OldFilePath') AS RutaAnterior,
		JSON_VALUE(data_json, '$.FilePath') AS RutaActual		
	FROM Events E
	JOIN Source S ON S.id_source = E.id_source 
	WHERE S.source_name = 'FileCollector'
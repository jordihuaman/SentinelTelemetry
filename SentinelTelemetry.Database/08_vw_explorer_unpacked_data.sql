/*      
    Descripción: Extrae del JSON la ruta de donde se está navegando en el explorador de archivos.
	Esto hacia una columna.
    Además de las demás columnas que se encuentran fuera del JSON.     
*/ 
CREATE VIEW vw_explorer_unpacked_data AS
	SELECT 
		E.id_event,
		E.[timestamp],
		E.id_source,
		E.id_category,
		E.id_version,
		E.event_type,
		JSON_VALUE(data_json, '$.FolderPath') AS Ruta
	FROM Events E
	JOIN Source S ON S.id_source = E.id_source 
	WHERE S.source_name = 'ExplorerCollector'

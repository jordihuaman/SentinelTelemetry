/*      
    Descripción: Extrae el nombre del programa que se está utilizando del JSON
	a una columna.
    Además de las demás columnas que se encuentran fuera del JSON.     
*/ 
CREATE VIEW vw_applicationCycle_unpacked_data AS
	SELECT 
		E.id_event,
		E.[timestamp],
		E.id_source,
		E.id_category,
		E.id_version,
		E.event_type,
		JSON_VALUE(data_json, '$.ProcessName') AS Programa
	FROM Events E
	JOIN Source S ON S.id_source = E.id_source 
	WHERE S.source_name = 'Application'
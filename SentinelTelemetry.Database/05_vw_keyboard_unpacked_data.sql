/*      
    Descripción: Extrae las pulsaciones por segundo de las teclas del teclado.       
    Esos atributos traídos del Json, hacia columnas.      
    Además de las demás columnas que se encuentran fuera del JSON.     
*/  
CREATE VIEW vw_keyboard_unpacked_data AS
	SELECT
		E.id_event,
		E.[timestamp],
		E.id_source,
		E.id_category,
		E.id_version,
		E.event_type,
		CAST(JSON_VALUE(data_json, '$.KeyPressCount') AS INT) AS Pulsaciones
	FROM Events E
	JOIN Source S ON S.id_source = E.id_source
	WHERE S.source_name = 'Keyboard'
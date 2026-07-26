/*  
    Descripción: Extrae el nombre del proceso. El titulo de la ventana en uso en dicho momento. Desde el Json hacia columnas.
	Además de el tiempo de inicio, tiempo final. Y la diferencia entre estos, equivale a la duracion. Dentro una nueva columna en segundos.  
*/
CREATE VIEW vw_window_activity_duration AS
	SELECT 
		JSON_VALUE(E.data_json, '$.ProcessName') AS Process_Name,
		JSON_VALUE(E.data_json, '$.WindowTitle') AS Window_Title,

		E.timestamp Start_Time,
		LEAD(E.timestamp) OVER (ORDER BY E.timestamp) AS End_Time,

		DATEDIFF(SECOND, E.timestamp, LEAD(e.timestamp) OVER (ORDER BY E.timestamp)) Duration_Seconds
	FROM Events E
	WHERE e.event_type = 'ActiveWindowSnapshot';
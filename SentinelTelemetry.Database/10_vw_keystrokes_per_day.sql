/*      
    Descripción: A partir de la vista de keyboard. Genera resumen de cuantas pulsaciones por día
	se generan. 
*/ 
CREATE VIEW vw_keystrokes_per_day AS
	SELECT 
		CAST(DATEADD(HOUR, -5, [timestamp]) AS DATE) Fecha,
		event_type,
		SUM(Pulsaciones) Pulsaciones_Dia
	FROM vw_keyboard_unpacked_data
	GROUP BY
		CAST(DATEADD(HOUR, -5, [timestamp]) AS DATE),
		event_type
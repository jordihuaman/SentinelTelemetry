/*      
    Descripción: A partir de la view de keyboard con datos crudos. Crea metricas que reflejan
	la velocidad de escritura KPS, o KPM, la duracion de lo segundos y las teclas presionadas. 
	Con CTEs se hacen los cálculos. Dentro de una View.
*/ 
CREATE VIEW vw_keyboard_typing_metrics AS
	WITH Peru_Timestamp AS (
		SELECT
			DATEADD(HOUR,-5,timestamp) Peru_Time,
			Pulsaciones
		FROM vw_keyboard_unpacked_data
	), Detect_Gaps AS (
		SELECT
			Peru_Time,
			Pulsaciones,
			LAG(Peru_Time) OVER (ORDER BY Peru_Time) Previous
		FROM Peru_Timestamp
	), New_Sessions AS (
		SELECT 
			Peru_Time,
			Pulsaciones,
			CASE
				WHEN Previous IS NULL THEN 1
				WHEN DATEDIFF(MILLISECOND, Previous, Peru_Time) >= 2000 THEN 1
				ELSE 0
			END Session_Created
		FROM Detect_Gaps
	), Naming_Sessions AS (
		SELECT 
			Peru_Time,
			Pulsaciones,
			SUM(Session_Created) OVER (ORDER BY Peru_Time) SessionId
		FROM New_Sessions 
	)
		SELECT 
			CAST(MIN(Peru_Time) AS DATE) Fecha_Peru,
			MIN(Peru_Time) Start_Time,
			MAX(Peru_Time) End_Time,
			COALESCE(NULLIF(DATEDIFF(SECOND, MIN(Peru_Time), MAX(Peru_Time)),0),1) Duration_Seconds,
			SUM(Pulsaciones) Keystrokes,
			ROUND(CAST(SUM(Pulsaciones) AS FLOAT)/COALESCE(NULLIF(DATEDIFF(SECOND, MIN(Peru_Time), MAX(Peru_Time)), 0), 1), 2) KPS,
			ROUND((CAST(SUM(Pulsaciones) AS FLOAT)/COALESCE(NULLIF(DATEDIFF(SECOND, MIN(Peru_Time), MAX(Peru_Time)), 0), 1)) * 60, 2) KPM
			FROM Naming_Sessions
		GROUP BY SessionId
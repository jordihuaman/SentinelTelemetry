/*        
    Descripción: Revisa que a que applicacion te diriges luego de salir de la actual.
*/  
CREATE VIEW vw_transitions_between_apps AS
    WITH Transiciones AS (
        SELECT
            Aplicacion AS AppActual,
            LEAD(Aplicacion) OVER (ORDER BY [timestamp]) AS AppSiguiente
        FROM vw_window_unpacked_data
    ),
    TransicionesReales AS (
        SELECT AppActual, AppSiguiente
        FROM Transiciones
        WHERE AppSiguiente IS NOT NULL
          AND AppSiguiente <> AppActual
    ),
    ConteoTransiciones AS (
        SELECT
            AppActual,
            AppSiguiente,
            COUNT(*) AS Frecuencia
        FROM TransicionesReales
        GROUP BY AppActual, AppSiguiente
    ),
    TotalSalidasPorApp AS (
        SELECT AppActual, SUM(Frecuencia) AS TotalSalidas
        FROM ConteoTransiciones
        GROUP BY AppActual
    )
    SELECT
        c.AppActual,
        c.AppSiguiente,
        c.Frecuencia,
        t.TotalSalidas,
        ROUND(100.0 * c.Frecuencia / t.TotalSalidas, 1) AS PorcentajeDeSalidas
    FROM ConteoTransiciones c
    JOIN TotalSalidasPorApp t ON t.AppActual = c.AppActual;
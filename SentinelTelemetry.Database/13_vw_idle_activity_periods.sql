/*        
    Descripción: Usa la informacion de idle para calcular el tiempo en segundos 
    de actividad real.
*/  
CREATE VIEW vw_idle_activity_periods AS
    WITH IdleEvents AS (
        SELECT E.[timestamp], E.event_type
        FROM Events E
        JOIN Source S ON S.id_source = E.id_source
        WHERE S.source_name = 'Idle'
    ),
    IdlePeriods AS (
        SELECT
            [timestamp] AS Inicio,
            LEAD([timestamp]) OVER (ORDER BY [timestamp]) AS Fin
        FROM IdleEvents
        WHERE event_type = 'IdleStarted'
    ),
    ActiveStreaks AS (
        SELECT
            [timestamp] AS Inicio,
            LEAD([timestamp]) OVER (ORDER BY [timestamp]) AS Fin
        FROM IdleEvents
        WHERE event_type = 'IdleEnded'
    )
    SELECT 'Idle' AS TipoPeriodo, Inicio, Fin,
           DATEDIFF(SECOND, Inicio, Fin) AS DuracionSegundos
    FROM IdlePeriods
    WHERE Fin IS NOT NULL

    UNION ALL

    SELECT 'Activo' AS TipoPeriodo, Inicio, Fin,
           DATEDIFF(SECOND, Inicio, Fin) AS DuracionSegundos
    FROM ActiveStreaks
    WHERE Fin IS NOT NULL;
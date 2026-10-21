/*        
    Descripción: Usa la información de idle para calcular el tiempo real
    de actividad e inactividad.

    Regla:
      - Un evento IdleStarted marca el inicio de un periodo "Idle".
      - Un evento IdleEnded marca el inicio de un periodo "Activo".
      - Cada periodo termina en el siguiente evento, sea del tipo que sea.
      - Así, Idle + Activo cubre la línea de tiempo exactamente una vez.

*/
CREATE OR ALTER VIEW vw_idle_activity_periods AS
WITH IdleEvents AS (
    SELECT
        E.[timestamp],
        E.event_type
    FROM Events E
    JOIN Source S ON S.id_source = E.id_source
    WHERE S.source_name = 'Idle'
      AND E.event_type IN ('IdleStarted', 'IdleEnded')
),
Ordered AS (
    SELECT
        [timestamp] AS Inicio,
        LEAD([timestamp]) OVER (ORDER BY [timestamp]) AS Fin,
        event_type
    FROM IdleEvents
)
SELECT
    CASE event_type
        WHEN 'IdleStarted' THEN 'Idle'
        WHEN 'IdleEnded'  THEN 'Activo'
    END AS TipoPeriodo,
    Inicio,
    Fin,
    DATEDIFF(SECOND, Inicio, Fin) AS DuracionSegundos
FROM Ordered
WHERE Fin IS NOT NULL;

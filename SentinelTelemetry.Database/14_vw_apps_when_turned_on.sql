/*        
    Descripción: Revisa que aplicaciones habro normalmente al Login/Unlock
*/  
CREATE VIEW vw_apps_when_turned_on AS
    WITH SessionStarts AS (
        SELECT E.[timestamp] AS Inicio
        FROM Events E
        JOIN Source S ON S.id_source = E.id_source
        WHERE S.source_name = 'Session'
          AND E.event_type IN ('Login', 'Unlock')
    ),
    AppOpens AS (
        SELECT Programa, [timestamp] AS OpenedAt
        FROM vw_applicationCycle_unpacked_data
        WHERE event_type = 'Opened'
    )
    SELECT
        ss.Inicio AS SesionInicio,
        ao.Programa,
        ao.OpenedAt,
        DATEDIFF(SECOND, ss.Inicio, ao.OpenedAt) AS SegundosDesdeInicio
    FROM SessionStarts ss
    JOIN AppOpens ao
        ON ao.OpenedAt >= ss.Inicio
        AND ao.OpenedAt <= DATEADD(MINUTE, 4, ss.Inicio); 
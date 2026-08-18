/*          
    Descripción: Revisa que aplicaciones habro normalmente al Login/Unlock. Hace calculos  
    para reporte.  
*/    
CREATE VIEW vw_apps_when_turnedOn_metrics AS  
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
),  
AppsPorSesion AS (  
    SELECT  
        ss.Inicio AS SesionInicio,  
        ao.Programa  
    FROM SessionStarts ss  
    JOIN AppOpens ao  
        ON ao.OpenedAt >= ss.Inicio  
        AND ao.OpenedAt <= DATEADD(MINUTE, 5, ss.Inicio)  
),  
TotalSesiones AS (  
    SELECT COUNT(*) AS Total FROM SessionStarts  
)  
SELECT  
    aps.Programa,  
    COUNT(DISTINCT aps.SesionInicio) AS VecesQueAparece,  
    t.Total AS TotalSesiones,  
    ROUND(100.0 * COUNT(DISTINCT aps.SesionInicio) / t.Total, 1) AS PorcentajeDeSesiones  
FROM AppsPorSesion aps  
CROSS JOIN TotalSesiones t  
GROUP BY aps.Programa, t.Total;  
/*
    Índice de apoyo para las vistas que filtran eventos por fuente y los
    ordenan por timestamp (por ejemplo, vw_idle_activity_periods).

    Sin este índice, SQL Server puede intentar ordenar los ~281k eventos
    completos y quedarse sin memoria (RESOURCE_SEMAPHORE) en SQL Express.
*/
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Events_id_source_timestamp'
      AND object_id = OBJECT_ID('dbo.Events')
)
BEGIN
    CREATE INDEX IX_Events_id_source_timestamp
        ON dbo.Events (id_source, [timestamp])
        INCLUDE (event_type);
END;
GO

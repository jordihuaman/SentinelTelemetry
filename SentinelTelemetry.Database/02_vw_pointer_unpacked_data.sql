/*    
    Descripción: Extrae la posición x e y. Tipos de clic (izquierdo y derecho).     
    Esos atributos traídos del Json, hacia columnas.    
    Además de cuándo ocurrió, el tipo de evento.   
*/
CREATE VIEW vw_pointer_unpacked_data AS       
    SELECT       
        timestamp,      
        event_type,      
        CAST(JSON_VALUE(data_json, '$.X') AS INT) AS PosicionX,      
        CAST(JSON_VALUE(data_json, '$.Y') AS INT) AS PosicionY,      
        JSON_VALUE(data_json, '$.LeftClick') AS ClicIzquierdo,  
        JSON_VALUE(data_json, '$.RightClick') AS ClickDerecho  
    FROM       
        dbo.Events E      
    JOIN Source S ON s.id_source = E.id_source      
    WHERE S.source_name = 'Pointer'; 
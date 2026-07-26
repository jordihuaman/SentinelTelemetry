/*  
    Descripción: Extrae la posicion x e y. Tipo de click.    
    Esos tres atributos traidos del Json, hacia columnas.  
    Además de cuando ocurrió, el tipo de evento. 
*/  
CREATE VIEW vw_pointer_extracted_data AS   
    SELECT   
        timestamp,  
        event_type,  
        CAST(JSON_VALUE(data_json, '$.X') AS INT) AS PosicionX,  
        CAST(JSON_VALUE(data_json, '$.Y') AS INT) AS PosicionY,  
        JSON_VALUE(data_json, '$.LeftClick') AS ClicIzquierdo  
    FROM   
        dbo.Events E  
    JOIN Source S ON s.id_source = E.id_source  
    WHERE S.source_name = 'Pointer';

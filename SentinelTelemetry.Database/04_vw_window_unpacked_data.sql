/*  
    Descripción: Extrae el nombre de la aplicación que se está usando por el usuario en  
    primero plano. Tambien, extrae la deteccion de la ventana interna de esa aplicacion.  
    Además de las demás columnas que se encuentran fuera del JSON.      
*/  
CREATE VIEW vw_window_unpacked_data AS  
    SELECT   
     E.id_event,  
        E.[timestamp],  
        E.id_source,  
        E.id_category,  
        E.id_version,  
        E.event_type,  
     JSON_VALUE(data_json, '$.ProcessName') Aplicacion,  
     JSON_VALUE(data_json, '$.WindowTitle') Ventana_Interna  
    FROM Events E  
    JOIN Source S ON S.id_source = E.id_source   
    WHERE S.source_name = 'Window' 
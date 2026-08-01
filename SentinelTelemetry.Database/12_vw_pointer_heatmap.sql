/*        
    Descripción: Agrupa coordenadas cercanas en celdas de 10x10 píxeles              
*/    
CREATE VIEW vw_pointer_heatmap AS
    SELECT
        (PosicionX / 10) * 10 AS X_Bin,
        (PosicionY / 10) * 10 AS Y_Bin,
        COUNT(*) AS Densidad
    FROM vw_pointer_unpacked_data
    GROUP BY
        (PosicionX / 10) * 10,
        (PosicionY / 10) * 10;
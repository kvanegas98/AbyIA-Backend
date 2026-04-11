-- =============================================
-- SP: sp_HistorialChat_Obtener
-- Descripción: Obtiene las últimas N conversaciones de un usuario
-- =============================================
CREATE OR ALTER PROCEDURE dbo.sp_HistorialChat_Obtener
    @IdUsuario INT,
    @Cantidad  INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Cantidad)
        Id,
        Pregunta,
        RespuestaIA,
        Fecha,
        Exito
    FROM dbo.HistorialChat
    WHERE IdUsuario = @IdUsuario
    ORDER BY Fecha DESC;
END
GO

-- =============================================
-- SP: sp_HistorialChat_Guardar
-- Descripción: Inserta un registro en dbo.HistorialChat
-- =============================================
CREATE OR ALTER PROCEDURE dbo.sp_HistorialChat_Guardar
    @IdUsuario   INT,
    @Pregunta    NVARCHAR(MAX),
    @SqlGenerado NVARCHAR(MAX) = NULL,
    @RespuestaIA NVARCHAR(MAX) = NULL,
    @Exito       BIT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.HistorialChat (IdUsuario, Pregunta, SqlGenerado, RespuestaIA, Exito)
    VALUES (@IdUsuario, @Pregunta, @SqlGenerado, @RespuestaIA, @Exito);
END
GO

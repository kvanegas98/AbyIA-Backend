-- =============================================
-- Tabla: dbo.HistorialChat
-- Descripción: Auditoría del módulo Aby IA (Text-to-SQL)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.HistorialChat') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.HistorialChat (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        IdUsuario       INT NOT NULL,
        Pregunta        NVARCHAR(MAX) NOT NULL,
        SqlGenerado     NVARCHAR(MAX) NULL,
        RespuestaIA     NVARCHAR(MAX) NULL,
        Fecha           DATETIME NOT NULL DEFAULT GETDATE(),
        Exito           BIT NOT NULL DEFAULT 0,
        ModeloUsado     NVARCHAR(100) NULL,
        
        CONSTRAINT FK_HistorialChat_Usuario 
            FOREIGN KEY (IdUsuario) REFERENCES dbo.usuario(idusuario)
    );

    CREATE NONCLUSTERED INDEX IX_HistorialChat_IdUsuario 
        ON dbo.HistorialChat(IdUsuario);

    CREATE NONCLUSTERED INDEX IX_HistorialChat_Fecha 
        ON dbo.HistorialChat(Fecha DESC);
END
GO

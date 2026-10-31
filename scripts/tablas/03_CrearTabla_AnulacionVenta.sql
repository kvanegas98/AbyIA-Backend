-- =============================================
-- Tabla: dbo.AnulacionVenta
-- Descripción: Registro de anulaciones de ventas con motivo y usuario responsable
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.AnulacionVenta') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.AnulacionVenta (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        IdVenta         INT NOT NULL,
        IdUsuario       INT NOT NULL,
        IdSucursal      INT NOT NULL,
        Motivo          NVARCHAR(500) NOT NULL,
        FechaAnulacion  DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_AnulacionVenta_Venta
            FOREIGN KEY (IdVenta) REFERENCES dbo.venta(idventa),

        CONSTRAINT FK_AnulacionVenta_Usuario
            FOREIGN KEY (IdUsuario) REFERENCES dbo.usuario(idusuario),

        CONSTRAINT FK_AnulacionVenta_Sucursal
            FOREIGN KEY (IdSucursal) REFERENCES dbo.sucursal(idsucursal)
    );

    CREATE UNIQUE INDEX UX_AnulacionVenta_IdVenta
        ON dbo.AnulacionVenta(IdVenta);

    CREATE NONCLUSTERED INDEX IX_AnulacionVenta_IdUsuario
        ON dbo.AnulacionVenta(IdUsuario);

    CREATE NONCLUSTERED INDEX IX_AnulacionVenta_FechaAnulacion
        ON dbo.AnulacionVenta(FechaAnulacion DESC);
END
GO

USE [AdminVariedadesAby]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ingreso_imagen](
	[idingreso_imagen] [int] IDENTITY(1,1) NOT NULL,
	[idingreso] [int] NOT NULL,
	[url_imagen] [varchar](1000) NOT NULL,
	[modelo_ia] [nvarchar](100) NULL,
 CONSTRAINT [PK_ingreso_imagen] PRIMARY KEY CLUSTERED 
(
	[idingreso_imagen] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ingreso_imagen]  WITH CHECK ADD  CONSTRAINT [FK_ingreso_imagen_ingreso] FOREIGN KEY([idingreso])
REFERENCES [dbo].[ingreso] ([idingreso])
GO

ALTER TABLE [dbo].[ingreso_imagen] CHECK CONSTRAINT [FK_ingreso_imagen_ingreso]
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'PivotLayouts' AND xtype = 'U')
CREATE TABLE [dbo].[PivotLayouts] (
    [Id]          int IDENTITY(1, 1) NOT NULL,
    [RepCode]     nvarchar(50)       NOT NULL,
    [PageKey]     nvarchar(100)      NOT NULL,
    [ReportName]  nvarchar(200)      NOT NULL,
    [Report]      nvarchar(MAX)      NOT NULL,
    [CreatedAt]   datetime2(0)       NOT NULL  CONSTRAINT DF_PivotLayouts_CreatedAt DEFAULT (GETDATE()),
    [UpdatedAt]   datetime2(0)       NOT NULL  CONSTRAINT DF_PivotLayouts_UpdatedAt DEFAULT (GETDATE()),
    CONSTRAINT PK_PivotLayouts PRIMARY KEY CLUSTERED ([Id])
        WITH (PAD_INDEX = OFF, FILLFACTOR = 100, IGNORE_DUP_KEY = OFF,
              STATISTICS_NORECOMPUTE = OFF, ALLOW_ROW_LOCKS = ON,
              ALLOW_PAGE_LOCKS = ON, DATA_COMPRESSION = NONE)
        ON [PRIMARY]
)
ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PivotLayouts_RepCode_PageKey' AND object_id = OBJECT_ID('PivotLayouts'))
CREATE NONCLUSTERED INDEX [IX_PivotLayouts_RepCode_PageKey]
ON [dbo].[PivotLayouts] ([RepCode], [PageKey])
WITH (PAD_INDEX = OFF, FILLFACTOR = 100, IGNORE_DUP_KEY = OFF,
      STATISTICS_NORECOMPUTE = OFF, ONLINE = OFF,
      ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
      DATA_COMPRESSION = NONE)
ON [PRIMARY];
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_PivotLayouts_RepCode_PageKey_ReportName' AND object_id = OBJECT_ID('PivotLayouts'))
CREATE UNIQUE NONCLUSTERED INDEX [UQ_PivotLayouts_RepCode_PageKey_ReportName]
ON [dbo].[PivotLayouts] ([RepCode], [PageKey], [ReportName])
WITH (PAD_INDEX = OFF, FILLFACTOR = 100, IGNORE_DUP_KEY = OFF,
      STATISTICS_NORECOMPUTE = OFF, ONLINE = OFF,
      ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
      DATA_COMPRESSION = NONE)
ON [PRIMARY];
GO

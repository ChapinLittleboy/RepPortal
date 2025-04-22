USE [RepPortal];
GO
EXECUTE [sp_rename]
	@objname  = N'[dbo].[PK__ReportUs__3214EC07B6B3163F]',
	@newname  = N'tmp_0a4d619684ab47059e5815a3bf80e0bb',
	@objtype  = 'OBJECT'
GO
EXECUTE [sp_rename]
	@objname  = N'[dbo].[ReportUsageHistory]',
	@newname  = N'tmp_0905dc0878bd4bdba1711a9cf3367df8',
	@objtype  = 'OBJECT'
GO
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE [dbo].[ReportUsageHistory] (
	[Id] int IDENTITY(1, 1),
	[RepCode] nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReportName] nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RunTime] datetime2(7) NULL,
	[Parameters] nvarchar(max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AdminUser] nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT [PK__ReportUs__3214EC07B6B3163F] PRIMARY KEY([Id]) WITH (FILLFACTOR=100,
		DATA_COMPRESSION = NONE) ON [PRIMARY]
)
SET IDENTITY_INSERT [dbo].[ReportUsageHistory] ON
GO
INSERT INTO [dbo].[ReportUsageHistory] (
	[Id],
	[RepCode],
	[ReportName],
	[RunTime],
	[Parameters],
	[AdminUser])
SELECT
	[Id],
	[RepCode],
	[ReportName],
	[RunTime],
	[Parameters],
	[AdminUser]
FROM [dbo].[tmp_0905dc0878bd4bdba1711a9cf3367df8]
GO
SET IDENTITY_INSERT [dbo].[ReportUsageHistory] OFF
GO
DROP TABLE [dbo].[tmp_0905dc0878bd4bdba1711a9cf3367df8]
GO


USE [RepPortal];
GO
EXECUTE [sp_rename]
	@objname  = N'[dbo].[PK__RepLogin__3214EC07CB556861]',
	@newname  = N'tmp_c49e6c1921d04b578791651a31f378a7',
	@objtype  = 'OBJECT'
GO
EXECUTE [sp_rename]
	@objname  = N'[dbo].[RepLoginHistory]',
	@newname  = N'tmp_8fadeb628f644684a5de922c63a13e76',
	@objtype  = 'OBJECT'
GO
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
CREATE TABLE [dbo].[RepLoginHistory] (
	[Id] int IDENTITY(1, 1),
	[RepCode] nvarchar(100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[LoginTime] datetime2(7) NULL,
	[IPAddress] nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserAgent] nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT [PK__RepLogin__3214EC07CB556861] PRIMARY KEY([Id]) WITH (FILLFACTOR=100,
		DATA_COMPRESSION = NONE) ON [PRIMARY]
)
SET IDENTITY_INSERT [dbo].[RepLoginHistory] ON
GO
INSERT INTO [dbo].[RepLoginHistory] (
	[Id],
	[RepCode],
	[LoginTime],
	[IPAddress],
	[UserAgent])
SELECT
	[Id],
	[RepCode],
	[LoginTime],
	[IPAddress],
	[UserAgent]
FROM [dbo].[tmp_8fadeb628f644684a5de922c63a13e76]
GO
SET IDENTITY_INSERT [dbo].[RepLoginHistory] OFF
GO
DROP TABLE [dbo].[tmp_8fadeb628f644684a5de922c63a13e76]
GO



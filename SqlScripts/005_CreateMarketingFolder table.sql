/****** Object: Table [dbo].[FormsFolders]   Script Date: 4/22/2025 12:10:05 PM ******/
USE [RepPortal];
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE TABLE [dbo].[MarketingFolders] (
[Id] int IDENTITY(1, 1) NOT NULL,
[DisplayName] nvarchar(100) NOT NULL,
[FolderRelativePath] nvarchar(200) NOT NULL,
[DisplayOrder] int NOT NULL)
ON [PRIMARY]
WITH (DATA_COMPRESSION = NONE);
GO
ALTER TABLE [dbo].[MarketingFolders] SET (LOCK_ESCALATION = TABLE);
GO



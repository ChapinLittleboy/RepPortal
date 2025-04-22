-- ============================================ 
-- CREATE TABLE [CreditHoldReasonCodeExclusions]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'CreditHoldReasonCodeExclusions' AND xtype = 'U')
CREATE TABLE [CreditHoldReasonCodeExclusions] (
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(255) NOT NULL
);

-- ============================================ 
-- CREATE TABLE [CustItemSalesByMonth]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'CustItemSalesByMonth' AND xtype = 'U')
CREATE TABLE [CustItemSalesByMonth] (
    [Cust_num] nvarchar(50) NULL,
    [CustNum] nvarchar(50) NULL,
    [CustName] nvarchar(100) NULL,
    [Item] nvarchar(100) NULL,
    [InvoiceMonth] int NULL,
    [InvoiceFYMonth] int NULL,
    [FiscalYear] int NULL,
    [InvoicedAmt] decimal(18,2) NULL,
    [slsman] nvarchar(50) NULL
);

-- ============================================ 
-- CREATE TABLE [CustSalesByMonth]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'CustSalesByMonth' AND xtype = 'U')
CREATE TABLE [CustSalesByMonth] (
    [Cust_num] nvarchar(50) NULL,
    [CustNum] nvarchar(50) NULL,
    [CustName] nvarchar(100) NULL,
    [InvoiceMonth] int NULL,
    [InvoiceFYMonth] int NULL,
    [FiscalYear] int NULL,
    [InvoicedAmt] decimal(18,2) NULL,
    [slsman] nvarchar(50) NULL
);

-- ============================================ 
-- CREATE TABLE [FormsFolders]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'FormsFolders' AND xtype = 'U')
CREATE TABLE [FormsFolders] (
    [Id] int NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [FolderRelativePath] nvarchar(200) NOT NULL,
    [DisplayOrder] int NOT NULL
);

-- ============================================ 
-- CREATE TABLE [NewUserNotifications]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'NewUserNotifications' AND xtype = 'U')
CREATE TABLE [NewUserNotifications] (
    [Id] int NOT NULL,
    [UserID] nvarchar(256) NULL,
    [UserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [RepCode] nvarchar(100) NULL,
    [Notified] bit NULL,
    [CreatedAt] datetime NULL
);

-- ============================================ 
-- CREATE TABLE [PriceBookFolders]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'PriceBookFolders' AND xtype = 'U')
CREATE TABLE [PriceBookFolders] (
    [Id] int NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [FolderRelativePath] nvarchar(200) NOT NULL,
    [DisplayOrder] int NOT NULL
);

-- ============================================ 
-- CREATE TABLE [RepLoginHistory]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'RepLoginHistory' AND xtype = 'U')
CREATE TABLE [RepLoginHistory] (
    [Id] int NOT NULL,
    [RepCode] nvarchar(100) NULL,
    [LoginTime] datetime NULL,
    [IPAddress] nvarchar(50) NULL,
    [UserAgent] nvarchar(250) NULL
);

-- ============================================ 
-- CREATE TABLE [ReportUsageHistory]
-- ============================================ 
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'ReportUsageHistory' AND xtype = 'U')
CREATE TABLE [ReportUsageHistory] (
    [Id] int NOT NULL,
    [RepCode] nvarchar(100) NULL,
    [ReportName] nvarchar(100) NULL,
    [RunTime] datetime NULL,
    [Parameters] nvarchar(MAX) NULL,
    [AdminUser] nvarchar(100) NULL
);

USE [RepPortal];
GO

IF OBJECT_ID(N'[dbo].[MonthlyItemSalesPivotCache]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MonthlyItemSalesPivotCache]
    (
        [Id] bigint IDENTITY(1, 1) NOT NULL,
        [SiteRef] nvarchar(8) NULL,
        [RepCode] nvarchar(50) NOT NULL,
        [SalesRegion] nvarchar(50) NULL,
        [RegionName] nvarchar(100) NULL,
        [CustNum] nvarchar(50) NOT NULL,
        [CustSeq] int NOT NULL,
        [CustomerName] nvarchar(200) NULL,
        [ShipToName] nvarchar(200) NULL,
        [ShipToCity] nvarchar(100) NULL,
        [ShipToState] nvarchar(50) NULL,
        [ShipToZip] nvarchar(50) NULL,
        [ItemNum] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NULL,
        [FamilyCode] nvarchar(50) NULL,
        [FamilyCodeDescription] nvarchar(200) NULL,
        [InvoiceMonth] date NOT NULL,
        [FiscalYear] int NOT NULL,
        [QuarterOfFiscalYear] int NOT NULL,
        [MonthOfFiscalYear] int NOT NULL,
        [FiscalMonthShort] nvarchar(10) NOT NULL,
        [CalendarYear] int NOT NULL,
        [CalendarQuarter] int NOT NULL,
        [CalendarMonth] int NOT NULL,
        [CalendarMonthShort] nvarchar(10) NOT NULL,
        [Quantity] decimal(18, 4) NOT NULL,
        [SalesAmount] decimal(19, 4) NOT NULL,
        [RefreshedAt] datetime2(0) NOT NULL
            CONSTRAINT [DF_MonthlyItemSalesPivotCache_RefreshedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_MonthlyItemSalesPivotCache] PRIMARY KEY CLUSTERED ([Id])
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MonthlyItemSalesPivotCache_RepCode_InvoiceMonth'
      AND [object_id] = OBJECT_ID(N'[dbo].[MonthlyItemSalesPivotCache]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MonthlyItemSalesPivotCache_RepCode_InvoiceMonth]
    ON [dbo].[MonthlyItemSalesPivotCache] ([RepCode], [InvoiceMonth])
    INCLUDE
    (
        [SalesRegion],
        [RegionName],
        [CustNum],
        [CustSeq],
        [CustomerName],
        [ShipToName],
        [ShipToCity],
        [ShipToState],
        [ShipToZip],
        [ItemNum],
        [ProductName],
        [FamilyCode],
        [FamilyCodeDescription],
        [FiscalYear],
        [QuarterOfFiscalYear],
        [MonthOfFiscalYear],
        [FiscalMonthShort],
        [CalendarYear],
        [CalendarQuarter],
        [CalendarMonth],
        [CalendarMonthShort],
        [Quantity],
        [SalesAmount]
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MonthlyItemSalesPivotCache_RepCode_Region_Month'
      AND [object_id] = OBJECT_ID(N'[dbo].[MonthlyItemSalesPivotCache]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MonthlyItemSalesPivotCache_RepCode_Region_Month]
    ON [dbo].[MonthlyItemSalesPivotCache] ([RepCode], [SalesRegion], [InvoiceMonth])
    INCLUDE ([CustNum], [CustSeq], [ItemNum], [Quantity], [SalesAmount]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MonthlyItemSalesPivotCache_RepCode_State_Month'
      AND [object_id] = OBJECT_ID(N'[dbo].[MonthlyItemSalesPivotCache]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MonthlyItemSalesPivotCache_RepCode_State_Month]
    ON [dbo].[MonthlyItemSalesPivotCache] ([RepCode], [ShipToState], [InvoiceMonth])
    INCLUDE ([CustNum], [CustSeq], [ItemNum], [Quantity], [SalesAmount]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MonthlyItemSalesPivotCache_RepCode_Family_Month'
      AND [object_id] = OBJECT_ID(N'[dbo].[MonthlyItemSalesPivotCache]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MonthlyItemSalesPivotCache_RepCode_Family_Month]
    ON [dbo].[MonthlyItemSalesPivotCache] ([RepCode], [FamilyCode], [InvoiceMonth])
    INCLUDE ([CustNum], [CustSeq], [ItemNum], [Quantity], [SalesAmount]);
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[RefreshMonthlyItemSalesPivotCache]
    @HistoryStartDate date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RefreshStartedAt datetime2(0) = SYSUTCDATETIME();

    IF @HistoryStartDate IS NULL
    BEGIN
        DECLARE @Today date = CONVERT(date, GETDATE());
        DECLARE @FiscalYear int = CASE WHEN MONTH(@Today) >= 9 THEN YEAR(@Today) + 1 ELSE YEAR(@Today) END;

        SET @HistoryStartDate = DATEFROMPARTS(@FiscalYear - 4, 9, 1);
    END;

    IF OBJECT_ID(N'tempdb..#MonthlyItemSalesPivotCacheRefresh', N'U') IS NOT NULL
    BEGIN
        DROP TABLE #MonthlyItemSalesPivotCacheRefresh;
    END;

    CREATE TABLE #MonthlyItemSalesPivotCacheRefresh
    (
        [SiteRef] nvarchar(8) NULL,
        [RepCode] nvarchar(50) NOT NULL,
        [SalesRegion] nvarchar(50) NULL,
        [RegionName] nvarchar(100) NULL,
        [CustNum] nvarchar(50) NOT NULL,
        [CustSeq] int NOT NULL,
        [CustomerName] nvarchar(200) NULL,
        [ShipToName] nvarchar(200) NULL,
        [ShipToCity] nvarchar(100) NULL,
        [ShipToState] nvarchar(50) NULL,
        [ShipToZip] nvarchar(50) NULL,
        [ItemNum] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NULL,
        [FamilyCode] nvarchar(50) NULL,
        [FamilyCodeDescription] nvarchar(200) NULL,
        [InvoiceMonth] date NOT NULL,
        [FiscalYear] int NOT NULL,
        [QuarterOfFiscalYear] int NOT NULL,
        [MonthOfFiscalYear] int NOT NULL,
        [FiscalMonthShort] nvarchar(10) NOT NULL,
        [CalendarYear] int NOT NULL,
        [CalendarQuarter] int NOT NULL,
        [CalendarMonth] int NOT NULL,
        [CalendarMonthShort] nvarchar(10) NOT NULL,
        [Quantity] decimal(18, 4) NOT NULL,
        [SalesAmount] decimal(19, 4) NOT NULL,
        [RefreshedAt] datetime2(0) NOT NULL
    );

    ;WITH FilteredInvoiceLines AS
    (
        SELECT
            ih.site_ref AS SiteRef,
            cu.slsman AS RepCode,
            cu.Uf_SalesRegion AS SalesRegion,
            ih.cust_num AS CustNum,
            ih.cust_seq AS CustSeq,
            ii.item AS ItemNum,
            DATEFROMPARTS(YEAR(ih.inv_date), MONTH(ih.inv_date), 1) AS InvoiceMonth,
            SUM(ii.qty_invoiced) AS Quantity,
            SUM(ISNULL(ii.qty_invoiced * ii.price, 0)) AS SalesAmount
        FROM Bat_App.dbo.inv_hdr_mst_all ih
        JOIN Bat_App.dbo.inv_item_mst_all ii
            ON ih.inv_num = ii.inv_num
           AND ih.inv_seq = ii.inv_seq
           AND ISNULL(ih.site_ref, N'') = ISNULL(ii.site_ref, N'')
        JOIN Bat_App.dbo.customer_mst cu
            ON ih.cust_num = cu.cust_num
           AND ih.cust_seq = cu.cust_seq
        WHERE ih.inv_date >= @HistoryStartDate
          AND cu.slsman IS NOT NULL
          AND LTRIM(RTRIM(cu.slsman)) <> N''
        GROUP BY
            ih.site_ref,
            cu.slsman,
            cu.Uf_SalesRegion,
            ih.cust_num,
            ih.cust_seq,
            ii.item,
            DATEFROMPARTS(YEAR(ih.inv_date), MONTH(ih.inv_date), 1)
    ),
    FiscalMonths AS
    (
        SELECT
            DATEFROMPARTS(YEAR([Date]), MONTH([Date]), 1) AS InvoiceMonth,
            MAX(MonthShort) AS MonthShort,
            MAX(FiscalYear) AS FiscalYear,
            MAX(QuarterOfFiscalYear) AS QuarterOfFiscalYear,
            MAX(MonthOfFiscalYear) AS MonthOfFiscalYear
        FROM tempwork.dbo.FiscalCalendarVw
        GROUP BY DATEFROMPARTS(YEAR([Date]), MONTH([Date]), 1)
    )
    INSERT INTO #MonthlyItemSalesPivotCacheRefresh
    (
        [SiteRef],
        [RepCode],
        [SalesRegion],
        [RegionName],
        [CustNum],
        [CustSeq],
        [CustomerName],
        [ShipToName],
        [ShipToCity],
        [ShipToState],
        [ShipToZip],
        [ItemNum],
        [ProductName],
        [FamilyCode],
        [FamilyCodeDescription],
        [InvoiceMonth],
        [FiscalYear],
        [QuarterOfFiscalYear],
        [MonthOfFiscalYear],
        [FiscalMonthShort],
        [CalendarYear],
        [CalendarQuarter],
        [CalendarMonth],
        [CalendarMonthShort],
        [Quantity],
        [SalesAmount],
        [RefreshedAt]
    )
    SELECT
        fil.SiteRef,
        fil.RepCode,
        fil.SalesRegion,
        rn.RegionName,
        fil.CustNum,
        fil.CustSeq,
        ca0.name AS CustomerName,
        ca.name AS ShipToName,
        ca.City AS ShipToCity,
        ca.State AS ShipToState,
        ca.Zip AS ShipToZip,
        fil.ItemNum,
        im.description AS ProductName,
        im.family_code AS FamilyCode,
        fc.description AS FamilyCodeDescription,
        fil.InvoiceMonth,
        fm.FiscalYear,
        fm.QuarterOfFiscalYear,
        fm.MonthOfFiscalYear,
        fm.MonthShort AS FiscalMonthShort,
        YEAR(fil.InvoiceMonth) AS CalendarYear,
        DATEPART(QUARTER, fil.InvoiceMonth) AS CalendarQuarter,
        MONTH(fil.InvoiceMonth) AS CalendarMonth,
        LEFT(DATENAME(MONTH, fil.InvoiceMonth), 3) AS CalendarMonthShort,
        fil.Quantity,
        fil.SalesAmount,
        @RefreshStartedAt
    FROM FilteredInvoiceLines fil
    JOIN Bat_App.dbo.custaddr_mst ca
        ON fil.CustNum = ca.cust_num
       AND fil.CustSeq = ca.cust_seq
    JOIN Bat_App.dbo.custaddr_mst ca0
        ON fil.CustNum = ca0.cust_num
       AND ca0.cust_seq = 0
    JOIN Bat_App.dbo.item_mst im
        ON fil.ItemNum = im.item
    JOIN FiscalMonths fm
        ON fil.InvoiceMonth = fm.InvoiceMonth
    LEFT JOIN Bat_App.dbo.famcode_mst fc
        ON im.family_code = fc.family_code
    LEFT JOIN Bat_App.dbo.Chap_RegionNames rn WITH (NOLOCK)
        ON rn.Region = fil.SalesRegion;

    BEGIN TRANSACTION;

        TRUNCATE TABLE [dbo].[MonthlyItemSalesPivotCache];

        INSERT INTO [dbo].[MonthlyItemSalesPivotCache]
        (
            [SiteRef],
            [RepCode],
            [SalesRegion],
            [RegionName],
            [CustNum],
            [CustSeq],
            [CustomerName],
            [ShipToName],
            [ShipToCity],
            [ShipToState],
            [ShipToZip],
            [ItemNum],
            [ProductName],
            [FamilyCode],
            [FamilyCodeDescription],
            [InvoiceMonth],
            [FiscalYear],
            [QuarterOfFiscalYear],
            [MonthOfFiscalYear],
            [FiscalMonthShort],
            [CalendarYear],
            [CalendarQuarter],
            [CalendarMonth],
            [CalendarMonthShort],
            [Quantity],
            [SalesAmount],
            [RefreshedAt]
        )
        SELECT
            [SiteRef],
            [RepCode],
            [SalesRegion],
            [RegionName],
            [CustNum],
            [CustSeq],
            [CustomerName],
            [ShipToName],
            [ShipToCity],
            [ShipToState],
            [ShipToZip],
            [ItemNum],
            [ProductName],
            [FamilyCode],
            [FamilyCodeDescription],
            [InvoiceMonth],
            [FiscalYear],
            [QuarterOfFiscalYear],
            [MonthOfFiscalYear],
            [FiscalMonthShort],
            [CalendarYear],
            [CalendarQuarter],
            [CalendarMonth],
            [CalendarMonthShort],
            [Quantity],
            [SalesAmount],
            [RefreshedAt]
        FROM #MonthlyItemSalesPivotCacheRefresh;

    COMMIT TRANSACTION;

    SELECT
        COUNT_BIG(*) AS RowsRefreshed,
        MIN(InvoiceMonth) AS FirstInvoiceMonth,
        MAX(InvoiceMonth) AS LastInvoiceMonth,
        @RefreshStartedAt AS RefreshedAt
    FROM [dbo].[MonthlyItemSalesPivotCache];
END;
GO

-- First bounded test:
-- EXEC [dbo].[RefreshMonthlyItemSalesPivotCache] @HistoryStartDate = '2024-09-01';
--
-- Normal nightly refresh, using the default four-fiscal-year window:
-- EXEC [dbo].[RefreshMonthlyItemSalesPivotCache];
GO

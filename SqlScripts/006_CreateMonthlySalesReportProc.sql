/****** Object: Procedure [dbo].[Chap_RepMonthlySalesReportSp]   Script Date: 6/11/2025 9:26:48 AM ******/
USE [BAT_App];
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE   PROCEDURE [dbo].[Chap_RepMonthlySalesReportSp]
    @rep              NVARCHAR(3)        = 'LAW',
    @salesRegion      NVARCHAR(MAX)      = NULL,
    @usePriorMonthEnd BIT                = 0   -- 0 = include full FY (default); 1 = stop at prior-month end
AS
BEGIN
    SET NOCOUNT ON;

    /* ?????????????  Calendar helpers ????????????? */
    DECLARE @today DATE = GETDATE();

    -- Determine the fiscal year that started last 1-Sep and ends next 31-Aug
    DECLARE @fiscalYear INT =
        CASE WHEN MONTH(@today) >= 9 THEN YEAR(@today) + 1 ELSE YEAR(@today) END;

    /* ?????????????  Fiscal-year boundaries ????????????? */
    DECLARE @fyCurrentStart DATE = DATEFROMPARTS(@fiscalYear - 1, 9, 1);  -- 1-Sep of prior calendar year
    DECLARE @fyCurrentEnd   DATE = DATEFROMPARTS(@fiscalYear    , 8, 31); -- 31-Aug of current FY

    -- Optional override: last day of the previous calendar month
    IF @usePriorMonthEnd = 1
        SET @fyCurrentEnd = EOMONTH(DATEADD(MONTH, -1, @today));

    /*  The prior fiscal-year range always mirrors the length of the current FY */
    DECLARE @fyPriorStart DATE = DATEADD(YEAR, -1, @fyCurrentStart);
    DECLARE @fyPriorEnd   DATE = DATEADD(YEAR, -1, @fyCurrentEnd);

    /* ?????????????  Dynamic-SQL prep  ????????????? */
    DECLARE @i INT = 0;
    DECLARE
        @monthName      NVARCHAR(20),
        @colsPivot      NVARCHAR(MAX) = N'',
        @currentFYCols  NVARCHAR(MAX) = N'',
        @fyPriorSum     NVARCHAR(MAX) = N'',
        @fyCurrentSum   NVARCHAR(MAX) = N'';

    WHILE @i < 24
    BEGIN
        DECLARE @d DATE = DATEADD(MONTH, @i, @fyPriorStart);
        SET @monthName = FORMAT(@d, 'MMM') + CAST(YEAR(@d) AS VARCHAR(4));

        SET @colsPivot += QUOTENAME(@monthName) + N',';

        IF @i < 12
            SET @fyPriorSum += 'ISNULL(' + QUOTENAME(@monthName) + ',0)+';          -- prior FY
        ELSE
        BEGIN
            DECLARE @alias NVARCHAR(50) =
                'CurrentFiscalMonth' + CAST(@i - 11 AS VARCHAR(2)) + 'Sales';

            SET @fyCurrentSum  += 'ISNULL(' + QUOTENAME(@monthName) + ',0)+';
            SET @currentFYCols += 'ISNULL(' + QUOTENAME(@monthName) + ',0) AS '
                                + QUOTENAME(@alias) + N',';
        END
        SET @i += 1;
    END

    -- Trim trailing commas / plus signs
    SELECT
        @colsPivot     = LEFT(@colsPivot    , LEN(@colsPivot)     - 1),
        @fyPriorSum    = LEFT(@fyPriorSum   , LEN(@fyPriorSum)    - 1),
        @fyCurrentSum  = LEFT(@fyCurrentSum , LEN(@fyCurrentSum)  - 1),
        @currentFYCols = LEFT(@currentFYCols, LEN(@currentFYCols) - 1);

    /* ?????????????  Optional region filter  ????????????? */
    DECLARE @regionFilter NVARCHAR(MAX) = N'';
    IF @salesRegion IS NOT NULL AND LTRIM(RTRIM(@salesRegion)) <> ''
        SET @regionFilter =
            N' AND cu.Uf_SalesRegion IN (SELECT value FROM STRING_SPLIT(@salesRegion, '',''))';

    /* ?????????????  Build dynamic SQL  ????????????? */
    DECLARE @sql NVARCHAR(MAX) = N'
    SELECT
        Customer,
        [Customer Name],
        [Ship To Num],
        [Ship To City],
        [Ship To State],
        Uf_SalesRegion,
        RegionName,
        ' + @fyPriorSum + N'   AS LastFiscalYearSales,
        ' + @fyCurrentSum + N' AS CurrentFiscalYearSales,
        ' + @currentFYCols     + N'
    FROM (
        /* ---------- Bat_App ---------- */
        SELECT ih.cust_num AS Customer,
               ca0.Name    AS [Customer Name],
               ih.cust_seq AS [Ship To Num],
               ca.City     AS [Ship To City],
               ca.State    AS [Ship To State],
               cu.Uf_SalesRegion,
               rn.RegionName,
               FORMAT(ih.inv_date, ''MMM'') + CAST(YEAR(ih.inv_date) AS VARCHAR) AS Period,
               SUM(ii.qty_invoiced * ii.price) AS ExtPrice
        FROM Bat_App.dbo.inv_item_mst  ii
        JOIN Bat_App.dbo.inv_hdr_mst   ih ON ii.inv_num = ih.inv_num AND ii.inv_seq = ih.inv_seq
        JOIN Bat_App.dbo.custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
        JOIN Bat_App.dbo.custaddr_mst ca  ON ih.cust_num = ca.cust_num  AND ih.cust_seq = ca.cust_seq
        JOIN Bat_App.dbo.customer_mst cu  ON ih.cust_num = cu.cust_num  AND cu.cust_seq = ih.cust_seq
        LEFT JOIN Bat_App.dbo.Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
        WHERE ih.inv_date >= dbo.MidnightOf(@fyPriorStart) AND ih.inv_date <= dbo.dayEndOf(@fyCurrentEnd)
          AND cu.slsman = @rep' + @regionFilter + N'
        GROUP BY ih.cust_num, ca0.Name, ih.cust_seq, ca.City, ca.State,
                 cu.Uf_SalesRegion, rn.RegionName,
                 FORMAT(ih.inv_date, ''MMM'') + CAST(YEAR(ih.inv_date) AS VARCHAR)

        UNION ALL

        /* ---------- Kent_App ---------- */
        SELECT ih.cust_num AS Customer,
               ca0.Name    AS [Customer Name],
               ih.cust_seq AS [Ship To Num],
               ca.City     AS [Ship To City],
               ca.State    AS [Ship To State],
               cu.Uf_SalesRegion,
               rn.RegionName,
               FORMAT(ih.inv_date, ''MMM'') + CAST(YEAR(ih.inv_date) AS VARCHAR) AS Period,
               SUM(ii.qty_invoiced * ii.price) AS ExtPrice
        FROM Kent_App.dbo.inv_item_mst  ii
        JOIN Kent_App.dbo.inv_hdr_mst   ih ON ii.inv_num = ih.inv_num AND ii.inv_seq = ih.inv_seq
        JOIN Bat_App.dbo.custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
        JOIN Bat_App.dbo.custaddr_mst ca  ON ih.cust_num = ca.cust_num  AND ih.cust_seq = ca.cust_seq
        JOIN Bat_App.dbo.customer_mst cu  ON ih.cust_num = cu.cust_num  AND cu.cust_seq = ih.cust_seq
        LEFT JOIN Bat_App.dbo.Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
        WHERE ih.inv_date >= dbo.MidnightOf(@fyPriorStart) AND ih.inv_date <= dbo.dayEndOf(@fyCurrentEnd)
          AND cu.slsman = @rep' + @regionFilter + N'
        GROUP BY ih.cust_num, ca0.Name, ih.cust_seq, ca.City, ca.State,
                 cu.Uf_SalesRegion, rn.RegionName,
                 FORMAT(ih.inv_date, ''MMM'') + CAST(YEAR(ih.inv_date) AS VARCHAR)
    ) AS src
    PIVOT (
        SUM(ExtPrice)
        FOR Period IN (' + @colsPivot + N')
    ) AS p
    ORDER BY LastFiscalYearSales DESC;';

    /* ?????????????  Execute  ????????????? */
    EXEC sp_executesql
        @sql,
        N'@rep NVARCHAR(3), @salesRegion NVARCHAR(MAX), @fyPriorStart DATE, @fyCurrentEnd DATE',
        @rep           = @rep,
        @salesRegion   = @salesRegion,
        @fyPriorStart  = @fyPriorStart,
        @fyCurrentEnd  = @fyCurrentEnd;
END
GO


/****** Object: Procedure [dbo].[sp_GetInvoices]   Script Date: 4/2/2026 1:26:23 PM ******/
USE [RepPortal];
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE PROCEDURE [dbo].[sp_GetInvoices]
    @BeginInvoiceDate DATE,
    @EndInvoiceDate DATE,
    @RepCode VARCHAR(50),
    @CustNum VARCHAR(50) = NULL,
    @CorpNum VARCHAR(50) = NULL,
    @CustType VARCHAR(50) = NULL,
    @EndUserType VARCHAR(50) = NULL,
    @AllowedRegions VARCHAR(100) = NULL
    /*
    EXEC RepPortal.dbo.sp_GetInvoices
    @BeginInvoiceDate = '2025-04-01',
    @EndInvoiceDate = '2025-04-30',
    @RepCode = 'LAW',
    @CustNum = NULL, -- Optional
    @CorpNum = NULL, -- Optional
    @CustType = NULL, -- Optional
    @EndUserType = NULL, -- Optional
    @AllowedRegions = 'LMW';  --Optional
    */
AS
BEGIN

if @CustNum = '' set @CustNum = NULL;
if @CustType = '' set @CustType = NULL;
SET @CustNum = bat_app.dbo.ExpandKyByType('CustNumType', @CustNum)

if @RepCode = 'LAW' and len(isnull(@AllowedRegions,'')) > 12
    set @AllowedRegions = null
    
-- Base query template to reuse across databases
;WITH CombinedInvoices AS (
SELECT
 cu.Slsman
,ih.Cust_num as Cust
,ih.Cust_Seq as  CustSeq
,ca0.Name as B2Name
,ca.[Name]
,ca.State
,ii.site_ref as site
,co.co_num as CoNum
,ih.cust_po as CustPO
,ii.Item
,ii.qty_invoiced as InvQty
,ci.qty_ordered  as OrdQty
,ii.Price
,ci.Due_Date as DueDate
,co.order_date   as OrdDate
,ih.inv_date as    InvDate
,ih.inv_num    as InvNum
,ii.qty_invoiced * (ii.price * ((100 - isnull(ih.disc, 0.0))/100)) AS ExtPrice
,ih.Ship_Date, cu.Uf_SalesRegion as ShipToRegion
    FROM
        Bat_App.dbo.inv_hdr_mst ih
        JOIN Bat_App.dbo.inv_item_mst ii ON ih.inv_num = ii.inv_num AND ih.inv_seq = ii.inv_seq
        LEFT JOIN Bat_App.dbo.co_mst co ON ih.co_num = co.co_num
        LEFT JOIN Bat_App.dbo.coitem_mst ci ON ii.co_num = ci.co_num AND ci.co_release = ii.co_release AND ii.item = ci.item and ii.co_line = ci.co_line
        JOIN Bat_App.dbo.custaddr_mst ca ON ca.cust_num = ih.cust_num AND ca.cust_seq = ih.cust_seq
        JOIN Bat_App.dbo.customer_mst cu ON ca.cust_num = cu.cust_num AND ca.cust_seq = cu.cust_seq
        JOIN Bat_App.dbo.custaddr_mst ca0 ON ca0.cust_num = ih.cust_num AND ca0.cust_seq = 0
    WHERE
        ih.inv_date BETWEEN @BeginInvoiceDate AND @EndInvoiceDate
        AND (
        -- Admin gets everything
        @RepCode = 'Admin'

        -- DAL gets their normal customers + special customer list
        OR (
                @RepCode = 'DAL'
                AND (
                        ih.slsman = @RepCode
                        
                   )
           )

        -- All other reps get only customers matching their rep code
        OR (
                @RepCode NOT IN ('Admin', 'DAL')
                AND ih.slsman = @RepCode
           )
    )-- NOTE: Using IH slsman not CO or Customer assigned
        AND (@CustNum IS NULL OR ih.cust_num = @CustNum)
        AND (@CustType IS NULL OR cu.cust_type = @CustType)
        AND (@AllowedRegions IS NULL or cu.Uf_SalesRegion in (SELECT Value FROM bat_App.dbo.FnSplit(@AllowedRegions,',')))

    UNION ALL

SELECT
 cu.Slsman
,ih.Cust_num as Cust
,ih.Cust_Seq as  CustSeq
,ca0.Name as B2Name
,ca.[Name]
,ca.State
,ii.site_ref as site
,co.co_num as CoNum
,ih.cust_po as CustPO
,ii.Item
,ii.qty_invoiced as InvQty
,ci.qty_ordered  as OrdQty
,ii.Price
,ci.Due_Date as DueDate
,co.order_date   as OrdDate
,ih.inv_date as    InvDate
,ih.inv_num    as InvNum
,ii.qty_invoiced * (ii.price * ((100 - isnull(ih.disc, 0.0))/100)) AS ExtPrice
,ih.Ship_Date, cu.Uf_SalesRegion as ShipToRegion
    FROM
        Kent_App.dbo.inv_hdr_mst ih
        JOIN Kent_App.dbo.inv_item_mst ii ON ih.inv_num = ii.inv_num AND ih.inv_seq = ii.inv_seq
        LEFT JOIN Kent_App.dbo.co_mst co ON ih.co_num = co.co_num
        LEFT JOIN Kent_App.dbo.coitem_mst ci ON ii.co_num = ci.co_num AND ci.co_release = ii.co_release AND ii.item = ci.item  and ii.co_line = ci.co_line
        JOIN bat_App.dbo.custaddr_mst ca ON ca.cust_num = ih.cust_num AND ca.cust_seq = ih.cust_seq
        JOIN bat_App.dbo.customer_mst cu ON ca.cust_num = cu.cust_num AND ca.cust_seq = cu.cust_seq
        JOIN bat_App.dbo.custaddr_mst ca0 ON ca0.cust_num = ih.cust_num AND ca0.cust_seq = 0
    WHERE
        ih.inv_date BETWEEN @BeginInvoiceDate AND @EndInvoiceDate
        AND ih.Slsman = @RepCode -- NOTE: Using IH slsman not CO or Customer assigned
        AND (@CustNum IS NULL OR ih.cust_num = @CustNum)
        AND (@CustType IS NULL OR cu.cust_type = @CustType)
        AND (@AllowedRegions IS NULL or cu.Uf_SalesRegion in (SELECT Value FROM bat_app.dbo.FnSplit(@AllowedRegions,',')))
)

SELECT *
FROM CombinedInvoices
ORDER BY
    B2Name,
    CUST,
    CUSTSEQ

--GRANT EXECUTE ON [dbo].[sp_GetInvoices] TO ReportUser1	
END
GO


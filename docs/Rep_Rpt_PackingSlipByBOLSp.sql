/****** Object: Procedure [dbo].[Rep_Rpt_PackingSlipByBOLSp]   Script Date: 4/1/2026 2:02:10 PM ******/
USE [BAT_App];
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE PROCEDURE [dbo].Rep_Rpt_PackingSlipByBOLSp (


  @PrintBlankPickupDate            FlagNyType          = 1
, @IncludeSerialNumbers            ListYesNoType       = 0
, @PrintShipmentSequenceText       FlagNyType          = 0
, @DisplayShipmentSeqNotes         FlagNyType          = 0
, @DisplayShipmentPackageNotes     FlagNyType          = 0
, @MinShipNum                      NVARCHAR(30)		   = NULL --'475160' --AIT EWF 08/22/2019
, @MaxShipNum                      NVARCHAR(30)		   = NULL --'475160' --AIT EWF 08/22/2019
, @CustomerStarting                CustNumType         = NULL
, @CustomerEnding                  CustNumType         = NULL
, @ShiptoStarting                  CustSeqType         = NULL
, @ShiptoEnding                    CustSeqType         = NULL
, @PickupDateStarting              DateType            = NULL
, @PickupDateEnding                DateType            = NULL
, @DateStartingOffset              DateOffsetType      = NULL
, @DateEndingOffset                DateOffsetType      = NULL
, @ShowInternal                    FlagNyType          = 0
, @ShowExternal                    FlagNyType          = 0
, @DisplayHeader                   FlagNyType          = 0
, @UseProfile                      FlagNyType          = 0
, @LangCode                        LangCodeType        = NULL
, @pSite                           SiteType            = 'BAT'
, @PrintDescription                ListYesNoType       = 1
, @pPrintHeaderOnAllPages          ListYesNoType       = 0
, @pPageBetweenPackages            ListYesNoType       = 0
, @pPrintCertificateOfConformance  ListYesNoType       = 0
, @pPrintPackageWeight             ListYesNoType       = 0
, @pPrintDeliveryIncoTerms         ListYesNoType       = 0
, @pPrintEUDetails                 ListYesNoType       = 0
, @pPrintKitComponents             ListYesNoType       = 0
, @pPrintPrices                    ListYesNoType       = 0
, @UserID						   UserNameType		   = NULL --AIT EWF 08/13/19
, @SessioniD					   RowPointerType	   =NULL --AIT EWF 09/22/2020
) AS



EXEC dbo.SetSiteSp @pSite, NULL;
IF @MaxShipNum   is NULL SET @MaxShipNum = @MinShipNum
IF @ShowInternal IS NULL SET @ShowInternal = 0
IF @ShowExternal IS NULL SET @ShowExternal = 0
IF @DisplayHeader IS NULL SET @DisplayHeader = 0
IF @PrintDescription IS NULL SET @PrintDescription = 0
IF @pPrintHeaderOnAllPages IS NULL SET @pPrintHeaderOnAllPages = 0
IF @pPageBetweenPackages IS NULL SET @pPageBetweenPackages = 0
IF @pPrintCertificateOfConformance IS NULL SET @pPrintCertificateOfConformance = 0
IF @pPrintPackageWeight IS NULL SET @pPrintPackageWeight = 0
IF @pPrintDeliveryIncoTerms IS NULL SET @pPrintDeliveryIncoTerms = 0
IF @pPrintEUDetails IS NULL SET @pPrintEUDetails = 0
IF @pPrintKitComponents IS NULL SET @pPrintKitComponents = 0
IF @pPrintPrices IS NULL SET @pPrintPrices = 0
IF @PrintBlankPickupDate IS NULL SET @PrintBlankPickupDate = 0 --AIT EWF 06/23/2021

--<BEG AIT EWF 09/22/2020>
Declare @IsConsolidatedDoc TINYINT
--SET @IsConsolidatedDoc = ISNULL((Select MAX(Selected) FROM AIT_SS_TmpConsolidatedDoc WHERE SessionID = @SessionID AND Selected = 1),0)
SET @IsConsolidatedDoc = 0
--<END AIT EWF 09/22/2020>

DECLARE
  @RptSessionID RowPointerType

  EXEC dbo.InitSessionContextSp
  @ContextName = 'Rpt_ShipmentPackingSlipSp'
, @SessionID   = @RptSessionID OUTPUT
, @Site        = @pSite

DECLARE
  @Severity                          INT
, @Today                             DateType
, @IsThaiFlag                        INT
, @CertfOfConfrnText                 CertificateOfConformanceTextType
, @Job                               JobType
, @Suffix                            SuffixType
, @Counter                           INT
, @Infobar                           InfobarType
, @TaskId                            INT
, @CountryOfOrigin NVARCHAR(10) = ISNULL((SELECT TOP 1 Charfld1 FROM AIT_CustParms where ParmId='Shipping' AND ParmKey='ERPConfig'),'')

DECLARE
  @PackNum                           ShipmentIDType
, @PackDate                          CurrentDateType
, @Whse                              WhseType
, @CoNum                             EmpJobCoPoRmaProjPsTrnNumType  
, @CustNum                           CustNumType
, @Weight                            TotalWeightType
, @QtyPackages                       PackagesType
, @ShipCode                          ShipCodeType
, @Carrier                           CarrierCodeType
, @OfficeAddr                        AddressType
, @OfficeAddr2                       AddressType
, @OfficeAddr3                       AddressType
, @OfficeAddr4                       AddressType
, @OfficeCity                        CityType
, @OfficeState                       StateType
, @OfficeZip                         PostalCodeType
, @OfficeCountry                     CountyType
, @OfficeContact                     ContactType
, @OfficePhone                       PhoneType
, @ShipContact                       ContactType
, @ShipAddr                          AddressType
, @ShipAddr2                         AddressType
, @ShipAddr3                         AddressType
, @ShipAddr4                         AddressType
, @ShipCity                          CityType
, @ShipState                         StateType
, @ShipZip                           PostalCodeType
, @ShipCountry                       CountyType
, @Shipment_TH_FOBPoint              FOBType
, @Shipment_TH_ItemCategory          TH_ItemCategoryType
, @Shipment_TH_FromShippingPort      TH_ShippingPortType
, @Shipment_TH_ToShippingPort        TH_ShippingPortType
, @BillContact                       ContactType
, @BillAddr                          AddressType
, @BillAddr2                         AddressType
, @BillAddr3                         AddressType
, @BillAddr4                         AddressType
, @BillCity                          CityType
, @BillState                         StateType
, @BillZip                           PostalCodeType
, @BillCountry                       CountyType
, @LcrNum                            NVARCHAR(20)
, @CreditHold                        INT
, @CustPo                            CustPoType
, @CoLine                            INT
, @CoRelease                         INT
, @Item                              ItemType
, @ItemDesc                          DescriptionType
, @SerialNum                         SerNumType
, @UM                                UMType
, @ShipRowpointer                    RowPointerType
, @ShipPackageRowpointer             UNIQUEIDENTIFIER
, @ShipSeqRowpointer                 UNIQUEIDENTIFIER
, @QtyUnitFormat                     InputMaskType
, @PlacesQtyUnit                     TINYINT
, @ShipmentId                        ShipmentIDType
, @ShipmentLine                      ShipmentLineType
, @ShipmentSeq                       ShipmentSequenceType
, @QtyPicked                         QtyUnitNoNegType
, @QtyShipped                        QtyUnitNoNegType
, @PackageId                         PackageIDType
, @ShipmentNotes                     INT
, @ShipmentSeqNotes                  INT
, @ShipmentPackageNotes              INT
, @ShipmentPackage_TH_CartonPrefix   TH_CartonPrefixType
, @ShipmentPackage_TH_Measurement    TH_MeasurementType
, @ShipmentPackage_TH_CartonSize     TH_CartonSizeType
, @CustSeq                           CustSeqType
, @FromCompanyName                   NameType
, @ShipCompanyName                   NameType
, @BillCompanyName                   NameType
, @Lot                               LotType
, @Contact                           ContactType
, @OfficeLongAddr                    LongAddress
, @BillToLongAddr                    LongAddress
, @ShipToLongAddr                    LongAddress
, @EcCode                            EcCodeType
, @Origin                            EcCodeType
, @comm_code                          CommodityCodeType
, @Delterm                           DeltermType
, @DeltermDescription                DescriptionType
, @URL                               URLType
, @OfficeAddrFooter                  LongAddress
, @CertificateOfConformanceText      CertificateOfConformanceTextType
, @CompItem                          ItemType
, @CompUM                            UMType
, @CompItemDesc                      DescriptionType
, @CompTotalRequired                 QtyUnitType
, @Kit                               ListYesNoType
, @UomConvFactor                     UMConvFactorType
, @unit_price                        CostPrcType
, @ext_price                         CostPrcType
, @total_price						 CostPrcType
, @CorpCust							 CustNumType
, @CorpCustName						 NVARCHAR(120)
, @LotExpDate						 DATETIME --AIT EWF 07/30/19
, @QtyBO							 QtyTotlType --AIT EWF 07/30/19
, @QtyOrdered						 QtyTotlType --AIT EWF 07/30/19
, @ShipViaDescription				 NVARCHAR(100) --AIT EWF 07/30/19
, @CustItem							 NVARCHAR(100) --AIT EWF 07/30/19
, @hts_code							 NVARCHAR(100) --AIT EWF 07/30/19
, @sched_b_num						 NVARCHAR(100) --AIT EWF 07/30/19
, @eccn_usml_value					 NVARCHAR(100) --AIT EWF 07/30/19
, @commodity_jurisdiction			 NVARCHAR(100) --AIT EWF 07/30/19
, @export_compliance_program		 NVARCHAR(100) --AIT EWF 07/30/19
, @NaftPrefCrit						 NVARCHAR(100) --AIT EWF 07/30/19
--<BEG AIT EWF 11/05/2019>
, @TrackingNumber					 NVARCHAR(100)
, @PkgWeight						 DECIMAL(20,8)
, @PltLicense						 NVARCHAR(100)
--<END AIT EWF 11/05/2019>
, @end_user_type					EndUserTypeType --AIT EWF 11/14/2019
, @end_user_type_description		DescriptionType --AIT EWF 11/14/2019
, @taken_by							DescriptionType --AIT EWF 12/19/2019

DECLARE @ReportSet TABLE (
  pack_num                           ShipmentIDType
, pack_date                          CurrentDateType
, whse                               WhseType
, co_num                             EmpJobCoPoRmaProjPsTrnNumType  
, cust_num                           CustNumType
, weight                             TotalWeightType
, qty_packages                       PackagesType
, ship_code                          NVARCHAR(100) --AIT EWF 07/30/19
, carrier                            CarrierCodeType
, office_addr                        AddressType
, office_addr2                       AddressType
, office_addr3                       AddressType
, office_addr4                       AddressType
, office_city                        CityType
, office_state                       StateType
, office_zip                         PostalCodeType
, office_country                     CountyType
, office_contact                     ContactType
, office_phone                       PhoneType
, ship_contact                       ContactType
, ship_addr                          AddressType
, ship_addr2                         AddressType
, ship_addr3                         AddressType
, ship_addr4                         AddressType
, ship_city                          CityType
, ship_state                         StateType
, ship_zip                           PostalCodeType
, ship_country                       CountyType
, shipment_TH_fob_point              FOBType
, shipment_TH_item_category          TH_ItemCategoryType
, shipment_TH_from_shipping_port     TH_ShippingPortType
, shipment_TH_to_shipping_port       TH_ShippingPortType
, bill_contact                       ContactType
, bill_addr                          AddressType
, bill_addr2                         AddressType
, bill_addr3                         AddressType
, bill_addr4                         AddressType
, bill_city                          CityType
, bill_state                         StateType
, bill_zip                           PostalCodeType
, bill_country                       CountyType
, lcr_num                            NVARCHAR(20)
, credit_hold                        INT
, cust_po                            CustPoType
, co_line                            INT
, co_release                         INT
, item                               ItemType
, item_desc                          NVARCHAR(100) --AIT EWF 07/30/2019
, serial_num                         SerNumType
, u_m                                UMType
, ship_rowpointer                    RowPointerType
, ship_package_rowpointer            UNIQUEIDENTIFIER
, ship_seq_rowpointer                UNIQUEIDENTIFIER
, qty_unit_format                    InputMaskType
, places_qty_unit                    TINYINT
, shipment_id                        ShipmentIDType
, shipment_line                      ShipmentLineType
, shipment_seq                       ShipmentSequenceType
, qty_picked                         QtyUnitNoNegType
, qty_shipped                         QtyUnitNoNegType
, package_id                         PackageIDType
, shipment_notes                     INT
, shipmentSeq_notes                  INT
, shipmentPackage_notes              INT
, shipmentPackage_TH_carton_prefix   TH_CartonPrefixType
, shipmentPackage_TH_measurement     TH_MeasurementType
, shipmentPackage_TH_carton_size     TH_CartonSizeType
, cust_seq                           CustSeqType
, from_CompanyName                   NameType
, ship_CompanyName                   NameType
, bill_CompanyName                   NameType
, lot                                LotType
, contact                            ContactType
, OfficeLongAddr                     LongAddress
, BillToLongAddr                     LongAddress
, ShipToLongAddr                     LongAddress
, ec_code                            EcCodeType
, origin                             EcCodeType
, comm_code                          CommodityCodeType
, delterm                            DeltermType
, delterm_description                DescriptionType
, url                                URLType
, office_addr_footer                 LongAddress
, certificate_of_conformance_text    CertificateOfConformanceTextType
, Comp_Item                          ItemType
, Comp_U_M                           UMType
, Comp_ItemDesc                      DescriptionType
, Comp_TotalRequired                 QtyUnitType
, unit_price                         CostPrcType
, ext_price                          CostPrcType
, total_price                        CostPrcType
, CorpCust							 CustNumType
, CorpCustName						 NVARCHAR(120)
, CountryOfOrigin					 NVARCHAR(10)
, LotExpDate						 DateTime --AIT EWF 07/30/19
, QtyBO								 QtyTotlType --AIT EWF 07/30/19
, QtyOrdered						 QtyTotlType --AIT EWF 07/30/19
, ShipViaDescription				 NVARCHAR(100) --AIT EWF 07/30/19
, CustItem							 NVARCHAR(100) --AIT EWF 07/30/19
, hts_code							NVARCHAR(100) --AIT EWF 07/30/19
, sched_b_num						NVARCHAR(100) --AIT EWF 07/30/19
, eccn_usml_value					NVARCHAR(100) --AIT EWF 07/30/19
, commodity_jurisdiction			NVARCHAR(100) --AIT EWF 07/30/19
, export_compliance_program			NVARCHAR(100) --AIT EWF 07/30/19
--<BEG AIT EWF 11/05/2019>
, TrackingNumber					NVARCHAR(100)
, PkgWeight							Decimal(20,8)
, PltLicense						NVARCHAR(100)
--<END AIT EWF 11/05/2019>
, end_user_type						EndUserTypeType --AIT EWF 11/14/2019
, end_user_type_description			DescriptionType --AIT EWF 11/14/2019
, taken_by							DescriptionType --AIT EWF 12/19/2019
, NaftPrefCrit						NVARCHAR(10)
, INDEX IX NONCLUSTERED(shipment_id,co_num,co_line,co_release)
)
drop table if exists #PickList
CREATE TABLE #PickList (
  hdr_Job                            NVARCHAR(30)     -- @FormattedJob
, hdr_JobDate                        DATETIME         -- @JobJobDate
, hdr_JobStat                        NCHAR            -- @JobStat
, hdr_JobStatDesc                    NVARCHAR(30)     -- @JobStatDesc
, hdr_JobSchEndDate                  DATETIME         -- @JobSchEndDate
, hdr_ProdMix                        NVARCHAR(7)      -- @JobProdMix
, hdr_ProdMixDesc                    NVARCHAR(40)     -- @ProdmixDescription
, hdr_JobItem                        NVARCHAR(30)     -- @JobItem
, hdr_JobItemDesc                    NVARCHAR(40)     -- @JobDescription
, hdr_JobQtyReleased                 DECIMAL(19,8)    -- @JobQtyReleased
, hdr_JobRevision                    NVARCHAR(8)      -- @JobRevision
, hdr_JobWhse                        NVARCHAR(4)      -- @JobWhse
, sub_JobMatlOperNum                 INT              -- @lJobmatlOperNum
, sub_JobRouteWC                     NVARCHAR(6)      -- @JobrouteWc
, sub_WCDecription                   NVARCHAR(40)     -- @WCDescription
, sub_JrtSchStartDate                DATETIME         -- @JrtSchStartDate
, sub_JrtSchEndDate                  DATETIME         -- @JrtSchEndDate
, det_OperNum                        INT              -- @OperNum
, det_JobSequence                    NVARCHAR(30)     -- @lJobmatlSeq
, det_JobMatlItem                    NVARCHAR(30)     -- @lJobmatlItem
, det_JobMatlDesciption              NVARCHAR(40)     -- @DerItemDescription
, det_JobMatlU_M                     NVARCHAR(3)      -- @lJobmatlUM
, det_TotalRequired                  DECIMAL(19,8)    -- @QtuRequired
, det_JobMatlQtyIssued               DECIMAL(19,8)    -- @lJobmatlQtyIssued
, det_QtyAvailable                   DECIMAL(19,8)    -- @QtuRequired
, det_QtyToPick                      DECIMAL(19,8)    -- @QtuPickQty
, det_Location                       NVARCHAR(15)     -- @Loc
, det_LotDescription                 NVARCHAR(40)     -- @Lot
, det_JobPicked                      TINYINT          -- @JobPicked
, det_Reprint                        BIT              -- @Reprint Material
, det_QtyRequiredToPick              NVARCHAR(1)      -- @Asterisk
, det_Exception                      NVARCHAR(30)     -- @Exception
, det_SerialNum                      NVARCHAR(60)     -- @SerialNumber
, suba_CoProdExist                   TINYINT          -- @CoProdExist
, coprod_item                        NVARCHAR(30)     -- @CoProdItem
, coprod_itemdesc                    NVARCHAR(40)     -- @CoProdItemDescription
, coprod_QtyReleased                 DECIMAL(19,8)    -- @CoProdQtyReleased
, coprod_U_M                         NVARCHAR(3)      -- @CoProdUM
, nettable                           TINYINT          -- @Nettable
, qty_unit_format                    NVARCHAR(60)     -- @QtyUnitFormat
, places_qty_unit                    TINYINT          -- @PlacesQtyUnit
   )
   
DECLARE @Features TABLE (
  JobrouteJob                        NVARCHAR(20)
, JobrouteSuffix                     INT
, JobrouteOperNum                    INT
, FeatureDisplayQty                  DECIMAL(25,8)
, FeatureDisplayUM                   NVARCHAR(10)
, FeatureDisplayDesc                 NVARCHAR(60)
, FeatureDisplayStr                  NVARCHAR(60)
, CoitemAllRowPointer                UNIQUEIDENTIFIER
, seq                                INT IDENTITY
)

DECLARE
  @AllExPickupDate                   FlagNyType

---SETS
SET @Severity         = 0
SET @MinShipNum       = ISNULL(@MinShipNum, dbo.HighInt())  -- AIT JCW 09/11/17 Modified from LowInt to HighInt.  This will force the Shipment ID to exist so if can be called from the AIT Pack and Scan form without a shipment ID it will fail instead of printing everything ever.
SET @MaxShipNum       = ISNULL(@MaxShipNum, dbo.HighInt())
SET @CustomerStarting = ISNULL(@CustomerStarting, dbo.LowCharacter())
SET @CustomerEnding   = ISNULL(@CustomerEnding, dbo.HighCharacter())
SET @ShiptoStarting   = ISNULL(@ShiptoStarting, dbo.LowInt())
SET @ShiptoEnding     = ISNULL(@ShiptoEnding, dbo.HighInt())
SET @AllExPickupDate  = CASE WHEN @PickupDateStarting IS NULL AND @PickupDateEnding IS NULL THEN 1 ELSE 0 END
EXEC dbo.ApplyDateOffsetSp @PickupDateStarting OUTPUT, @DateStartingOffset, 0
EXEC dbo.ApplyDateOffsetSp @PickupDateEnding   OUTPUT, @DateEndingOffset, 1


--Check for Thai License
SELECT @IsThaiFlag = dbo.IsAddonAvailable('ThailandCountryPack')

SELECT @QtyUnitFormat = qty_unit_format
     , @PlacesQtyUnit = places_qty_unit
FROM invparms

SET @QtyUnitFormat = dbo.FixMaskForCrystal( @QtyUnitFormat, dbo.GetWinRegDecGroup() )

SELECT @URL = url FROM parms (READUNCOMMITTED) WHERE parm_key = 0

SET @OfficeAddrFooter = dbo.DisplayAddressForReportFooter()

SELECT @CertfOfConfrnText = certificate_of_conformance_text FROM coparms (READUNCOMMITTED)

--<BEGIN AIT EWF 08/15/2019>
drop table if exists #coship
create table #coship (
  shipment_id int -- ShipmentIDType
, shipment_line int -- ShipmentIDType
, co_num nvarchar(10) -- CoNumType
, co_line int -- CoLineType
, co_release int -- CoReleaseType
, price decimal(25,8) -- CostPrcType
, shipment_seq_RP UNIQUEIDENTIFIER
--<BEG AIT EWF 09/22/2020>
, item NVARCHAR(100)
, delterm NVARCHAR(100)
, qty_ordered decimal(20,0)
, qty_shipped decimal(20,0)
, cust_item NVARCHAR(100)
, ec_code NVARCHAR(100)
, origin NVARCHAR(100)
, comm_code NVARCHAR(100)
, u_m NVARCHAR(100)
, description NVARCHAR(100)
, SS_RefType NCHAR(1)
, OrderLineRowpointer UNIQUEIDENTIFIER
--<END AIT EWF 09/22/2020>
)

--<BEG AIT EWF 08/22/2019>
Declare @QuickShip TINYINT = 0

IF LEFT(@MinShipNum,1) = 'Q' BEGIN
	SET @QuickShip = 1
	SET @MinShipNum = REPLACE(@MinShipNum,'Q','')
	SET @MaxShipNum = REPLACE(@MaxShipNum,'Q','')
END

IF @QuickShip = 1
	GOTO QuickShip
--<END AIT EWF 08/22/2019>

--<BEG AIT EWF 09/22/2020>
IF OBJECT_ID('tempdb..#AIT_SS_Ref') IS NOT NULL DROP TABLE #AIT_SS_Ref
CREATE TABLE #AIT_SS_Ref(
	co_num NVARCHAR(50)
	,cust_po NVARCHAR(50)
	,end_user_type NVARCHAR(30)
	,SS_RefType NCHAR(1)
	,taken_by NVARCHAR(100)
	,NoteExistsFlag TINYINT
	,RowPointer UniqueIdentifier
)
set @PrintDescription = 1
Declare @BOLs as Table(Shipment_id int)

IF @IsConsolidatedDoc = 0
   INSERT @BOLs Select distinct shipment_id from AIT_SS_BOL shipment WHERE (shipment.shipment_id BETWEEN @MinShipNum AND @MaxShipNum)
ELSE IF @IsConsolidatedDoc = 1
   INSERT @BOLs Select distinct shipment_id from AIT_SS_BOL shipment WHERE shipment.shipment_id in (Select BOL FROM AIT_SS_TmpConsolidatedDoc WHERE SessionID = @SessionID AND Selected = 1)

INSERT #AIT_SS_Ref
	SELECT c.co_num, c.cust_po,end_user_type, l.ref_type,taken_by,c.NoteExistsFlag,c.RowPointer FROM co c inner join AIT_SS_BOL_Line l on l.ref_num = c.co_num and l.ref_type = 'O'
	   Where l.shipment_id in (Select shipment_id from @BOLs)
	   UNION SELECT c.trn_num, NULL as cust_po, NULL as end_user_type, l.ref_type,NULL as taken_by,c.NoteExistsFlag,c.RowPointer FROM transfer c inner join AIT_SS_BOL_Line l on l.ref_num = c.trn_num and l.ref_type = 'T'
	   Where l.shipment_id in (Select shipment_id from @BOLs)
	   UNION SELECT c.sro_num, c.cust_po, end_user_type, l.ref_type,NULL as taken_by,c.NoteExistsFlag,c.RowPointer FROM fs_sro c inner join AIT_SS_BOL_Line l on l.ref_num = c.sro_num and l.ref_type = 'S'
	   Where l.shipment_id in (Select shipment_id from @BOLs)

create index AIT_SS_Ref_co on #AIT_SS_Ref (co_num, SS_RefType)
--<END AIT EWF 09/22/2020>

-- pre-build the list of co_ship records
insert into #coship
select
  shipment.shipment_id
, shipment_line.shipment_line
, co_ship.co_num
, co_ship.co_line
, co_ship.co_release
, ISNULL(v.unit_value,co_ship.price)
, shipment_seq.RowPointer
--<BEG AIT EWF 09/22/2020>
, co_ship.item
, co_ship.delterm	
, co_ship.qty_ordered	
, dbo.UomConvQty(shipment_seq.qty_picked,dbo.Getumcf(co_ship.u_m,co_ship.item,shipment.cust_num,'C'),'From Base') as qty_shipped
, co_ship.cust_item	
, co_ship.ec_code	
, co_ship.origin	
, co_ship.comm_code	
, co_ship.u_m	
, co_ship.description
, co_ship.ref_type
, co_ship.RowPointer
--<END AIT EWF 09/22/2020>
FROM 
	(	SELECT c.[co_num],c.[co_line],c.[co_release],round((price * (1.0 - Disc / 100.0)), 3) as price,'O' as ref_type,null as SROShipBasis,c.RowPointer,c.disc
				, item, delterm, qty_ordered, qty_shipped, cust_item, ec_code, origin, comm_code, u_m, description --AIT EWF 09/22/2020
		FROM coitem c WITH(NOLOCK) inner join #AIT_SS_Ref r on r.co_num = c.co_num and r.SS_RefType = 'O'
		UNION
		SELECT c.trn_num,c.trn_line,0,c.unit_price,'T' as RefType,null as SROShipBasis,c.RowPointer,0 as disc
				, c.item, c.delterm, c.qty_req, c.qty_shipped, NULL as cust_item, c.ec_code, c.origin, c.comm_code, c.u_m, NULL as description --AIT EWF 09/22/2020
		FROM TRNITEM c WITH(NOLOCK) inner join #AIT_SS_Ref r on r.co_num = c.trn_num and r.SS_RefType = 'T'
		UNION
		SELECT c.sro_num,c.sro_line,c.sro_oper+c.trans_num,round((c.price * (1.0 - c.Disc / 100.0)), 3),'S' as ref_type,'M' as SROShipBasis,c.RowPointer,c.disc
				, c.item, c.delterm, c.matl_qty, c.qty_shipped, c.cust_item, c.ec_code, c.origin, c.comm_code, c.u_m, c.description --AIT EWF 09/22/2020
		FROM FS_SRO_MATL c WITH(NOLOCK) inner join #AIT_SS_Ref r on r.co_num = c.sro_num and r.SS_RefType = 'S'
		Where c.trans_type IN ('S','E','O') and type = 'P'
		UNION
		SELECT c.sro_num,c.sro_line,c.trans_num,round((c.price * (1.0 - c.Disc / 100.0)), 3),'S' as ref_type,'L' as SROShipBasis,c.RowPointer,c.disc
				, c.item, c.delterm, c.matl_qty, c.qty_shipped, NULL as cust_item, c.ec_code, c.origin, c.comm_code, c.u_m, c.description --AIT EWF 09/22/2020
		FROM FS_SRO_Line_MATL c WITH(NOLOCK) inner join #AIT_SS_Ref r on r.co_num = c.sro_num and r.SS_RefType = 'S'
		Where c.trans_type IN ('S','E','O')  and c.type = 'P'
		) co_ship 
	LEFT JOIN AIT_SS_BOL_Line shipment_line ON shipment_line.ref_num = co_ship.co_num and shipment_line.ref_type = co_ship.ref_type
		AND co_ship.co_line = shipment_line.ref_line_suf
		AND co_ship.co_release = shipment_line.ref_release
	left join AIT_SS_PICK_LIST_Ref r on r.pick_list_id = shipment_line.pick_list_id
		and r.sequence = pick_list_ref_sequence
			AND 
				(
					(shipment_line.ref_line_suf = co_ship.co_line AND shipment_line.ref_release = co_ship.co_release and co_ship.ref_type in ('T','O'))
					OR (r.SROTranRowpointer = co_ship.rowpointer and co_ship.ref_type in ('S'))
				)
	LEFT JOIN AIT_SS_BOL_seq shipment_seq on 
		shipment_seq.shipment_id = shipment_line.shipment_id
		AND shipment_seq.shipment_line = shipment_line.shipment_line
	LEFT JOIN AIT_SS_BOL shipment on 
		shipment.shipment_id = shipment_line.shipment_id
	--<BEG AIT EWF 08/29/2019>
	LEFT JOIN AIT_SS_BOLDetailsValue v on v.shipment_id = shipment_line.shipment_id
			AND v.shipment_line = shipment_line.shipment_line --AIT EWF 11/10/2020
			AND r.item = v.item
			AND v.altered = 1
	--<END AIT EWF 08/29/2019>
WHERE 
	shipment.shipment_id IN (select shipment_id from @BOLs)
	
create index co_ship_row on #coship (shipment_id, co_num, co_line, co_release)
create index co_ship_rowp on #coship (shipment_seq_RP)
create index co_ship_item on #coship (item)
--<END AIT EWF 08/15/2019>

set @unit_price  = 0
set @ext_price   = 0
set @total_price = 0

--<BEG AIT EWF 12/10/2021>
IF @pPrintKitComponents = 0 OR (@pPrintKitComponents = 1 AND NOT EXISTS(SELECT 1 FROM #coship c inner join item i on i.item = c.item and i.kit = 1))
	INSERT INTO @ReportSet (
		 pack_num
	   , pack_date
	   , whse
	   , co_num
	   , cust_num
	   , weight
	   , qty_packages
	   , ship_code
	   , carrier
	   , office_addr
	   , office_addr2
	   , office_addr3
	   , office_addr4
	   , office_city
	   , office_state
	   , office_zip
	   , office_country
	   , office_contact
	   , office_phone
	   , ship_contact
	   , ship_addr
	   , ship_addr2
	   , ship_addr3
	   , ship_addr4
	   , ship_city
	   , ship_state
	   , ship_zip
	   , ship_country
	   , bill_contact
	   , bill_addr
	   , bill_addr2
	   , bill_addr3
	   , bill_addr4
	   , bill_city
	   , bill_state
	   , bill_zip
	   , bill_country
	   , shipment_TH_fob_point
	   , shipment_TH_item_category
	   , shipment_TH_from_shipping_port
	   , shipment_TH_to_shipping_port
	   , co_line
	   , co_release
	   , item
	   , item_desc
	   , u_m
	   , ship_rowpointer
	   , ship_package_rowpointer
	   , ship_seq_rowpointer
	   , qty_unit_format
	   , places_qty_unit   
	   , shipment_id
	   , shipment_line
	   , shipment_seq
	   , qty_picked
	   , qty_shipped
	   , package_id
	   , shipment_notes
	   , shipmentSeq_notes
	   , shipmentPackage_notes
	   , shipmentPackage_TH_carton_prefix
	   , shipmentPackage_TH_measurement
	   , shipmentPackage_TH_carton_size
	   , cust_seq
	   , from_CompanyName
	   , ship_CompanyName
	   , bill_CompanyName
	   , serial_num
	   , lot
	   , cust_po
	   , contact
	   , OfficeLongAddr
	   , BillToLongAddr
	   , ShipToLongAddr 
	   , ec_code 
	   , origin 
	   , comm_code 
	   , delterm 
	   , delterm_description
	   , url 
	   , office_addr_footer
	   , certificate_of_conformance_text
	   , unit_price
	   , ext_price
	   , CorpCust
	   , CorpCustName
	   , CountryOfOrigin
	   , LotExpDate --AIT EWF 07/30/19
	   , QtyBO --AIT EWF 07/30/19
	   , QtyOrdered --AIT EWF 07/30/19
	   , ShipViaDescription --AIT EWF 07/30/19
	   , CustItem --AIT EWF 07/30/19
	   , hts_code --AIT EWF 07/30/19
	   , sched_b_num --AIT EWF 07/30/19
	   , eccn_usml_value --AIT EWF 07/30/19
	   , commodity_jurisdiction --AIT EWF 07/30/19
	   , export_compliance_program --AIT EWF 07/30/19
	   --<BEG AIT EWF 11/05/2019>
		, TrackingNumber
		, PkgWeight
		, PltLicense
		--<END AIT EWF 11/05/2019>
		, end_user_type --AIT EWF 11/14/2019
		, end_user_type_description --AIT EWF 11/14/2019
		, taken_by --AIT EWF 12/19/2019
	   )
		SELECT
			 ship.shipment_id
			, ISNULL(ship.ShipTransDate,ISNULL(ship.pickup_date, @Today))
			, ship.whse
			, ISNULL(coship.ref_num, coitem.co_num)
			, ship.cust_num
			, ship.weight
			, ship.qty_packages
			, ship.ship_code
			, ship.carrier_code
			, consignor_addr##1
			, consignor_addr##2
			, consignor_addr##3
			, consignor_addr##4
			, ship.consignor_city
			, ship.consignor_state
			, ship.consignor_zip
			, ship.consignor_country
			, ship.consignor_contact
			, ship.consignor_phone
			, ship.consignee_contact
			, ship.consignee_addr##1
			, ship.consignee_addr##2
			, ship.consignee_addr##3
			, ship.consignee_addr##4
			, ship.consignee_city
			, ship.consignee_state
			, ship.consignee_zip
			, ship.consignee_country
			, ship.invoicee_contact
			, ship.invoicee_addr##1
			, ship.invoicee_addr##2
			, ship.invoicee_addr##3
			, ship.invoicee_addr##4
			, ship.invoicee_city
			, ship.invoicee_state
			, ship.invoicee_zip
			, ship.invoicee_country
			, ship.TH_fob_point
			, ship.TH_item_category
			, ship.TH_from_shipping_port
			, ship.TH_to_shipping_port
			, ISNULL(coship.ref_line_suf, coitem.co_line)
			, ISNULL(coship.ref_release, coitem.co_release)
			, ISNULL(coship.item,coitem.item)
			, CASE WHEN 1 = 1 THEN ISNULL(coitem.description,i.description) ELSE NULL END
			, ISNULL(coitem.u_m,i.u_m)
			, ship.RowPointer   
			, shipment_package.RowPointer   
			, shipment_seq.RowPointer
			, @QtyUnitFormat
			, @PlacesQtyUnit   
			, shipment_seq.shipment_id
			, shipment_seq.shipment_line
			, shipment_seq.shipment_seq
			, dbo.UomConvQty(shipment_seq.qty_picked,dbo.Getumcf(item.u_m,item.item,ship.cust_num,'C'),'From Base')
			, dbo.UomConvQty(shipment_seq.qty_shipped,dbo.Getumcf(item.u_m,item.item,ship.cust_num,'C'),'From Base')
			, shipment_seq.package_id
			, CASE WHEN @PrintShipmentSequenceText = 1 THEN dbo.ReportNotesExist('AIT_SS_BOL', ship.RowPointer, @ShowInternal, @ShowExternal, ship.NoteExistsFlag) ELSE 0 END
			, CASE WHEN @DisplayShipmentSeqNotes = 1 THEN dbo.ReportNotesExist('AIT_SS_BOL_Seq', shipment_seq.RowPointer, @ShowInternal, @ShowExternal, shipment_seq.NoteExistsFlag) ELSE 0 END
			, CASE WHEN @DisplayShipmentPackageNotes = 1 THEN dbo.ReportNotesExist('AIT_SS_BOL_Package', shipment_package.RowPointer, @ShowInternal, @ShowExternal, shipment_package.NoteExistsFlag) ELSE 0 END
			, shipment_package.TH_carton_prefix
			, shipment_package.TH_measurement
			, shipment_package.TH_carton_size
			, ship.cust_seq
			, consignor_name
			, consignee_name
			, invoicee_name
			, CASE WHEN  @IncludeSerialNumbers = 1 THEN shipment_seq_serial.ser_num ELSE NULL END
			, shipment_seq.lot
			, co.cust_po as cust_po
			, ship.consignor_contact --co.contact
			, dbo.MultiLineAddressSp(CASE WHEN invoicee_name IS NULL THEN invoicee_name ELSE consignor_name END,consignor_addr##1,consignor_addr##2,consignor_addr##3, consignor_addr##4,ship.consignor_city,ship.consignor_state,ship.consignor_zip,ship.consignor_country,ISNULL(ship.consignor_contact,''))
			, dbo.MultiLineAddressSp(invoicee_name,ship.invoicee_addr##1,ship.invoicee_addr##2,ship.invoicee_addr##3,ship.invoicee_addr##4,ship.invoicee_city,ship.invoicee_state,ship.invoicee_zip,ship.invoicee_country,ship.invoicee_contact)
			, dbo.MultiLineAddressSp(consignee_name,ship.consignee_addr##1,ship.consignee_addr##2,ship.consignee_addr##3,ship.consignee_addr##4, ship.consignee_city,ship.consignee_state,ship.consignee_zip,ship.consignee_country,ship.consignee_contact)
			, coitem.ec_code
			, coitem.origin
			, coitem.comm_code
			, coitem.delterm
			, del_term.description
			, @URL
			, @OfficeAddrFooter
			, @CertfOfConfrnText
			, coitem.price --AIT EWF 08/15/2019
			, coitem.price * shipment_seq.qty_picked
			, corpcust.cust_num
			, corpcust.name
			, ISNULL(c.iso_country_code, ISNULL(item.country,ai.CountryOfOrigin)) as iso_country_code
			, lot.exp_date --AIT EWF 07/30/19
			, dbo.UomConvQty(coitem.qty_ordered - coitem.qty_shipped,dbo.Getumcf(item.u_m,item.item,ship.cust_num,'C'),'From Base')
			, dbo.UomConvQty(coitem.qty_ordered,dbo.Getumcf(item.u_m,item.item,ship.cust_num,'C'),'From Base')
			, sc.description --AIT EWF 07/30/19
			, coitem.cust_item --AIT EWF 07/30/19
			, ISNULL(ic.hts_code, item.hts_code) --AIT EWF 07/30/19 -- AIT BTS 12282023
			, ISNULL(ic.sched_b_num, item.sched_b_num) --AIT BTS 12282023
			, item.eccn_usml_value
			, item.commodity_jurisdiction
			, item.export_compliance_program
			, shipment_package.TrackingNumber
			, shipment_package.weight
			, shipment_package.PalletLicense
			, co.end_user_type --AIT EWF 11/14/2019
			, et.description as end_user_type_description --AIT EWF 11/14/2019
			, co.taken_by --AIT EWF 12/19/2019
		FROM 
			AIT_SS_BOL ship WITH(NOLOCK)
			LEFT JOIN AIT_SS_BOL_Line shipment_line WITH(NOLOCK) ON ship.shipment_id = shipment_line.shipment_id
			LEFT JOIN AIT_SS_BOL_seq shipment_seq WITH(NOLOCK) ON shipment_seq.shipment_line = shipment_line.shipment_line
				AND shipment_seq.shipment_id = shipment_line.shipment_id
			LEFT JOIN AIT_SS_BOL_Package shipment_package WITH(NOLOCK) ON shipment_package.shipment_id = ship.shipment_id
				AND shipment_package.package_id = shipment_seq.package_id
			LEFT JOIN AIT_SS_pick_list_ref coship WITH(NOLOCK) ON shipment_line.pick_list_id = coship.pick_list_id
				AND shipment_line.pick_list_ref_sequence = coship.sequence
			LEFT JOIN AIT_SS_BOL_Seq_Serial shipment_seq_serial WITH(NOLOCK) ON shipment_seq_serial.shipment_id = shipment_seq.shipment_id
				AND shipment_seq_serial.shipment_line = shipment_seq.shipment_line
				AND shipment_seq_serial.shipment_seq = shipment_seq.shipment_seq
			LEFT JOIN customer AS shipto WITH(NOLOCK) ON shipto.cust_num = ship.cust_num
				AND shipto.cust_seq = 0
			LEFT JOIN custaddr WITH(NOLOCK) ON custaddr.cust_num = ship.cust_num
				AND custaddr.cust_seq = ship.cust_seq
			LEFT JOIN custaddr corpcust WITH(NOLOCK) ON custaddr.corp_cust = corpcust.cust_num
				AND corpcust.cust_seq = 0
			LEFT OUTER JOIN whse WITH(NOLOCK) ON whse.whse = ship.whse
			LEFT OUTER JOIN shipcode ON shipcode.ship_code = ship.ship_code
			LEFT JOIN #coship coitem WITH(NOLOCK) ON shipment_line.ref_num = coitem.co_num
				AND shipment_line.ref_line_suf = coitem.co_line
				AND shipment_line.ref_release = coitem.co_release
				AND coitem.SS_RefType = shipment_line.ref_type
				AND shipment_seq.RowPointer = coitem.shipment_seq_RP
			LEFT JOIN #AIT_SS_Ref co WITH(NOLOCK) ON coship.ref_num = co.co_num --AIT EWF 09/22/2020
				AND co.SS_RefType = coship.ref_type
			LEFT OUTER JOIN co_bln WITH(NOLOCK) ON co_bln.co_num = shipment_line.ref_num
				AND co_bln.co_line = shipment_line.ref_line_suf
			LEFT JOIN item WITH(NOLOCK) ON item.item = coship.item
			LEFT OUTER JOIN ship_lang WITH(NOLOCK) ON ship_lang.ship_code = shipcode.ship_code
				AND ship_lang.lang_code = shipto.lang_code
			LEFT OUTER JOIN item_lang WITH(NOLOCK) ON item_lang.item = coitem.item
				AND item_lang.lang_code = shipto.lang_code
			LEFT OUTER JOIN del_term WITH(NOLOCK) ON del_term.delterm = coitem.delterm
			LEFT JOIN item i on i.item = coship.item
			left join lot on lot.item = coitem.item	and lot.lot = shipment_seq.lot --AIT EWF 07/30/19
			left join shipcode sc on sc.ship_code = ship.ship_code --AIT EWF 07/30/19
			left join AITSSSLItems ai on ai.item = item.item
			left join country c on c.country = ISNULL(item.country, ai.CountryOfOrigin)
			left join endtype et on et.end_user_type = co.end_user_type
			left join ue_ss_itemcountry ic on ic.item = coitem.item AND ic.country = ship.consignee_country --AIT BTS 12282023
		WHERE 
--<BEG AIT EWF 09/22/2020>
			ship.shipment_id IN (select shipment_id from @BOLs)
--<END AIT EWF 09/22/2020>
		  AND ((ship.pickup_date >= @PickupDateStarting AND ship.pickup_date <= @PickupDateEnding) OR @AllExPickupDate = 1 OR @IsConsolidatedDoc = 1) --AIT EWF 09/22/2020
		  AND ((ship.pickup_date IS NOT NULL OR @PrintBlankPickupDate =1) OR @PrintBlankPickupDate =0 OR @IsConsolidatedDoc = 1) --AIT EWF 09/22/2020
		  --AND (ship.pickup_date IS NOT NULL OR @PrintBlankPickupDate =1)
		  AND 
			(
				(ship.cust_num >= @CustomerStarting
				AND ship.cust_num <= @CustomerEnding
				AND ship.cust_seq >= @ShiptoStarting
				AND ship.cust_seq <= @ShiptoEnding
				--AND (@UseProfile = 0 OR @UseProfile IS NULL OR ISNULL(@LangCode,'') = (SELECT ISNULL(customer.lang_Code,'') FROM customer WHERE customer.cust_num=ship.cust_num AND customer.cust_seq=0))
				AND shipment_line.ref_type <> 'T'
				)
				OR shipment_line.ref_type = 'T'
			)
ELSE BEGIn
--<END AIT EWF 12/10/2021>
DECLARE ShipPackSlipCrs CURSOR LOCAL STATIC FOR
SELECT
  ship.shipment_id
, ISNULL(ship.ShipTransDate,ISNULL(ship.pickup_date, @Today))
, ship.whse
, ISNULL(coship.ref_num, coitem.co_num)
, ship.cust_num
, ship.weight
, ship.qty_packages
, ship.ship_code
, ship.carrier_code
, consignor_addr##1
, consignor_addr##2
, consignor_addr##3
, consignor_addr##4
, ship.consignor_city
, ship.consignor_state
, ship.consignor_zip
, ship.consignor_country
, ship.consignor_contact
, ship.consignor_phone
, ship.consignee_contact
, ship.consignee_addr##1
, ship.consignee_addr##2
, ship.consignee_addr##3
, ship.consignee_addr##4
, ship.consignee_city
, ship.consignee_state
, ship.consignee_zip
, ship.consignee_country
, ship.invoicee_contact
, ship.invoicee_addr##1
, ship.invoicee_addr##2
, ship.invoicee_addr##3
, ship.invoicee_addr##4
, ship.invoicee_city
, ship.invoicee_state
, ship.invoicee_zip
, ship.invoicee_country
, ship.TH_fob_point
, ship.TH_item_category
, ship.TH_from_shipping_port
, ship.TH_to_shipping_port
, ISNULL(coship.ref_line_suf, coitem.co_line)
, ISNULL(coship.ref_release, coitem.co_release)
, ISNULL(coship.item,coitem.item)
, CASE WHEN 1 = 1
       THEN ISNULL(i.description,coitem.description)
       ELSE NULL
  END
, ISNULL(coitem.u_m,i.u_m)
, ship.RowPointer   
, shipment_package.RowPointer   
, shipment_seq.RowPointer
, @QtyUnitFormat
, @PlacesQtyUnit   
, shipment_seq.shipment_id
, shipment_seq.shipment_line
, shipment_seq.shipment_seq
, shipment_seq.qty_picked
, shipment_seq.qty_shipped
, shipment_seq.package_id
, CASE WHEN @PrintShipmentSequenceText = 1
       THEN dbo.ReportNotesExist('AIT_SS_BOL', ship.RowPointer, @ShowInternal, @ShowExternal, ship.NoteExistsFlag)
       ELSE 0
  END
, CASE WHEN @DisplayShipmentSeqNotes = 1
       THEN dbo.ReportNotesExist('AIT_SS_BOL_Seq', shipment_seq.RowPointer, @ShowInternal, @ShowExternal, shipment_seq.NoteExistsFlag)
       ELSE 0
       END
, CASE WHEN @DisplayShipmentPackageNotes = 1
       THEN dbo.ReportNotesExist('AIT_SS_BOL_Package', shipment_package.RowPointer, @ShowInternal, @ShowExternal, shipment_package.NoteExistsFlag)
       ELSE 0
  END
, shipment_package.TH_carton_prefix
, shipment_package.TH_measurement
, shipment_package.TH_carton_size
, ship.cust_seq
, consignor_name
, consignee_name
, invoicee_name
, CASE WHEN  @IncludeSerialNumbers = 1
       THEN shipment_seq_serial.ser_num
       ELSE NULL
  END
, shipment_seq.lot
, co.cust_po as cust_po
, ship.consignor_contact --co.contact
, dbo.MultiLineAddressSp(CASE WHEN invoicee_name IS NULL THEN invoicee_name ELSE consignor_name END,consignor_addr##1,consignor_addr##2,consignor_addr##3,
                         consignor_addr##4,ship.consignor_city,ship.consignor_state,ship.consignor_zip,ship.consignor_country,ISNULL(ship.consignor_contact,''))
, dbo.MultiLineAddressSp(invoicee_name,ship.invoicee_addr##1,ship.invoicee_addr##2,ship.invoicee_addr##3,ship.invoicee_addr##4,
                         ship.invoicee_city,ship.invoicee_state,ship.invoicee_zip,ship.invoicee_country,ship.invoicee_contact)
, dbo.MultiLineAddressSp(consignee_name,ship.consignee_addr##1,ship.consignee_addr##2,ship.consignee_addr##3,ship.consignee_addr##4,
                         ship.consignee_city,ship.consignee_state,ship.consignee_zip,ship.consignee_country,ship.consignee_contact)
, coitem.ec_code
, coitem.origin
, coitem.comm_code
, coitem.delterm
, del_term.description
, @URL
, @OfficeAddrFooter
, @CertfOfConfrnText
, item.kit
		, coitem.price --AIT EWF 08/15/2019
, corpcust.cust_num
, corpcust.name
, ISNULL(c.iso_country_code, ISNULL(item.country,ai.CountryOfOrigin)) as iso_country_code
, lot.exp_date --AIT EWF 07/30/19
, coitem.qty_ordered - coitem.qty_shipped as QtyBO --AIT EWF 07/30/19
, coitem.qty_ordered as QtyOrdered --AIT EWF 07/30/19
, sc.description --AIT EWF 07/30/19
, coitem.cust_item --AIT EWF 07/30/19
, ISNULL(ic.hts_code, item.hts_code) --AIT EWF 07/30/19 --AIT BTS 12282023
, item.comm_code
, ISNULL(ic.sched_b_num, item.sched_b_num) --AIT BTS 12282023
, item.eccn_usml_value
, item.commodity_jurisdiction
, item.export_compliance_program
, item.nafta_pref_crit
--<BEG AIT EWF 11/05/2019>
, shipment_package.TrackingNumber
, shipment_package.weight
, shipment_package.PalletLicense
--<END AIT EWF 11/05/2019>
, co.end_user_type --AIT EWF 11/14/2019
, et.description as end_user_type_description --AIT EWF 11/14/2019
, co.taken_by --AIT EWF 12/19/2019
FROM AIT_SS_BOL ship WITH(NOLOCK)
LEFT JOIN AIT_SS_BOL_Line shipment_line WITH(NOLOCK) ON ship.shipment_id = shipment_line.shipment_id
LEFT JOIN AIT_SS_BOL_seq shipment_seq WITH(NOLOCK) ON shipment_seq.shipment_line = shipment_line.shipment_line
                                   AND shipment_seq.shipment_id = shipment_line.shipment_id
LEFT JOIN AIT_SS_BOL_Package shipment_package WITH(NOLOCK) ON shipment_package.shipment_id = ship.shipment_id
                                   AND shipment_package.package_id = shipment_seq.package_id
LEFT JOIN AIT_SS_pick_list_ref coship WITH(NOLOCK) ON shipment_line.pick_list_id = coship.pick_list_id
                                           AND shipment_line.pick_list_ref_sequence = coship.sequence
LEFT JOIN AIT_SS_BOL_Seq_Serial shipment_seq_serial WITH(NOLOCK) ON shipment_seq_serial.shipment_id = shipment_seq.shipment_id
                                          AND shipment_seq_serial.shipment_line = shipment_seq.shipment_line
                                          AND shipment_seq_serial.shipment_seq = shipment_seq.shipment_seq
LEFT JOIN customer AS shipto WITH(NOLOCK) ON shipto.cust_num = ship.cust_num
                                         AND shipto.cust_seq = 0
LEFT JOIN custaddr WITH(NOLOCK) ON custaddr.cust_num = ship.cust_num
                               AND custaddr.cust_seq = ship.cust_seq
LEFT JOIN custaddr corpcust WITH(NOLOCK) ON custaddr.corp_cust = corpcust.cust_num
                               AND corpcust.cust_seq = 0
LEFT OUTER JOIN whse WITH(NOLOCK) ON whse.whse = ship.whse
LEFT OUTER JOIN shipcode ON shipcode.ship_code = ship.ship_code
		LEFT JOIN #coship coitem WITH(NOLOCK) ON shipment_line.ref_num = coitem.co_num
									 AND shipment_line.ref_line_suf = coitem.co_line
									 AND shipment_line.ref_release = coitem.co_release
							 AND coitem.SS_RefType = shipment_line.ref_type
							 AND shipment_seq.RowPointer = coitem.shipment_seq_RP
		LEFT JOIN #AIT_SS_Ref co WITH(NOLOCK) ON coship.ref_num = co.co_num --AIT EWF 09/22/2020
	AND co.SS_RefType = coship.ref_type
		LEFT OUTER JOIN co_bln WITH(NOLOCK) ON co_bln.co_num = shipment_line.ref_num
										   AND co_bln.co_line = shipment_line.ref_line_suf
LEFT JOIN item WITH(NOLOCK) ON item.item = coship.item
LEFT OUTER JOIN ship_lang WITH(NOLOCK) ON ship_lang.ship_code = shipcode.ship_code
                                      AND ship_lang.lang_code = shipto.lang_code
LEFT OUTER JOIN item_lang WITH(NOLOCK) ON item_lang.item = coitem.item
                                      AND item_lang.lang_code = shipto.lang_code
LEFT OUTER JOIN del_term WITH(NOLOCK) ON del_term.delterm = coitem.delterm
		LEFT JOIN item i on i.item = coship.item
left join lot on lot.item = coitem.item	and lot.lot = shipment_seq.lot --AIT EWF 07/30/19
left join shipcode sc on sc.ship_code = ship.ship_code --AIT EWF 07/30/19
left join AITSSSLItems ai on ai.item = item.item
left join country c on c.country = ISNULL(item.country, ai.CountryOfOrigin)
left join endtype et on et.end_user_type = co.end_user_type
left join ue_ss_itemcountry ic on ic.item = coship.item AND ic.country = ship.consignee_country --AIT BTS 12282023
WHERE 
  --<BEG AIT EWF 09/22/2020>
	ship.shipment_id IN (select shipment_id from @BOLs)
	--<END AIT EWF 09/22/2020>
  AND ((ship.pickup_date >= @PickupDateStarting AND ship.pickup_date <= @PickupDateEnding) OR @AllExPickupDate = 1 OR @IsConsolidatedDoc = 1) --AIT EWF 09/22/2020
  AND ((ship.pickup_date IS NOT NULL OR @PrintBlankPickupDate =1) OR @PrintBlankPickupDate =0 OR @IsConsolidatedDoc = 1) --AIT EWF 09/22/2020
  --AND (ship.pickup_date IS NOT NULL OR @PrintBlankPickupDate =1)
  AND 
	(
		(ship.cust_num >= @CustomerStarting
		AND ship.cust_num <= @CustomerEnding
		AND ship.cust_seq >= @ShiptoStarting
		AND ship.cust_seq <= @ShiptoEnding
		AND (@UseProfile = 0 OR @UseProfile IS NULL OR ISNULL(@LangCode,'') = (SELECT ISNULL(customer.lang_Code,'') FROM customer WHERE customer.cust_num=ship.cust_num AND customer.cust_seq=0))
		AND shipment_line.ref_type <> 'T'
		)
		OR shipment_line.ref_type = 'T'
	)
OPEN ShipPackSlipCrs
   WHILE (1=1)
   BEGIN
      FETCH ShipPackSlipCrs INTO
              @PackNum
            , @PackDate
            , @Whse
            , @CoNUm
            , @CustNum
            , @Weight
            , @QtyPackages
            , @ShipCode
            , @Carrier
            , @OfficeAddr
            , @OfficeAddr2
            , @OfficeAddr3
            , @OfficeAddr4
            , @OfficeCity
            , @OfficeState
            , @OfficeZip
            , @OfficeCountry
            , @OfficeContact
            , @OfficePhone
            , @ShipContact
            , @ShipAddr
            , @ShipAddr2
            , @ShipAddr3
            , @ShipAddr4
            , @ShipCity
            , @ShipState
            , @ShipZip
            , @ShipCountry
            , @BillContact
            , @BillAddr
            , @BillAddr2
            , @BillAddr3
            , @BillAddr4
            , @BillCity
            , @BillState
            , @BillZip
            , @BillCountry
            , @Shipment_TH_FobPoint
            , @Shipment_TH_ItemCategory
            , @Shipment_TH_FromShippingPort
            , @Shipment_TH_ToShippingPort
            , @CoLine
            , @CoRelease
            , @Item
            , @ItemDesc
            , @UM
            , @ShipRowpointer
            , @ShipPackageRowpointer
            , @ShipSeqRowpointer
            , @QtyUnitFormat
            , @PlacesQtyUnit
            , @ShipmentId
            , @ShipmentLine
            , @ShipmentSeq
            , @QtyPicked
            , @QtyShipped
            , @PackageId
            , @ShipmentNotes
            , @ShipmentSeqNotes
            , @ShipmentPackageNotes
            , @ShipmentPackage_TH_CartonPrefix
            , @ShipmentPackage_TH_Measurement
            , @ShipmentPackage_TH_CartonSize
            , @CustSeq
            , @FromCompanyName
            , @ShipCompanyName
            , @BillCompanyName
            , @SerialNum
            , @Lot
            , @CustPo
            , @Contact
            , @OfficeLongAddr
            , @BillToLongAddr
            , @ShipToLongAddr 
            , @EcCode 
            , @Origin 
            , @comm_code 
            , @Delterm 
            , @DeltermDescription
            , @URL 
            , @OfficeAddrFooter
            , @CertificateOfConformanceText
            , @Kit
			, @unit_price
			, @CorpCust
			, @CorpCustName
			, @CountryOfOrigin
			, @LotExpDate --AIT EWF 07/30/19
			, @QtyBO --AIT EWF 07/30/19
			, @QtyOrdered --AIT EWF 07/30/19
			, @ShipViaDescription --AIT EWF 07/30/19
			, @CustItem --AIT EWF 07/30/19
			, @hts_code --AIT EWF 07/30/19
			, @comm_code --AIT EWF 07/30/19
			, @sched_b_num--AIT EWF 07/30/19
			, @eccn_usml_value--AIT EWF 07/30/19
			, @commodity_jurisdiction--AIT EWF 07/30/19
			, @export_compliance_program--AIT EWF 07/30/19
			, @NaftPrefCrit--AIT EWF 07/30/19
			--<BEG AIT EWF 11/05/2019>
			, @TrackingNumber
			, @PkgWeight
			, @PltLicense
			--<END AIT EWF 11/05/2019>
			, @end_user_type --AIT EWF 11/14/2019
			, @end_user_type_description --AIT EWF 11/14/2019
			, @taken_by --AIT EWF 12/19/2019

      IF @@FETCH_STATUS <> 0
      BREAK
-- Get UM convert factor
SET @UomConvFactor = dbo.Getumcf(@UM,@Item,@CustNum,'C')
	
	set @ext_price   = @unit_price * @QtyPicked
	set @total_price = @total_price + @ext_price
INSERT INTO @ReportSet (
     pack_num
   , pack_date
   , whse
   , co_num
   , cust_num
   , weight
   , qty_packages
   , ship_code
   , carrier
   , office_addr
   , office_addr2
   , office_addr3
   , office_addr4
   , office_city
   , office_state
   , office_zip
   , office_country
   , office_contact
   , office_phone
   , ship_contact
   , ship_addr
   , ship_addr2
   , ship_addr3
   , ship_addr4
   , ship_city
   , ship_state
   , ship_zip
   , ship_country
   , bill_contact
   , bill_addr
   , bill_addr2
   , bill_addr3
   , bill_addr4
   , bill_city
   , bill_state
   , bill_zip
   , bill_country
   , shipment_TH_fob_point
   , shipment_TH_item_category
   , shipment_TH_from_shipping_port
   , shipment_TH_to_shipping_port
   , co_line
   , co_release
   , item
   , item_desc
   , u_m
   , ship_rowpointer
   , ship_package_rowpointer
   , ship_seq_rowpointer
   , qty_unit_format
   , places_qty_unit   
   , shipment_id
   , shipment_line
   , shipment_seq
   , qty_picked
   , qty_shipped
   , package_id
   , shipment_notes
   , shipmentSeq_notes
   , shipmentPackage_notes
   , shipmentPackage_TH_carton_prefix
   , shipmentPackage_TH_measurement
   , shipmentPackage_TH_carton_size
   , cust_seq
   , from_CompanyName
   , ship_CompanyName
   , bill_CompanyName
   , serial_num
   , lot
   , cust_po
   , contact
   , OfficeLongAddr
   , BillToLongAddr
   , ShipToLongAddr 
   , ec_code 
   , origin 
   , comm_code 
   , delterm 
   , delterm_description
   , url 
   , office_addr_footer
   , certificate_of_conformance_text
   , unit_price
   , ext_price
   , CorpCust
   , CorpCustName
   , CountryOfOrigin
   , LotExpDate --AIT EWF 07/30/19
   , QtyBO --AIT EWF 07/30/19
   , QtyOrdered --AIT EWF 07/30/19
   , ShipViaDescription --AIT EWF 07/30/19
   , CustItem --AIT EWF 07/30/19
   , hts_code --AIT EWF 07/30/19
   , sched_b_num --AIT EWF 07/30/19
   , eccn_usml_value --AIT EWF 07/30/19
   , commodity_jurisdiction --AIT EWF 07/30/19
   , export_compliance_program --AIT EWF 07/30/19
   --<BEG AIT EWF 11/05/2019>
	, TrackingNumber
	, PkgWeight
	, PltLicense
	--<END AIT EWF 11/05/2019>
	, end_user_type --AIT EWF 11/14/2019
	, end_user_type_description --AIT EWF 11/14/2019
	, taken_by --AIT EWF 12/19/2019
   )
   VALUES (
     @PackNum
   , @PackDate
   , @Whse
   , @CoNum
   , @CustNum
   , @Weight
   , @QtyPackages
   , @ShipCode
   , @Carrier
   , @OfficeAddr
   , @OfficeAddr2
   , @OfficeAddr3
   , @OfficeAddr4
   , @OfficeCity
   , @OfficeState
   , @OfficeZip
   , @OfficeCountry
   , @OfficeContact
   , @OfficePhone
   , @ShipContact
   , @ShipAddr
   , @ShipAddr2
   , @ShipAddr3
   , @ShipAddr4
   , @ShipCity
   , @ShipState
   , @ShipZip
   , @ShipCountry
   , @BillContact
   , @BillAddr
   , @BillAddr2
   , @BillAddr3
   , @BillAddr4
   , @BillCity
   , @BillState
   , @BillZip
   , @BillCountry
   , @Shipment_TH_FobPoint
   , @Shipment_TH_ItemCategory
   , @Shipment_TH_FromShippingPort
   , @Shipment_TH_ToShippingPort
   , @CoLine
   , @CoRelease
   , @Item
   , 'desc'
   , @UM
   , @ShipRowpointer
   , @ShipPackageRowpointer
   , @ShipSeqRowpointer
   , @QtyUnitFormat
   , @PlacesQtyUnit
   , @ShipmentId
   , @ShipmentLine
   , @ShipmentSeq
   , dbo.UomConvQty(@QtyPicked,@UomConvFactor,'From Base')
   , dbo.UomConvQty(@QtyShipped,@UomConvFactor,'From Base')
   , @PackageId
   , @ShipmentNotes
   , @ShipmentSeqNotes
   , @ShipmentPackageNotes
   , @ShipmentPackage_TH_CartonPrefix
   , @ShipmentPackage_TH_Measurement
   , @ShipmentPackage_TH_CartonSize
   , @CustSeq
   , @FromCompanyName
   , @ShipCompanyName
   , @BillCompanyName
   , @SerialNum
   , @Lot
   , @CustPo
   , @Contact
   , @OfficeLongAddr
   , @BillToLongAddr
   , @ShipToLongAddr 
   , @EcCode 
   , @Origin 
   , @comm_code 
   , @Delterm 
   , @DeltermDescription
   , @URL 
   , @OfficeAddrFooter
   , @CertificateOfConformanceText
   , @unit_price
   , @ext_price
   , @CorpCust
   , @CorpCustName
   , @CountryOfOrigin
   , @LotExpDate --AIT EWF 07/30/19
   , @QtyBO --AIT EWF 07/30/19
   , dbo.UomConvQty(@QtyOrdered,@UomConvFactor,'From Base')
   , @ShipViaDescription --AIT EWF 07/30/19
   , @CustItem --AIT EWF 07/30/19
   , @hts_code --AIT EWF 07/30/19
   , @sched_b_num --AIT EWF 07/30/19
   , @eccn_usml_value --AIT EWF 07/30/19
   , @commodity_jurisdiction --AIT EWF 07/30/19
   , @export_compliance_program --AIT EWF 07/30/19
   --<BEG AIT EWF 11/05/2019>
	, @TrackingNumber
	, @PkgWeight
	, @PltLicense
	--<END AIT EWF 11/05/2019>
	, @end_user_type --AIT EWF 11/14/2019
	, @end_user_type_description --AIT EWF 11/14/2019
	, @taken_by --AIT EWF 12/19/2019
   )
	   
    IF @Kit = 1 AND @pPrintKitComponents = 1
    BEGIN
      SELECT
         @Job = item.job
       , @Suffix = 0
      FROM item
      WHERE item.item = @item

      EXEC @Severity = dbo.JobPickSp
           @Job
         , @Suffix
         , @whse
         , NULL  -- StartingOperNum
         , NULL  -- EndingOperNum
         , 0     -- SortByLoc
         , @IncludeSerialNumbers
         , 1     -- ReprintPickListItems
         , 0     -- PrintSecondaryLocations
         , 0     -- ExtendByScrapFactor
         , 0     -- PostMaterialIssues
         , @QtyPicked
         , NULL  -- PostDate
         , @Counter OUTPUT
         , @Infobar OUTPUT

      IF @Severity <> 0
      BEGIN
         IF @TaskId IS NOT NULL
            EXEC dbo.AddProcessErrorLogSp
                 @ProcessId = @TaskId
               , @InfobarText = @Infobar
               , @MessageSeverity = @Severity

         TRUNCATE TABLE #PickList
         CONTINUE
      END

      INSERT INTO @ReportSet (
           pack_num
         , pack_date
         , whse
         , co_num
         , cust_num
         , weight
         , qty_packages
         , ship_code
         , carrier
         , office_addr
         , office_addr2
         , office_addr3
         , office_addr4
         , office_city
         , office_state
         , office_zip
         , office_country
         , office_contact
         , office_phone
         , ship_contact
         , ship_addr
         , ship_addr2
         , ship_addr3
         , ship_addr4
         , ship_city
         , ship_state
         , ship_zip
         , ship_country
         , bill_contact
         , bill_addr
         , bill_addr2
         , bill_addr3
         , bill_addr4
         , bill_city
         , bill_state
         , bill_zip
         , bill_country
         , shipment_TH_fob_point
         , shipment_TH_item_category
         , shipment_TH_from_shipping_port
         , shipment_TH_to_shipping_port
         , co_line
         , co_release
         , item
         , item_desc
         , u_m
         , ship_rowpointer
         , ship_package_rowpointer
         , ship_seq_rowpointer
         , qty_unit_format
         , places_qty_unit   
         , shipment_id
         , shipment_line
         , shipment_seq
         , qty_picked
         , qty_shipped
         , package_id
         , shipment_notes
         , shipmentSeq_notes
         , shipmentPackage_notes
         , shipmentPackage_TH_carton_prefix
         , shipmentPackage_TH_measurement
         , shipmentPackage_TH_carton_size
         , cust_seq
         , from_CompanyName
         , ship_CompanyName
         , bill_CompanyName
         , serial_num
         , lot
         , cust_po
         , contact
         , OfficeLongAddr
         , BillToLongAddr
         , ShipToLongAddr 
         , ec_code 
         , origin 
         , comm_code 
         , delterm 
         , delterm_description
         , url 
         , office_addr_footer
         , certificate_of_conformance_text
         , Comp_Item
         , Comp_U_M
         , Comp_ItemDesc
         , Comp_TotalRequired
         , unit_price
         , ext_price
         , CorpCust
		 , CorpCustName
		 , CountryOfOrigin
		 , LotExpDate --AIT EWF 07/30/19
		 , QtyBO --AIT EWF 07/30/19
		 , QtyOrdered --AIT EWF 07/30/19
		 , ShipViaDescription --AIT EWF 07/30/19
		 , CustItem --AIT EWF 07/30/19
		 , hts_code --AIT EWF 07/30/19
		 , sched_b_num --AIT EWF 07/30/19
		 , eccn_usml_value --AIT EWF 07/30/19
		 , commodity_jurisdiction --AIT EWF 07/30/19
		 , export_compliance_program --AIT EWF 07/30/19
		 , NaftPrefCrit --AIT EWF 07/30/19
		--<BEG AIT EWF 11/05/2019>
		, TrackingNumber
		, PkgWeight
		, PltLicense
		--<END AIT EWF 11/05/2019>
		, end_user_type --AIT EWF 11/14/2019
		, end_user_type_description --AIT EWF 11/14/2019
		, taken_by --AIT EWF 12/19/2019
         )
      SELECT
           @PackNum
         , @PackDate
         , @Whse
         , @CoNum
         , @CustNum
         , @Weight
         , @QtyPackages
         , @ShipCode
         , @Carrier
         , @OfficeAddr
         , @OfficeAddr2
         , @OfficeAddr3
         , @OfficeAddr4
         , @OfficeCity
         , @OfficeState
         , @OfficeZip
         , @OfficeCountry
         , @OfficeContact
         , @OfficePhone
         , @ShipContact
         , @ShipAddr
         , @ShipAddr2
         , @ShipAddr3
         , @ShipAddr4
         , @ShipCity
         , @ShipState
         , @ShipZip
         , @ShipCountry
         , @BillContact
         , @BillAddr
         , @BillAddr2
         , @BillAddr3
         , @BillAddr4
         , @BillCity
         , @BillState
         , @BillZip
         , @BillCountry
         , @Shipment_TH_FobPoint
         , @Shipment_TH_ItemCategory
         , @Shipment_TH_FromShippingPort
         , @Shipment_TH_ToShippingPort
         , @CoLine
         , @CoRelease
         , @Item
         , @ItemDesc
         , @UM
         , @ShipRowpointer
         , @ShipPackageRowpointer
         , @ShipSeqRowpointer
         , @QtyUnitFormat
         , @PlacesQtyUnit
         , @ShipmentId
         , @ShipmentLine
         , @ShipmentSeq
         , dbo.UomConvQty(@QtyPicked,@UomConvFactor,'From Base')
         , dbo.UomConvQty(@QtyShipped,@UomConvFactor,'From Base')
         , @PackageId
         , @ShipmentNotes
         , @ShipmentSeqNotes
         , @ShipmentPackageNotes
         , @ShipmentPackage_TH_CartonPrefix
         , @ShipmentPackage_TH_Measurement
         , @ShipmentPackage_TH_CartonSize
         , @CustSeq
         , @FromCompanyName
         , @ShipCompanyName
         , @BillCompanyName
         , @SerialNum
         , @Lot
         , @CustPo
         , @Contact
         , @OfficeLongAddr
         , @BillToLongAddr
         , @ShipToLongAddr 
         , @EcCode 
         , @Origin 
         , @comm_code 
         , @Delterm 
         , @DeltermDescription
         , @URL 
         , @OfficeAddrFooter
         , @CertificateOfConformanceText
         , det_JobMatlItem
         , det_JobMatlU_M
         , det_JobMatlDesciption
         , det_TotalRequired
         , @unit_price
         , @ext_price
         , @CorpCust
		 , @CorpCustName
		 , @CountryOfOrigin
		 , @LotExpDate --AIT EWF 07/30/19
		 , @QtyBO --AIT EWF 07/30/19
		 , dbo.UomConvQty(@QtyOrdered,@UomConvFactor,'From Base')
		 , @ShipViaDescription --AIT EWF 07/30/19
		 , @CustItem --AIT EWF 07/30/19
		 , @hts_code --AIT EWF 07/30/19
	     , @sched_b_num --AIT EWF 07/30/19
	     , @eccn_usml_value --AIT EWF 07/30/19
	     , @commodity_jurisdiction --AIT EWF 07/30/19
	     , @export_compliance_program --AIT EWF 07/30/19
	     , @NaftPrefCrit --AIT EWF 07/30/19
		--<BEG AIT EWF 11/05/2019>
		, @TrackingNumber
		, @PkgWeight
		, @PltLicense
		--<END AIT EWF 11/05/2019>
		, @end_user_type --AIT EWF 11/14/2019
		, @end_user_type_description --AIT EWF 11/14/2019
		, @taken_by --AIT EWF 12/19/2019
      FROM #PickList
      TRUNCATE TABLE #PickList
    END
   END
   CLOSE ShipPackSlipCrs
   DEALLOCATE ShipPackSlipCrs
END --AIT EWF 12/10/2021

update r set total_price = @total_price
					  ,QtyBO = CASE WHEN q.QtyPicked IS NOT NULL THEN r.QtyOrdered - q.QtyPicked ELSE QtyBO END
FROM 
	@ReportSet r
	left JOIN (Select shipment_id,co_num,co_line,co_release,SUM(qty_picked) as QtyPicked FROM @ReportSet r Group by shipment_id,co_num,co_line,co_release) q on q.shipment_id = r.shipment_id
		and q.co_num = r.co_num
		and q.co_line = r.co_line
		and q.co_release = r.co_release
		and r.qty_shipped = 0

IF OBJECT_ID('tempdb..#FINAL') IS NOT NULL
    DROP TABLE #FINAL
SELECT DISTINCT r.*,@IsThaiFlag AS THALicense
       , ltrim(r.co_line) + '-' + ltrim(r.co_release) AS CoLineRelease
	   , ltrim(r.shipment_line) + '-' + ltrim(shipment_seq) AS ShipmentLineSeq
	   , (SELECT TOP 1 Destination FROM DocProfileCustomer z where z.CustNum = r.cust_num AND z.CustSeq = 0 and RptName = 'Shipment Packing Slip Report' AND Active = 1 and z.Method = 'E') as CustEmail
	   ,RTRIM(LTRIM((SELECT DISTINCT STUFF((SELECT  ', ' + RTRIM(LTRIM(ser_num)) FROM AIT_SS_BOL_SEQ_SERIAL EE 
				WHERE e.Shipment_id = ee.shipment_id AND e.shipment_line = ee.shipment_line AND e.shipment_seq = ee.shipment_seq
				ORDER BY ser_num FOR XML PATH('')), 1, 1, '') AS listStr
		FROM AIT_SS_BOL_SEQ_SERIAL E
		Where e.Shipment_id = r.shipment_id	AND e.shipment_line = r.shipment_line AND e.shipment_seq = r.shipment_seq))) as SerList, 
		b.consignee_phone
		,CAST(NULL AS NVARCHAR(30)) as SROUnit
		--<BEG AIT EWF 04/26/19>
		,co.NoteExistsFlag as OrderNotes
		,co.rowpointer as OrderRowPointer
		,coi.OrderLineRowpointer as OrderLineRowPointer
		--<END AIT EWF 04/26/19>
		,ROW_NUMBER() OVER(ORDER BY r.pack_num ASC) AS seq
into #FINAL
FROM 
	@ReportSet r
	LEFT JOIN AIT_SS_BOL_Line l on l.shipment_id = r.shipment_id
		and l.shipment_line = r.shipment_line
	LEFT JOIN AIT_SS_BOL b on b.shipment_id = r.shipment_id
	--<BEG AIT EWF 04/26/19>
	LEFT JOIN #AIT_SS_Ref co on co.co_num = l.ref_num
		and co.SS_RefType = l.ref_type
	LEFT JOIN (Select distinct x.co_num,x.co_line,x.co_release,x.ss_reftype,x.OrderLineRowpointer from #coship x) coi on coi.co_num = l.ref_num 
		and coi.co_line = l.ref_line_suf
		and coi.co_release = l.ref_release
		and coi.SS_RefType = l.ref_type
	--<END AIT EWF 04/26/19>
WHERE r.shipment_id IS NOT NULL
	
UPDATE rp
SET CoLineRelease = CAST(rp.co_line as NVARCHAR(4)) + '-' + CAST(m.sro_oper as NVARCHAR(4)) + '-' + CAST(m.trans_num as NVARCHAR(4))
	,SROUnit = sl.ser_num
FROM 
	#FINAL rp
	inner join AIT_SS_BOL_LINE l on rp.shipment_id = l.shipment_id
		and rp.shipment_line = l.shipment_line
	inner JOIN AIT_SS_PICK_LIST_REF r on r.pick_list_id = l.pick_list_id
		and l.pick_list_ref_sequence = r.sequence
	inner join fs_sro_matl m on m.rowpointer = r.SROTranRowpointer
		and r.SROShipBasis = 'M'
	left join fs_sro_line sl on sl.sro_num = l.ref_num
		AND sl.sro_line = l.ref_line_suf
where 
	l.shipment_id between @MinShipNum and @MaxShipNum

UPDATE #FINAL
SET CountryOfOrigin = (SELECT TOP 1 CharFld1 FROM AIT_CustParms where ParmId='Shipping' AND ParmKey='ERPConfig')
WHERE CountryOfOrigin IS NULL

--<BEG AIT EWF 04/26/19>
UPDATE f 
SET OrderNotes = 1
FROM #FINAL f inner join ReportNotesView v on v.RefRowPointer = f.OrderRowPointer
--<END AIT EWF 04/26/19>

--<BEG AIT EWF 08/22/2019>
QuickShip:
IF @QuickShip = 1 BEGIN
	--<BEG AIT EWF 06/23/2021>
	Declare @TotalWeight DECIMAL(20,8)
			,@TotalPkgs INT
			,@TotalValue DECIMAL(20,8)

	SELECT 
		@TotalValue = SUM(value) 
		,@QtyPackages = COUNT(*)
		,@TotalWeight = SUM(weight)
	FROM AIT_SS_QuickShipPackage p
	where p.ShipSeq = @MinShipNum
	
	IF @TotalValue is null
		SET @TotalValue = (SELECT ISNULL(MAX(q.Override_Value), SUM(i.qty*i.value)) FROM AIT_SS_QuickShipItems i inner join AIT_SS_QuickShip q on q.shipseq = i.shipseq where i.ShipSeq = @MinShipNum)

	SET @QtyPackages = ISNULL(@QtyPackages,1)
	SET @TotalWeight = ISNULL(@TotalWeight,0)

	SET @TotalPkgs = ISNULL(@TotalPkgs,1)
	SET @TotalWeight = ISNULL(@TotalWeight,0)
	--<END AIT EWF 06/23/2021>

	SELECT
		q.ShipSeq as pack_num	
		,q.PickupRequestDate as pack_date
		,w.whse as whse	
		,ISNULL(i.ref_num,'Q' + CAST(q.ShipSeq AS NVARCHAR(10))) as co_num	
		,q.CustNum as cust_num	
		,ISNULL(q.Override_Weight, @TotalWeight) as weight	--AIT EWF 06/23/2021
		,@TotalPkgs as qty_packages	--AIT EWF 06/23/2021
		,q.ShipVia as ship_code	
		,ISNULL(c.carrier_name,c.carrier_code) as carrier	
		,w.addr##1 as office_addr
		,w.addr##2 as office_addr2
		,w.addr##3 as office_addr3
		,w.addr##4 as office_addr4
		,w.city as office_city
		,w.state as office_state
		,w.zip as office_zip
		,w.country as office_country
		,w.contact as office_contact
		,w.phone as office_phone
		,q.name as ship_contact
		,q.addr##1 as ship_addr
		,q.addr##2 as ship_addr2
		,q.addr##3 as ship_addr3
		,q.addr##4 as ship_addr4
		,q.city as ship_city
		,q.state as ship_state
		,q.zip as ship_zip
		,q.country as ship_country
		,NULL as shipment_TH_fob_point	
		,NULL as shipment_TH_item_category	
		,NULL as shipment_TH_from_shipping_port	
		,NULL as shipment_TH_to_shipping_port	
		,q.name as bill_contact
		,q.addr##1 as bill_addr
		,q.addr##2 as bill_addr2
		,q.addr##3 as bill_addr3
		,q.addr##4 as bill_addr4
		,q.city as bill_city
		,q.state as bill_state
		,q.zip as bill_zip
		,q.country as bill_country
		,NULL as lcr_num	
		,NULL as credit_hold	
		,NULL as cust_po	
		,isnull(i.ref_line_suf,i.seq) as co_line	
		,1 as co_release	
		,i.item as item	
		,i.description as item_desc	
		,NULL as serial_num	
		,'EA' as u_m	
		,q.rowpointer as ship_rowpointer	--AIT EWF 06/23/2021
		,p.rowpointer as ship_package_rowpointer	--AIT EWF 06/23/2021
		,NULL as ship_seq_rowpointer	
		,@QtyUnitFormat as qty_unit_format	
		,@PlacesQtyUnit as places_qty_unit	
		,q.ShipSeq as shipment_id	
		,ISNULL(p.package_id,1) as shipment_line	--AIT EWF 06/23/2021
		,i.seq as shipment_seq	--AIT EWF 06/23/2021
		,i.Qty as qty_picked	
		,p.package_id as package_id	--AIT EWF 06/23/2021
		--<BEG AIT EWF 05/25/2022>
		, CASE WHEN @PrintShipmentSequenceText = 1 THEN dbo.ReportNotesExist('AIT_SS_QuickShip', q.RowPointer, @ShowInternal, @ShowExternal, q.NoteExistsFlag) ELSE 0 END as shipment_notes
		, 0 as shipmentSeq_notes
		, CASE WHEN @DisplayShipmentPackageNotes = 1 THEN dbo.ReportNotesExist('AIT_SS_QuickShipPackage', p.RowPointer, @ShowInternal, @ShowExternal, p.NoteExistsFlag) ELSE 0 END as shipmentPackage_notes
		--<END AIT EWF 05/25/2022>
		,NULL as shipmentPackage_TH_carton_prefix	
		,NULL as shipmentPackage_TH_measurement	
		,NULL as shipmentPackage_TH_carton_size	
		,q.ShipTo as cust_seq	
		,w.name as from_CompanyName	
		,q.name as ship_CompanyName	
		,q.name as bill_CompanyName	
		,NULL as lot	
		,w.contact as contact	
		,dbo.MultiLineAddressSp(w.name,w.addr##1,w.addr##2,w.addr##3,w.addr##4,w.city,w.state,w.zip,w.country,w.contact) as OfficeLongAddr
		,dbo.MultiLineAddressSp(q.name,q.addr##1,q.addr##2,q.addr##3,q.addr##4,q.city,q.state,q.zip,q.country,q.attn) as BillToLongAddr
		,dbo.MultiLineAddressSp(q.name,q.addr##1,q.addr##2,q.addr##3,q.addr##4,q.city,q.state,q.zip,q.country,q.attn) as ShipToLongAddr
		,NULL as ec_code	
		,NULL as origin	
		,i.comm_code as comm_code	
		,NULL as delterm	
		,NULL as delterm_description	
		,NULL as url	
		,@OfficeAddrFooter as office_addr_footer	
		,@CertfOfConfrnText as certificate_of_conformance_text	
		,NULL as Comp_Item	
		,NULL as Comp_U_M	
		,NULL as Comp_ItemDesc	
		,NULL as Comp_TotalRequired	
		,i.Value as unit_price	
		,i.Value * i.Qty as ext_price	
		,ISNULL(q.override_value, @TotalValue) as total_price	--AIT EWF 06/23/2021
		,NULL as CorpCust	
		,NULL as CorpCustName	
		,i.CountryOfOrigin as CountryOfOrigin	
		,NULL as LotExpDate	
		,NULL as QtyBO	
		,NULL as QtyOrdered
		,sc.description as ShipViaDescription	
		,NULL as CustItem	
		,NULL as THALicense	
		,NULL as CoLineRelease	
		,NULL as ShipmentLineSeq	
		,ship_to_email as CustEmail	
		,NULL as SerList	
		,q.telex_num as consignee_phone	
		,NULL as SROUnit	
		--<BEG AIT EWF 05/25/2022>
		,ISNULL(co.NoteExistsFlag, ISNULL(trn.NoteExistsFlag, ISNULL(sro.NoteExistsFlag, po.NoteExistsFlag))) as OrderNotes
		,ISNULL(co.rowpointer, ISNULL(trn.rowpointer, ISNULL(sro.rowpointer, po.rowpointer))) as OrderRowPointer
		,ISNULL(coi.rowpointer, ISNULL(tri.rowpointer, ISNULL(srl.rowpointer, poi.rowpointer))) as OrderLineRowPointer
		--<END AIT EWF 05/25/2022>
		,ISNULL(p.package_id,0) + i.Seq as seq --AIT EWF 06/23/2021
		,ISNULL(ic.hts_code, i.hts_code) --AIT BTS 12282023
		,i.comm_code
		,ISNULL(ic.sched_b_num, i.sched_b_num) --AIT BTS 12282023
		,i.eccnusmlvalue as eccn_usml_value
		,i.CommodityJurisdiction as commodity_jurisdiction
		,i.ExportComplianceProgram as export_compliance_program
	FROM
		AIT_SS_QuickShip q
		left join AIT_SS_QuickShipPackage p on p.shipseq = q.shipseq --AIT EWF 06/23/2021
		LEFT JOIN AIT_SS_QuickShipItems i on i.ShipSeq = q.ShipSeq and (i.package_id = p.package_id OR (i.package_id is null AND p.package_id is null)) --AIT EWF 06/23/2021
		LEFT JOIN whse w on w.whse = q.whse
		LEFT JOIN AITShipViaMap m on m.ShipCode = q.ShipVia
		LEFT JOIN carrier c on c.carrier_code = m.Carrier
		LEFT JOIN shipcode sc on sc.ship_code = q.ShipVia
		
		--<BEG AIT EWF 05/25/2022>
		LEFT JOIN co on co.co_num = i.ref_num and i.ref_type = 'O'
		LEFT JOIN transfer trn on trn.trn_num = i.ref_num and i.ref_type = 'T'
		LEFT JOIN fs_sro sro on sro.sro_num = i.ref_num and i.ref_type = 'S'
		LEFT JOIN po on po.po_num = i.ref_num and i.ref_type = 'P'
		
		LEFT JOIN coitem coi on coi.co_num = i.ref_num and coi.co_line = i.ref_line_suf and i.ref_type = 'O'
		LEFT JOIN trnitem tri on tri.trn_num = i.ref_num and tri.trn_line = i.ref_line_suf and i.ref_type = 'T'
		LEFT JOIN fs_sro_line srl on srl.sro_num = i.ref_num and srl.sro_line = i.ref_line_suf and i.ref_type = 'S'
		LEFT JOIN poitem poi on poi.po_num = i.ref_num and poi.po_line = i.ref_line_suf and i.ref_type = 'P'
		--<END AIT EWF 05/25/2022>
		LEFT JOIN ue_ss_itemcountry ic on ic.item = i.item AND ic.country = q.country --AIT BTS 12282023
	WHERE q.ShipSeq BETWEEN @MinShipNum AND @MaxShipNum
END
ELSE
--<END AIT EWF 08/22/2019>
--<BEG AIT EWF 09/22/2020>
IF @IsConsolidatedDoc = 1 BEGIN
	Declare @Value DECIMAL(20,8)
			,@BOLList NVARCHAR(4000)

	IF OBJECT_ID('tempdb..#tmp') IS NOT NULL DROP TABLE #tmp
	IF OBJECT_ID('tempdb..#ConsOutput') IS NOT NULL DROP TABLE #ConsOutput
	SELECT DISTINCT f.pack_num,f.pack_date,f.qty_packages,f.PkgWeight,f.ext_price,f.TrackingNumber,p.package_id,p.weight
	INTO #tmp 
	FROM #FINAL f left join AIT_SS_BOL_package p on p.shipment_id = f.pack_num

	SELECT @QtyPackages = COUNT(*),@Weight = SUM(weight) FROM (SELECT DISTINCT pack_num,package_id,weight from #tmp) q
	SELECT @Value = SUM(ext_price) FROM #FINAL

	SELECT @BOLList = STUFF(( SELECT DISTINCT ', ' + CAST(pack_num AS NVARCHAR(100))
	FROM #FINAL
	ORDER BY ', ' + CAST(pack_num AS NVARCHAR(100))
	FOR XML PATH('')), 1, 2, '')

	UPDATE #FINAL
	SET pack_date = q.pack_date
		,TrackingNumber = q.TrackingNumber
	FROM
		(
			SELECT
				MAX(pack_date) as pack_date
				,MAX(TrackingNumber) as TrackingNumber
			FROM
				#FINAL
		) q

	SELECT 
		@BOLList as pack_num
		,pack_date
		,whse
		,co_num
		,cust_num
		,@Weight as weight
		,@QtyPackages as qty_packages
		,ship_code
		,carrier
		,office_addr
		,office_addr2
		,office_addr3
		,office_addr4
		,office_city
		,office_state
		,office_zip
		,office_country
		,office_contact
		,office_phone
		,ship_contact
		,ship_addr
		,ship_addr2
		,ship_addr3
		,ship_addr4
		,ship_city
		,ship_state
		,ship_zip
		,ship_country
		,shipment_TH_fob_point
		,shipment_TH_item_category
		,shipment_TH_from_shipping_port
		,shipment_TH_to_shipping_port
		,bill_contact
		,bill_addr
		,bill_addr2
		,bill_addr3
		,bill_addr4
		,bill_city
		,bill_state
		,bill_zip
		,bill_country
		,lcr_num
		,credit_hold
		,cust_po
		,co_line
		,co_release
		,item
		,item_desc
		,serial_num
		,u_m
		,ship_rowpointer
		,ship_package_rowpointer
		,ship_seq_rowpointer
		,qty_unit_format
		,places_qty_unit
		,@BOLList as shipment_id
		,shipment_line
		,shipment_seq
		,qty_picked
		,package_id
		,shipment_notes
		,shipmentSeq_notes
		,shipmentPackage_notes
		,shipmentPackage_TH_carton_prefix
		,shipmentPackage_TH_measurement
		,shipmentPackage_TH_carton_size
		,cust_seq
		,from_CompanyName
		,ship_CompanyName
		,bill_CompanyName
		,lot
		,contact
		,OfficeLongAddr
		,BillToLongAddr
		,ShipToLongAddr
		,ec_code
		,origin
		,comm_code
		,delterm
		,delterm_description
		,url
		,office_addr_footer
		,certificate_of_conformance_text
		,Comp_Item
		,Comp_U_M
		,Comp_ItemDesc
		,Comp_TotalRequired
		,unit_price
		,ext_price
		,@Value
		,CorpCust
		,CorpCustName
		,CountryOfOrigin
		,LotExpDate
		,QtyBO
		,QtyOrdered
		,ShipViaDescription
		,CustItem
		,hts_code
		,sched_b_num
		,eccn_usml_value
		,commodity_jurisdiction
		,export_compliance_program
		,TrackingNumber
		,PkgWeight
		,PltLicense
		,end_user_type
		,end_user_type_description
		,taken_by
		,NaftPrefCrit
		,THALicense
		,CoLineRelease
		,ShipmentLineSeq
		,CustEmail
		,SerList
		,consignee_phone
		,SROUnit
		,OrderNotes
		,OrderRowPointer
		,OrderLineRowPointer
		,seq
	FROM
		#FINAL
END
--<END AIT EWF 09/22/2020>
ELSE
	SELECT * FROM #FINAL

IF @QuickShip = 0
	UPDATE AIT_SS_BOL SET pack_slip_printed = 1
	WHERE shipment_id IN (SELECT pack_num FROM @ReportSet) AND pack_slip_printed = 0

ENDPROG:
EXEC dbo.CloseSessionContextSp @SessionID = @RptSessionID
GO


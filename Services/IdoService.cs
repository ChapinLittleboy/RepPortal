using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using RepPortal.Data;
using RepPortal.Models;

namespace RepPortal.Services;

public class IdoService : IIdoService
{
    private readonly ICsiRestClient _csiRestClient;
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly ILogger<IdoService> _logger;
    private readonly CsiOptions _csiOptions;

    public IdoService(
        ICsiRestClient csiRestClient,
        IDbConnectionFactory dbConnectionFactory,
        ILogger<IdoService> logger,
        IOptions<CsiOptions> csiOptions)
    {
        _csiRestClient = csiRestClient;
        _dbConnectionFactory = dbConnectionFactory;
        _logger = logger;
        _csiOptions = csiOptions.Value;
    }

    public async Task<List<OrderDetail>> GetAllOpenOrderDetailsAsync(string repCode, List<string> salesRegions)
    {
        var filters = new List<string>
        {
            "QtyOrdered > QtyShipped",
            Eq("CoSlsman", repCode)
        };

        if (_csiOptions.OpenOrderCutoffDate.HasValue)
            filters.Add(DateGt("DerDueDate", _csiOptions.OpenOrderCutoffDate.Value));

        if (salesRegions is { Count: > 0 })
            filters.Add(In("SalesRegion", salesRegions));

        var query = new Dictionary<string, string>
        {
            ["props"] = "CoCustNum,Adr0Name,CoOrderDate,CoCustPo,CoNum,DerDueDate,SalesRegion,Item,Description,Price,QtyOrdered,QtyShipped,AdrName,CoCustSeq",
            ["filter"] = string.Join(" AND ", filters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var response = Deserialize(await _csiRestClient.GetAsync("json/SLCoitems/adv", query));
        if (response.MessageCode != 0)
            throw new InvalidOperationException(response.Message);

        return response.Items.Select(MapRow<OrderDetail>).ToList();
    }

    public async Task<List<CustomerShipment>> GetShipmentsDataAsync(
        SalesService.ShipmentsParameters parameters,
        string repCode,
        IEnumerable<string>? allowedRegions)
    {
        HashSet<string>? allowedCustKeys = null;
        Dictionary<string, string>? custRegionLookup = null;

        if (allowedRegions != null && allowedRegions.Any())
        {
            var custQuery = new Dictionary<string, string>
            {
                ["props"] = "CustNum,CustSeq,Uf_SalesRegion",
                ["filter"] = In("Uf_SalesRegion", allowedRegions),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var custResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCustomers/adv", custQuery));

            if (custResponse.MessageCode == 0)
            {
                allowedCustKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                custRegionLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in custResponse.Items)
                {
                    var cn = GetCell(row, "CustNum")?.Trim();
                    var cs = GetCell(row, "CustSeq")?.Trim() ?? "0";
                    var region = GetCell(row, "Uf_SalesRegion")?.Trim();

                    if (string.IsNullOrWhiteSpace(cn))
                        continue;

                    var key = $"{cn}|{cs}";
                    allowedCustKeys.Add(key);
                    if (!string.IsNullOrWhiteSpace(region))
                        custRegionLookup[key] = region;
                }
            }
        }

        var slFilters = new List<string> { Eq("CoSlsman", repCode) };
        if (parameters.BeginShipDate != default)
            slFilters.Add($"ShipDate >= '{parameters.BeginShipDate:yyyyMMdd}'");
        if (parameters.EndShipDate != default)
            slFilters.Add($"ShipDate <= '{parameters.EndShipDate:yyyyMMdd}'");

        var slQuery = new Dictionary<string, string>
        {
            ["props"] = "CoCustNum,CoCustSeq,CadrName,CoCustPo,CoNum,CoLine,CoiItem,CoiDescription,CoiDueDate,ShipDate,QtyShipped,DerNetPrice,BolNumber",
            ["filter"] = string.Join(" AND ", slFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var slResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCoShips/adv", slQuery));

        if (slResponse.MessageCode != 0)
            throw new InvalidOperationException(slResponse.Message);

        var filteredItems = slResponse.Items;
        if (allowedCustKeys != null && allowedCustKeys.Count > 0)
        {
            filteredItems = slResponse.Items.Where(row =>
            {
                var cn = GetCell(row, "CoCustNum")?.Trim();
                var cs = GetCell(row, "CoCustSeq")?.Trim() ?? "0";
                return !string.IsNullOrWhiteSpace(cn) && allowedCustKeys.Contains($"{cn}|{cs}");
            }).ToList();
        }

        var shipments = filteredItems.Select(MapRow<CustomerShipment>).ToList();

        if (custRegionLookup != null && custRegionLookup.Count > 0)
        {
            for (int i = 0; i < filteredItems.Count && i < shipments.Count; i++)
            {
                var cn = GetCell(filteredItems[i], "CoCustNum")?.Trim();
                var cs = GetCell(filteredItems[i], "CoCustSeq")?.Trim() ?? "0";
                if (!string.IsNullOrWhiteSpace(cn) && custRegionLookup.TryGetValue($"{cn}|{cs}", out string? region))
                    shipments[i].ShipToRegion = region;
            }
        }

        var bolNumbers = shipments
            .Where(s => s.BolNumber.HasValue && s.BolNumber.Value != 0)
            .Select(s => s.BolNumber!.Value)
            .Distinct()
            .ToList();

        if (bolNumbers.Count == 0)
            return shipments;

        var bolQuery = new Dictionary<string, string>
        {
            ["props"] = "ShipmentId,InvoiceeState,ConsigneeState,Whse,CarrierCode,ShipCode,ShipCodeDesc,ShipDate,BillTransportationTo,TrackingNumber,InvoiceeName,CustNum,CustSeq",
            ["filter"] = In("ShipmentId", bolNumbers.Select(n => n.ToString(CultureInfo.InvariantCulture))),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var bolResponse = Deserialize(await _csiRestClient.GetAsync("json/ait_ss_bols/adv", bolQuery));

        if (bolResponse.MessageCode != 0)
            throw new InvalidOperationException(bolResponse.Message);

        var bolsByShipmentId = bolResponse.Items
            .Select(MapRow<BolInfo>)
            .Where(b => b.ShipmentId.HasValue)
            .GroupBy(b => b.ShipmentId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        try
        {
            foreach (var shipment in shipments)
            {
                if (!shipment.BolNumber.HasValue
                    || !bolsByShipmentId.TryGetValue(shipment.BolNumber.Value, out BolInfo? bol))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(bol.InvoiceeState))
                    shipment.BillToState = bol.InvoiceeState;
                if (!string.IsNullOrWhiteSpace(bol.ConsigneeState))
                    shipment.ShipToState = bol.ConsigneeState;
                if (!string.IsNullOrWhiteSpace(bol.Whse))
                    shipment.Whse = bol.Whse;
                if (!string.IsNullOrWhiteSpace(bol.CarrierCode))
                    shipment.CarrierCode = bol.CarrierCode;
                if (!string.IsNullOrWhiteSpace(bol.ShipCode))
                    shipment.ShipCode = bol.ShipCode;
                else if (!string.IsNullOrWhiteSpace(bol.ShipCodeDesc))
                    shipment.ShipCode = bol.ShipCodeDesc;
                if (!string.IsNullOrWhiteSpace(bol.BillTransportationTo))
                    shipment.FreightTerms = bol.BillTransportationTo;
                if (!string.IsNullOrWhiteSpace(bol.TrackingNumber))
                    shipment.TrackingNumber = bol.TrackingNumber;
            }

            return shipments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping shipment data");
            return new List<CustomerShipment>();
        }
    }

    public async Task<List<InvoiceRptDetail>> GetInvoiceRptDataAsync(
        SalesService.InvoiceRptParameters parameters,
        string repCode)
    {
        var hdrFilters = new List<string>
        {
            Eq("Slsman", repCode),
            $"InvDate >= '{parameters.BeginInvoiceDate:yyyyMMdd}'",
            $"InvDate <= '{parameters.EndInvoiceDate:yyyyMMdd}'"
        };
        if (!string.IsNullOrWhiteSpace(parameters.CustNum))
            hdrFilters.Add(Eq("CustNum", parameters.CustNum));

        var hdrQuery = new Dictionary<string, string>
        {
            ["props"] = "InvNum,InvSeq,CustNum,CustSeq,AddrName,State,ShipDate,CustPo,InvDate",
            ["filter"] = string.Join(" AND ", hdrFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var hdrResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery));
        if (hdrResponse.MessageCode != 0)
            throw new InvalidOperationException(hdrResponse.Message);

        var headers = hdrResponse.Items.Select(MapRow<InvHdrInfo>).ToList();
        if (headers.Count == 0)
            return new List<InvoiceRptDetail>();

        var hdrLookup = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .GroupBy(h => (h.InvNum!, h.InvSeq))
            .ToDictionary(g => g.Key, g => g.First());

        var invNums = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .Select(h => h.InvNum!)
            .Distinct()
            .ToList();

        var itemQuery = new Dictionary<string, string>
        {
            ["props"] = "InvNum,InvSeq,Item,QtyInvoiced,Price,CoNum,CoLine,SiteRef",
            ["filter"] = In("InvNum", invNums),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var itemResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery));
        if (itemResponse.MessageCode != 0)
            throw new InvalidOperationException(itemResponse.Message);

        var invoiceItems = itemResponse.Items.Select(MapRow<InvoiceRptDetail>).ToList();

        foreach (var indexedItem in invoiceItems.Select((item, index) => (item, index)))
        {
            var invSeqRaw = GetCell(itemResponse.Items[indexedItem.index], "InvSeq");
            int.TryParse(invSeqRaw, out int invSeq);

            var key = (indexedItem.item.InvNum ?? "", invSeq);
            if (!hdrLookup.TryGetValue(key, out InvHdrInfo? hdr))
                continue;

            indexedItem.item.Cust = hdr.CustNum ?? "";
            indexedItem.item.CustSeq = hdr.CustSeq;
            indexedItem.item.Name = hdr.AddrName ?? "";
            indexedItem.item.State = hdr.State ?? "";
            indexedItem.item.Ship_Date = hdr.ShipDate;
            indexedItem.item.CustPO = hdr.CustPo ?? "";
            indexedItem.item.InvDate = hdr.InvDate ?? DateTime.MinValue;
        }

        var coNums = invoiceItems
            .Where(i => !string.IsNullOrWhiteSpace(i.CoNum))
            .Select(i => i.CoNum!)
            .Distinct()
            .ToList();

        if (coNums.Count > 0)
        {
            var coQuery = new Dictionary<string, string>
            {
                ["props"] = "CoNum,CoLine,Adr0Name,DueDate,CoOrderDate",
                ["filter"] = In("CoNum", coNums),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var coResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCoitems/adv", coQuery));

            if (coResponse.MessageCode == 0)
            {
                var coLookup = coResponse.Items
                    .Select(MapRow<CoItemInfo>)
                    .Where(c => !string.IsNullOrWhiteSpace(c.CoNum))
                    .GroupBy(c => (c.CoNum!, c.CoLine))
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var indexedItem in invoiceItems.Select((item, index) => (item, index)))
                {
                    if (string.IsNullOrWhiteSpace(indexedItem.item.CoNum))
                        continue;

                    var coLineRaw = GetCell(itemResponse.Items[indexedItem.index], "CoLine");
                    int.TryParse(coLineRaw, out int coLine);

                    var coKey = (indexedItem.item.CoNum!, coLine);
                    if (!coLookup.TryGetValue(coKey, out CoItemInfo? co))
                        continue;

                    indexedItem.item.B2Name = co.Adr0Name ?? "";
                    indexedItem.item.DueDate = co.DueDate;
                    indexedItem.item.OrdDate = co.CoOrderDate;
                }
            }
        }

        foreach (var item in invoiceItems)
        {
            item.ExtPrice = item.InvQty * item.Price;
            item.Slsman = repCode;
        }

        IEnumerable<InvoiceRptDetail> results = invoiceItems;
        if (!string.IsNullOrWhiteSpace(parameters.CoNum))
        {
            var match = parameters.CoNum.Trim().ToUpperInvariant();
            results = results.Where(r => !string.IsNullOrWhiteSpace(r.CoNum) &&
                                         r.CoNum.Trim().ToUpperInvariant() == match);
        }

        return results.ToList();
    }

    public async Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQtyAsync(
        string repCode,
        List<string> allowedRegions)
    {
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyCurrentEnd = new DateTime(fiscalYear, 8, 31);
        var fyMinus3Start = new DateTime(fiscalYear - 4, 9, 1);
        var fyMinus3End = new DateTime(fiscalYear - 3, 8, 31);
        var fyMinus2Start = new DateTime(fiscalYear - 3, 9, 1);
        var fyMinus2End = new DateTime(fiscalYear - 2, 8, 31);
        var fyMinus1Start = new DateTime(fiscalYear - 2, 9, 1);
        var fyMinus1End = new DateTime(fiscalYear - 1, 8, 31);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var currentFYMonths = Enumerable.Range(0, currentFiscalMonth)
            .Select(i => fyCurrentStart.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var filters = new List<string>
        {
            Eq("Slsman", repCode),
            $"InvDate >= '{fyMinus3Start:yyyyMMdd}'",
            $"InvDate <= '{fyCurrentEnd:yyyyMMdd}'"
        };

        if (allowedRegions is { Count: > 0 })
            filters.Add(In("SalesRegion", allowedRegions));

        var query = new Dictionary<string, string>
        {
            ["props"] = "InvDate,CustNum,CustSeq,ShipToCity,ShipToState,BillToState,Slsman,SalesRegion,RegionName,item,qty_invoiced,price,disc,Period",
            ["filter"] = string.Join(" AND ", filters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var siteAuths = new List<(string Site, string? AuthOverride)> { ("BAT", null) };
        if (!string.IsNullOrWhiteSpace(_csiOptions.KentAuthorization))
            siteAuths.Add(("KENT", _csiOptions.KentAuthorization));

        var lines = new List<InvLineRawRow>();

        foreach (var (site, authOverride) in siteAuths)
        {
            string siteJson = authOverride != null
                ? await _csiRestClient.GetAsync("json/Chap_InvoiceLines/adv", query, authOverride)
                : await _csiRestClient.GetAsync("json/Chap_InvoiceLines/adv", query);

            var siteResponse = Deserialize(siteJson);

            if (siteResponse.MessageCode == 0)
            {
                lines.AddRange(siteResponse.Items
                    .Select(MapRow<InvLineRawRow>)
                    .Where(l => l.InvDate.HasValue));
            }
            else if (authOverride != null)
            {
                _logger.LogInformation(
                    "Chap_InvoiceLines not available on {Site} ({Msg}); falling back to SLInvHdrs + SLInvItemAlls",
                    site, siteResponse.Message);
                lines.AddRange(await FetchKentLinesViaStandardIdosAsync(repCode, fyMinus3Start, fyCurrentEnd, authOverride));
            }
            else
            {
                _logger.LogWarning("Chap_InvoiceLines failed for {Site}: {Msg}", site, siteResponse.Message);
            }
        }

        _logger.LogInformation(
            "GetItemSalesReportDataWithQtyAsync: {Count} raw lines fetched for rep {RepCode} (BAT + KENT)",
            lines.Count, repCode);

        var uniqueCustNums = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.CustNum))
            .Select(l => l.CustNum!)
            .Distinct()
            .ToList();

        var custNameLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var regionFromCustLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (uniqueCustNums.Count > 0)
        {
            var custQuery = new Dictionary<string, string>
            {
                ["props"] = "CustNum,CustSeq,Name,Uf_SalesRegion",
                ["filter"] = $"CustSeq = 0 AND {In("CustNum", uniqueCustNums)}",
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var custResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCustomers/adv", custQuery));

            if (custResponse.MessageCode == 0)
            {
                foreach (var custRow in custResponse.Items.Select(MapRow<CustNameInfo>))
                {
                    if (string.IsNullOrWhiteSpace(custRow.CustNum))
                        continue;

                    custNameLookup[custRow.CustNum] = custRow.Name ?? "";
                    if (!string.IsNullOrWhiteSpace(custRow.UfSalesRegion))
                        regionFromCustLookup[custRow.CustNum] = custRow.UfSalesRegion;
                }
            }
            else
            {
                _logger.LogWarning("SLCustomers lookup failed: {Msg}", custResponse.Message);
            }
        }

        var uniqueItems = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Item))
            .Select(l => l.Item!)
            .Distinct()
            .ToList();

        var itemDescLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (uniqueItems.Count > 0)
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"] = "Item,Description",
                ["filter"] = In("Item", uniqueItems),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemResponse = Deserialize(await _csiRestClient.GetAsync("json/SLItems/adv", itemQuery));

            if (itemResponse.MessageCode == 0)
            {
                foreach (var itemRow in itemResponse.Items.Select(MapRow<ItemDescInfo>))
                {
                    if (!string.IsNullOrWhiteSpace(itemRow.Item))
                        itemDescLookup[itemRow.Item] = itemRow.Description ?? "";
                }
            }
            else
            {
                _logger.LogWarning("SLItems lookup failed: {Msg}", itemResponse.Message);
            }
        }

        foreach (var line in lines.Where(l => string.IsNullOrEmpty(l.SalesRegion) && !string.IsNullOrWhiteSpace(l.CustNum)))
        {
            if (regionFromCustLookup.TryGetValue(line.CustNum!, out string? region))
                line.SalesRegion = region;
        }

        if (allowedRegions is { Count: > 0 })
        {
            lines = lines
                .Where(l => !string.IsNullOrEmpty(l.SalesRegion) && allowedRegions.Contains(l.SalesRegion))
                .ToList();
        }

        string GetFyLabel(DateTime d)
        {
            if (d >= fyMinus3Start && d <= fyMinus3End) return $"FY{fiscalYear - 3}";
            if (d >= fyMinus2Start && d <= fyMinus2End) return $"FY{fiscalYear - 2}";
            if (d >= fyMinus1Start && d <= fyMinus1End) return $"FY{fiscalYear - 1}";
            if (d >= fyCurrentStart && d <= fyCurrentEnd) return $"FY{fiscalYear}";
            return string.Empty;
        }

        var grouped = lines
            .GroupBy(l => (CustNum: l.CustNum ?? "", CustSeq: l.CustSeq, Item: l.Item ?? ""))
            .ToList();

        var result = new List<Dictionary<string, object>>(grouped.Count);

        foreach (var group in grouped)
        {
            var first = group.First();
            custNameLookup.TryGetValue(first.CustNum ?? "", out string? custName);
            itemDescLookup.TryGetValue(first.Item ?? "", out string? itemDesc);

            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customer"] = first.CustNum ?? "",
                ["Customer Name"] = custName ?? "",
                ["Ship To Num"] = first.CustSeq,
                ["Ship To City"] = first.ShipToCity ?? "",
                ["Ship To State"] = first.ShipToState ?? "",
                ["slsman"] = first.Slsman ?? "",
                ["name"] = custName ?? "",
                ["Bill To State"] = first.BillToState ?? "",
                ["Uf_SalesRegion"] = first.SalesRegion ?? "",
                ["RegionName"] = first.RegionName ?? "",
                ["Item"] = first.Item ?? "",
                ["ItemDescription"] = itemDesc ?? ""
            };

            foreach (int offset in new[] { 3, 2, 1, 0 })
            {
                string fyLabel = $"FY{fiscalYear - offset}";
                var fyLines = group.Where(l => GetFyLabel(l.InvDate!.Value) == fyLabel).ToList();
                row[$"{fyLabel}_Rev"] = fyLines.Sum(l => l.NetRevenue);
                row[$"{fyLabel}_Qty"] = fyLines.Sum(l => l.QtyInvoiced);
            }

            foreach (var monthKey in currentFYMonths)
            {
                var monthLines = group
                    .Where(l => string.Equals(l.Period, monthKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                row[$"{monthKey}_Rev"] = monthLines.Sum(l => l.NetRevenue);
                row[$"{monthKey}_Qty"] = monthLines.Sum(l => l.QtyInvoiced);
            }

            result.Add(row);
        }

        return result;
    }

    public async Task<List<Dictionary<string, object>>> GetSalesReportDataUsingInvRepAsync(
        string repCode,
        IEnumerable<string>? allowedRegions)
    {
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;
        var fyMinus3Start = new DateTime(fiscalYear - 4, 9, 1);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var allMonths = Enumerable.Range(0, 36 + currentFiscalMonth)
            .Select(i => fyMinus3Start.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var fyMinus3Months = allMonths.Take(12).ToList();
        var fyMinus2Months = allMonths.Skip(12).Take(12).ToList();
        var fyMinus1Months = allMonths.Skip(24).Take(12).ToList();
        var currentFYMonths = allMonths.Skip(36).Take(currentFiscalMonth).ToList();

        var hdrQuery = new Dictionary<string, string>
        {
            ["props"] = "InvNum,InvSeq,CustNum,CustSeq,Slsman,InvDate,AddrName,State",
            ["filter"] = $"{Eq("Slsman", repCode)} AND InvDate >= '{fyMinus3Start:yyyyMMdd}'",
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var hdrResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery));
        if (hdrResponse.MessageCode != 0)
            throw new InvalidOperationException(hdrResponse.Message);

        var headers = hdrResponse.Items.Select(MapRow<InvHdrInfo>).ToList();
        if (headers.Count == 0)
            return new List<Dictionary<string, object>>();

        var invNums = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .Select(h => h.InvNum!)
            .Distinct()
            .ToList();

        var invoiceItems = new List<InvItemInfo>();
        foreach (var batch in invNums.Chunk(30))
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"] = "InvNum,InvSeq,QtyInvoiced,Price",
                ["filter"] = In("InvNum", batch),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery));
            if (itemResponse.MessageCode != 0)
                throw new InvalidOperationException(itemResponse.Message);

            invoiceItems.AddRange(itemResponse.Items.Select(MapRow<InvItemInfo>));
        }

        var custNums = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.CustNum))
            .Select(h => h.CustNum!)
            .Distinct()
            .ToList();

        var addrQuery = new Dictionary<string, string>
        {
            ["props"] = "CustNum,CustSeq,Name,City,State",
            ["filter"] = In("CustNum", custNums),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var addrResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCustAddrs/adv", addrQuery));
        var custAddrs = addrResponse.MessageCode == 0
            ? addrResponse.Items.Select(MapRow<CustAddrInfo>).ToList()
            : new List<CustAddrInfo>();

        var custAddrLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum))
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First());

        var billToLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum) && c.CustSeq == 0)
            .GroupBy(c => c.CustNum!)
            .ToDictionary(g => g.Key, g => g.First());

        var custRegionQuery = new Dictionary<string, string>
        {
            ["props"] = "CustNum,CustSeq,Uf_SalesRegion",
            ["filter"] = In("CustNum", custNums),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var custRegionResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCustomers/adv", custRegionQuery));
        var regionLookup = new Dictionary<(string, int), string>();
        if (custRegionResponse.MessageCode == 0)
        {
            foreach (var row in custRegionResponse.Items)
            {
                var cn = GetCell(row, "CustNum")?.Trim();
                var cs = GetCell(row, "CustSeq")?.Trim() ?? "0";
                var region = GetCell(row, "Uf_SalesRegion")?.Trim();
                if (!string.IsNullOrWhiteSpace(cn) && int.TryParse(cs, out int custSeq))
                    regionLookup[(cn, custSeq)] = region ?? "";
            }
        }

        using var connection = _dbConnectionFactory.CreateBatConnection();
        var regionRows = await connection.QueryAsync<(string Region, string RegionName)>(
            "SELECT Region, RegionName FROM Chap_RegionNames WITH (NOLOCK)");
        var regionNameLookup = regionRows.ToDictionary(
            r => r.Region ?? "",
            r => r.RegionName ?? "",
            StringComparer.OrdinalIgnoreCase);

        HashSet<string>? allowedCustKeys = null;
        if (allowedRegions != null && allowedRegions.Any())
        {
            allowedCustKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in regionLookup)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value) && allowedRegions.Contains(kvp.Value))
                    allowedCustKeys.Add($"{kvp.Key.Item1}|{kvp.Key.Item2}");
            }
        }

        var hdrLookup = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .GroupBy(h => (h.InvNum!, h.InvSeq))
            .ToDictionary(g => g.Key, g => g.First());

        var joined = new List<(string Customer, string CustomerName, int ShipToNum,
            string ShipToCity, string ShipToState, string Slsman, string Name,
            string BillToState, string UfSalesRegion, string RegionName, string Period,
            decimal ExtPrice)>();

        foreach (var item in invoiceItems)
        {
            if (string.IsNullOrWhiteSpace(item.InvNum))
                continue;

            var key = (item.InvNum!, item.InvSeq);
            if (!hdrLookup.TryGetValue(key, out InvHdrInfo? hdr))
                continue;

            if (hdr.InvDate == null || string.IsNullOrWhiteSpace(hdr.CustNum))
                continue;

            if (allowedCustKeys != null && !allowedCustKeys.Contains($"{hdr.CustNum}|{hdr.CustSeq}"))
                continue;

            var custAddrKey = (hdr.CustNum!, hdr.CustSeq);
            custAddrLookup.TryGetValue(custAddrKey, out CustAddrInfo? shipToCust);
            billToLookup.TryGetValue(hdr.CustNum!, out CustAddrInfo? billToCust);

            regionLookup.TryGetValue(custAddrKey, out string? ufSalesRegion);
            ufSalesRegion ??= "";
            string regionName = !string.IsNullOrWhiteSpace(ufSalesRegion) &&
                                regionNameLookup.TryGetValue(ufSalesRegion, out string? rn)
                ? rn
                : "";

            string period = hdr.InvDate.Value.ToString("MMM") + hdr.InvDate.Value.Year;
            decimal extPrice = item.QtyInvoiced * item.Price;

            joined.Add((
                Customer: hdr.CustNum!,
                CustomerName: billToCust?.Name ?? hdr.AddrName ?? "",
                ShipToNum: hdr.CustSeq,
                ShipToCity: shipToCust?.City ?? "",
                ShipToState: hdr.State ?? shipToCust?.State ?? "",
                Slsman: hdr.Slsman ?? repCode,
                Name: billToCust?.Name ?? hdr.AddrName ?? "",
                BillToState: billToCust?.State ?? "",
                UfSalesRegion: ufSalesRegion,
                RegionName: regionName,
                Period: period,
                ExtPrice: extPrice
            ));
        }

        return BuildSalesPivotResults(joined, fiscalYear, fyMinus3Months, fyMinus2Months, fyMinus1Months, currentFYMonths);
    }

    public async Task<List<Dictionary<string, object>>> GetSalesReportDataAsync(
        string repCode,
        IEnumerable<string>? allowedRegions)
    {
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;
        var fyMinus3Start = new DateTime(fiscalYear - 4, 9, 1);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var allMonths = Enumerable.Range(0, 36 + currentFiscalMonth)
            .Select(i => fyMinus3Start.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var fyMinus3Months = allMonths.Take(12).ToList();
        var fyMinus2Months = allMonths.Skip(12).Take(12).ToList();
        var fyMinus1Months = allMonths.Skip(24).Take(12).ToList();
        var currentFYMonths = allMonths.Skip(36).Take(currentFiscalMonth).ToList();

        var custQuery = new Dictionary<string, string>
        {
            ["props"] = "CustNum,CustSeq,Slsman,Uf_SalesRegion",
            ["filter"] = Eq("Slsman", repCode),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var custResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCustomers/adv", custQuery));
        if (custResponse.MessageCode != 0)
            throw new InvalidOperationException(custResponse.Message);

        var customers = custResponse.Items.Select(row =>
        {
            var cn = GetCell(row, "CustNum");
            var cs = GetCell(row, "CustSeq")?.Trim() ?? "0";
            var slsman = GetCell(row, "Slsman")?.Trim();
            var region = GetCell(row, "Uf_SalesRegion")?.Trim();
            int.TryParse(cs, out int custSeq);
            return new { CustNum = cn, CustSeq = custSeq, Slsman = slsman ?? repCode, UfSalesRegion = region ?? "" };
        })
        .Where(c => !string.IsNullOrWhiteSpace(c.CustNum))
        .ToList();

        if (customers.Count == 0)
            return new List<Dictionary<string, object>>();

        var custSlsmanLookup = customers
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First().Slsman);

        var regionLookup = customers
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First().UfSalesRegion);

        HashSet<string>? allowedCustKeys = null;
        if (allowedRegions != null && allowedRegions.Any())
        {
            allowedCustKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in customers)
            {
                if (!string.IsNullOrWhiteSpace(c.UfSalesRegion) && allowedRegions.Contains(c.UfSalesRegion))
                    allowedCustKeys.Add($"{c.CustNum}|{c.CustSeq}");
            }
        }

        var custNums = customers.Select(c => c.CustNum!).Distinct().ToList();
        const int custBatchSize = 30;

        var siteAuths = new List<(string Site, string? AuthOverride)> { ("BAT", null) };
        if (!string.IsNullOrWhiteSpace(_csiOptions.KentAuthorization))
            siteAuths.Add(("KENT", _csiOptions.KentAuthorization));

        var headers = new List<(string Site, InvHdrInfo Hdr)>();
        foreach (var (site, authOverride) in siteAuths)
        {
            foreach (var batch in custNums.Chunk(custBatchSize))
            {
                var hdrQuery = new Dictionary<string, string>
                {
                    ["props"] = "InvNum,InvSeq,CustNum,CustSeq,InvDate,AddrName,State",
                    ["filter"] = In("CustNum", batch) + $" AND InvDate >= '{fyMinus3Start:yyyyMMdd}'",
                    ["rowcap"] = "0",
                    ["loadtype"] = "FIRST",
                    ["bookmark"] = "0",
                    ["readonly"] = "1"
                };

                string hdrJson = authOverride != null
                    ? await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery, authOverride)
                    : await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery);

                var hdrResponse = Deserialize(hdrJson);
                if (hdrResponse.MessageCode != 0)
                    throw new InvalidOperationException(hdrResponse.Message);

                headers.AddRange(hdrResponse.Items.Select(row => (site, MapRow<InvHdrInfo>(row))));
            }
        }

        if (headers.Count == 0)
            return new List<Dictionary<string, object>>();

        var invoiceItems = new List<(string Site, InvItemInfo Item)>();
        foreach (var (site, authOverride) in siteAuths)
        {
            var siteInvNums = headers
                .Where(h => h.Site == site && !string.IsNullOrWhiteSpace(h.Hdr.InvNum))
                .Select(h => h.Hdr.InvNum!)
                .Distinct()
                .ToList();

            foreach (var batch in siteInvNums.Chunk(30))
            {
                var itemQuery = new Dictionary<string, string>
                {
                    ["props"] = "InvNum,InvSeq,QtyInvoiced,Price",
                    ["filter"] = In("InvNum", batch),
                    ["rowcap"] = "0",
                    ["loadtype"] = "FIRST",
                    ["bookmark"] = "0",
                    ["readonly"] = "1"
                };

                string itemJson = authOverride != null
                    ? await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery, authOverride)
                    : await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery);

                var itemResponse = Deserialize(itemJson);
                if (itemResponse.MessageCode != 0)
                    throw new InvalidOperationException(itemResponse.Message);

                invoiceItems.AddRange(itemResponse.Items.Select(row => (site, MapRow<InvItemInfo>(row))));
            }
        }

        var custAddrs = new List<CustAddrInfo>();
        foreach (var batch in custNums.Chunk(custBatchSize))
        {
            var addrQuery = new Dictionary<string, string>
            {
                ["props"] = "CustNum,CustSeq,Name,City,State",
                ["filter"] = In("CustNum", batch),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var addrResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCustAddrs/adv", addrQuery));
            if (addrResponse.MessageCode == 0)
                custAddrs.AddRange(addrResponse.Items.Select(MapRow<CustAddrInfo>));
        }

        var custAddrLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum))
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First());

        var billToLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum) && c.CustSeq == 0)
            .GroupBy(c => c.CustNum!)
            .ToDictionary(g => g.Key, g => g.First());

        using var connection = _dbConnectionFactory.CreateBatConnection();
        var regionRows = await connection.QueryAsync<(string Region, string RegionName)>(
            "SELECT Region, RegionName FROM Chap_RegionNames WITH (NOLOCK)");
        var regionNameLookup = regionRows.ToDictionary(
            r => r.Region ?? "",
            r => r.RegionName ?? "",
            StringComparer.OrdinalIgnoreCase);

        var hdrLookup = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.Hdr.InvNum))
            .GroupBy(h => (h.Site, h.Hdr.InvNum!, h.Hdr.InvSeq))
            .ToDictionary(g => g.Key, g => g.First().Hdr);

        var joined = new List<(string Customer, string CustomerName, int ShipToNum,
            string ShipToCity, string ShipToState, string Slsman, string Name,
            string BillToState, string UfSalesRegion, string RegionName, string Period,
            decimal ExtPrice)>();

        foreach (var (site, item) in invoiceItems)
        {
            if (string.IsNullOrWhiteSpace(item.InvNum))
                continue;

            var key = (site, item.InvNum!, item.InvSeq);
            if (!hdrLookup.TryGetValue(key, out InvHdrInfo? hdr))
                continue;

            if (hdr.InvDate == null || string.IsNullOrWhiteSpace(hdr.CustNum))
                continue;

            if (allowedCustKeys != null && !allowedCustKeys.Contains($"{hdr.CustNum}|{hdr.CustSeq}"))
                continue;

            var custAddrKey = (hdr.CustNum!, hdr.CustSeq);
            custAddrLookup.TryGetValue(custAddrKey, out CustAddrInfo? shipToCust);
            billToLookup.TryGetValue(hdr.CustNum!, out CustAddrInfo? billToCust);

            regionLookup.TryGetValue(custAddrKey, out string? ufSalesRegion);
            ufSalesRegion ??= "";
            string regionName = !string.IsNullOrWhiteSpace(ufSalesRegion) &&
                                regionNameLookup.TryGetValue(ufSalesRegion, out string? rn)
                ? rn
                : "";

            custSlsmanLookup.TryGetValue(custAddrKey, out string? slsman);
            slsman ??= repCode;

            string period = hdr.InvDate.Value.ToString("MMM") + hdr.InvDate.Value.Year;
            decimal extPrice = item.QtyInvoiced * item.Price;

            joined.Add((
                Customer: hdr.CustNum!,
                CustomerName: billToCust?.Name ?? hdr.AddrName ?? "",
                ShipToNum: hdr.CustSeq,
                ShipToCity: shipToCust?.City ?? "",
                ShipToState: hdr.State ?? shipToCust?.State ?? "",
                Slsman: slsman,
                Name: billToCust?.Name ?? hdr.AddrName ?? "",
                BillToState: billToCust?.State ?? "",
                UfSalesRegion: ufSalesRegion,
                RegionName: regionName,
                Period: period,
                ExtPrice: extPrice
            ));
        }

        return BuildSalesPivotResults(joined, fiscalYear, fyMinus3Months, fyMinus2Months, fyMinus1Months, currentFYMonths);
    }

    private async Task<List<InvLineRawRow>> FetchKentLinesViaStandardIdosAsync(
        string repCode,
        DateTime dateFrom,
        DateTime dateTo,
        string kentAuth)
    {
        var hdrQuery = new Dictionary<string, string>
        {
            ["props"] = "InvNum,InvSeq,CustNum,CustSeq,InvDate,State,Disc,Slsman",
            ["filter"] = $"{Eq("Slsman", repCode)} AND InvDate >= '{dateFrom:yyyyMMdd}' AND InvDate <= '{dateTo:yyyyMMdd}'",
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var hdrResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery, kentAuth));
        if (hdrResponse.MessageCode != 0)
        {
            _logger.LogWarning("SLInvHdrs (KENT fallback) failed: {Msg}", hdrResponse.Message);
            return new List<InvLineRawRow>();
        }

        var headers = hdrResponse.Items
            .Select(MapRow<KentInvHdrRaw>)
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum) && h.InvDate.HasValue)
            .ToList();

        if (headers.Count == 0)
            return new List<InvLineRawRow>();

        var hdrLookup = headers
            .GroupBy(h => (h.InvNum!, h.InvSeq))
            .ToDictionary(g => g.Key, g => g.First());

        var rawItems = new List<KentInvItemRaw>();
        foreach (var batch in headers.Select(h => h.InvNum!).Distinct().Chunk(30))
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"] = "InvNum,InvSeq,Item,QtyInvoiced,Price",
                ["filter"] = In("InvNum", batch),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery, kentAuth));
            if (itemResponse.MessageCode == 0)
                rawItems.AddRange(itemResponse.Items.Select(MapRow<KentInvItemRaw>));
            else
                _logger.LogWarning("SLInvItemAlls (KENT fallback) batch failed: {Msg}", itemResponse.Message);
        }

        var result = new List<InvLineRawRow>();
        foreach (var item in rawItems)
        {
            if (string.IsNullOrWhiteSpace(item.InvNum) || string.IsNullOrWhiteSpace(item.Item))
                continue;

            var key = (item.InvNum!, item.InvSeq);
            if (!hdrLookup.TryGetValue(key, out KentInvHdrRaw? hdr) || !hdr.InvDate.HasValue)
                continue;

            result.Add(new InvLineRawRow
            {
                InvDate = hdr.InvDate,
                CustNum = hdr.CustNum,
                CustSeq = hdr.CustSeq,
                ShipToCity = "",
                ShipToState = hdr.State ?? "",
                BillToState = "",
                Slsman = hdr.Slsman ?? repCode,
                SalesRegion = "",
                RegionName = "",
                Item = item.Item,
                QtyInvoiced = item.QtyInvoiced,
                Price = item.Price,
                Disc = hdr.Disc,
                Period = hdr.InvDate.Value.ToString("MMM") + hdr.InvDate.Value.Year
            });
        }

        _logger.LogInformation(
            "KENT fallback (SLInvHdrs + SLInvItemAlls): {Count} lines for rep {RepCode}",
            result.Count, repCode);

        return result;
    }

    private static List<Dictionary<string, object>> BuildSalesPivotResults(
        List<(string Customer, string CustomerName, int ShipToNum, string ShipToCity, string ShipToState,
            string Slsman, string Name, string BillToState, string UfSalesRegion, string RegionName,
            string Period, decimal ExtPrice)> joined,
        int fiscalYear,
        List<string> fyMinus3Months,
        List<string> fyMinus2Months,
        List<string> fyMinus1Months,
        List<string> currentFYMonths)
    {
        var grouped = joined
            .GroupBy(r => new
            {
                r.Customer,
                r.CustomerName,
                r.ShipToNum,
                r.ShipToCity,
                r.ShipToState,
                r.Slsman,
                r.Name,
                r.BillToState,
                r.UfSalesRegion,
                r.RegionName
            })
            .Select(g => new
            {
                g.Key,
                PeriodTotals = g.GroupBy(x => x.Period)
                    .ToDictionary(pg => pg.Key, pg => pg.Sum(x => x.ExtPrice))
            })
            .ToList();

        var fyMinus3Label = $"FY{fiscalYear - 3}";
        var fyMinus2Label = $"FY{fiscalYear - 2}";
        var fyMinus1Label = $"FY{fiscalYear - 1}";
        var fyCurrentLabel = $"FY{fiscalYear}";

        var results = new List<Dictionary<string, object>>();

        foreach (var g in grouped)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customer"] = g.Key.Customer,
                ["Customer Name"] = g.Key.CustomerName,
                ["Ship To Num"] = g.Key.ShipToNum,
                ["Ship To City"] = g.Key.ShipToCity,
                ["Ship To State"] = g.Key.ShipToState,
                ["slsman"] = g.Key.Slsman,
                ["name"] = g.Key.Name,
                ["Bill To State"] = g.Key.BillToState,
                ["Uf_SalesRegion"] = g.Key.UfSalesRegion,
                ["RegionName"] = g.Key.RegionName,
            };

            decimal SumFy(List<string> months) =>
                months.Sum(m => g.PeriodTotals.TryGetValue(m, out decimal v) ? v : 0m);

            dict[fyMinus3Label] = SumFy(fyMinus3Months);
            dict[fyMinus2Label] = SumFy(fyMinus2Months);
            dict[fyMinus1Label] = SumFy(fyMinus1Months);
            dict[fyCurrentLabel] = SumFy(currentFYMonths);

            foreach (var month in currentFYMonths)
                dict[month] = g.PeriodTotals.TryGetValue(month, out decimal v) ? v : 0m;

            results.Add(dict);
        }

        return results
            .OrderByDescending(d => d.TryGetValue(fyMinus1Label, out object? v) && v is decimal dec ? dec : 0m)
            .ToList();
    }

    public async Task<PackingList> GetPackingListByShipmentAsync(string packNum)
    {
        var result = new PackingList();
        if (string.IsNullOrWhiteSpace(packNum))
            return result;

        // Line items + order/customer fields from SLCoShips
        var shipQuery = new Dictionary<string, string>
        {
            ["props"] = "BolNumber,CoNum,CoCustNum,CoCustPo,CoLine,CoiItem,CoiDescription,CoiUM,QtyShipped",
            ["filter"] = Eq("BolNumber", packNum),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var shipResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCoShips/adv", shipQuery));
        if (shipResponse.MessageCode != 0)
            throw new InvalidOperationException(shipResponse.Message);

        var shipRows = shipResponse.Items.Select(MapRow<PackingListShipRow>).ToList();

        // Header fields (dates, warehouse, carrier, ship-to address) from ait_ss_bols
        var bolQuery = new Dictionary<string, string>
        {
            ["props"] = "ShipmentId,ShipDate,Whse,ShipCode,CarrierCode,CustNum,ConsigneeName,ConsigneeAddr1,ConsigneeAddr2,ConsigneeAddr3,ConsigneeAddr4,ConsigneeCity,ConsigneeState,ConsigneeZip",
            ["filter"] = Eq("ShipmentId", packNum),
            ["rowcap"] = "1",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var bolResponse = Deserialize(await _csiRestClient.GetAsync("json/ait_ss_bols/adv", bolQuery));
        PackingListBolRow? bol = bolResponse.MessageCode == 0 && bolResponse.Items.Count > 0
            ? MapRow<PackingListBolRow>(bolResponse.Items[0])
            : null;

        var firstShip = shipRows.FirstOrDefault();
        result.Header = new PackingListHeader
        {
            PackNum    = packNum,
            PackDate   = bol?.ShipDate,
            Whse       = bol?.Whse ?? "",
            CoNum      = firstShip?.CoNum ?? "",
            CustNum    = firstShip?.CustNum ?? bol?.CustNum ?? "",
            ShipCode   = bol?.ShipCode ?? "",
            Carrier    = bol?.CarrierCode ?? "",
            ShipAddr   = bol?.ConsigneeName ?? "",
            ShipAddr2  = bol?.ConsigneeAddr1 ?? "",
            ShipAddr3  = bol?.ConsigneeAddr2 ?? "",
            ShipAddr4  = bol?.ConsigneeAddr3 ?? "",
            ShipCity   = bol?.ConsigneeCity ?? "",
            ShipState  = bol?.ConsigneeState ?? "",
            ShipZip    = bol?.ConsigneeZip ?? "",
            CustPo     = firstShip?.CustPo ?? "",
        };

        result.Items = shipRows.Select(r => new PackingListItem
        {
            CoLine     = r.CoLine,
            Item       = r.Item ?? "",
            ItemDesc   = r.ItemDesc ?? "",
            UM         = r.UM ?? "",
            ShipmentId = packNum,
            QtyPicked  = r.QtyShipped,
            QtyShipped = r.QtyShipped,
        }).ToList();

        return result;
    }

    public async Task<List<PackingList>> GetPackingListsByOrderAsync(string coNum)
    {
        if (string.IsNullOrWhiteSpace(coNum))
            return new List<PackingList>();

        // Fetch all shipment lines for this order in one query
        var shipQuery = new Dictionary<string, string>
        {
            ["props"] = "BolNumber,CoNum,CoCustNum,CoCustPo,CoLine,CoiItem,CoiDescription,CoiUM,QtyShipped",
            ["filter"] = Eq("CoNum", coNum),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var shipResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCoShips/adv", shipQuery));
        if (shipResponse.MessageCode != 0)
            throw new InvalidOperationException(shipResponse.Message);

        var shipRows = shipResponse.Items.Select(MapRow<PackingListShipRow>).ToList();
        if (shipRows.Count == 0)
            return new List<PackingList>();

        // Group lines by pack number (BolNumber)
        var byBol = shipRows
            .Where(r => !string.IsNullOrWhiteSpace(r.BolNumber))
            .GroupBy(r => r.BolNumber!)
            .ToList();

        // Fetch all BOL headers in one query
        var bolNums = byBol.Select(g => g.Key).ToList();
        var bolQuery = new Dictionary<string, string>
        {
            ["props"] = "ShipmentId,ShipDate,Whse,ShipCode,CarrierCode,CustNum,ConsigneeName,ConsigneeAddr1,ConsigneeAddr2,ConsigneeAddr3,ConsigneeAddr4,ConsigneeCity,ConsigneeState,ConsigneeZip",
            ["filter"] = In("ShipmentId", bolNums),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var bolResponse = Deserialize(await _csiRestClient.GetAsync("json/ait_ss_bols/adv", bolQuery));
        var bolLookup = (bolResponse.MessageCode == 0
            ? bolResponse.Items.Select(MapRow<PackingListBolRow>)
                                .Where(b => !string.IsNullOrWhiteSpace(b.ShipmentId))
            : Enumerable.Empty<PackingListBolRow>())
            .ToDictionary(b => b.ShipmentId!, StringComparer.OrdinalIgnoreCase);

        var results = new List<PackingList>();
        foreach (var group in byBol)
        {
            var packNum = group.Key;
            bolLookup.TryGetValue(packNum, out PackingListBolRow? bol);
            var firstShip = group.First();

            var pl = new PackingList
            {
                Header = new PackingListHeader
                {
                    PackNum   = packNum,
                    PackDate  = bol?.ShipDate,
                    Whse      = bol?.Whse ?? "",
                    CoNum     = firstShip.CoNum ?? "",
                    CustNum   = firstShip.CustNum ?? bol?.CustNum ?? "",
                    ShipCode  = bol?.ShipCode ?? "",
                    Carrier   = bol?.CarrierCode ?? "",
                    ShipAddr  = bol?.ConsigneeName ?? "",
                    ShipAddr2 = bol?.ConsigneeAddr1 ?? "",
                    ShipAddr3 = bol?.ConsigneeAddr2 ?? "",
                    ShipAddr4 = bol?.ConsigneeAddr3 ?? "",
                    ShipCity  = bol?.ConsigneeCity ?? "",
                    ShipState = bol?.ConsigneeState ?? "",
                    ShipZip   = bol?.ConsigneeZip ?? "",
                    CustPo    = firstShip.CustPo ?? "",
                },
                Items = group.Select(r => new PackingListItem
                {
                    CoLine     = r.CoLine,
                    Item       = r.Item ?? "",
                    ItemDesc   = r.ItemDesc ?? "",
                    UM         = r.UM ?? "",
                    ShipmentId = packNum,
                    QtyPicked  = r.QtyShipped,
                    QtyShipped = r.QtyShipped,
                }).ToList()
            };
            results.Add(pl);
        }

        return results;
    }

    public async Task<List<Customer>> GetCustomersDetailsByRepCodeAsync(string repCode)
    {
        // Load supplemental data from local DBs (not available in CSI IDO)
        using var repConnection = _dbConnectionFactory.CreateRepConnection();
        var exclusionCodes = (await repConnection.QueryAsync<string>(
            "SELECT Code FROM CreditHoldReasonCodeExclusions WITH (NOLOCK)"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var batConnection = _dbConnectionFactory.CreateBatConnection();
        var salesManagerRows = await batConnection.QueryAsync<(string Initials, string Name)>(
            "SELECT SalesManagerInitials, SalesManagerName FROM Chap_SalesManagers WITH (NOLOCK)");
        var salesManagerLookup = salesManagerRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Initials))
            .ToDictionary(r => r.Initials!, r => r.Name ?? "", StringComparer.OrdinalIgnoreCase);

        // Build IDO filter matching SQL path logic
        var isAdmin = string.Equals(repCode, "Admin", StringComparison.OrdinalIgnoreCase);
        var isDal   = string.Equals(repCode, "DAL",   StringComparison.OrdinalIgnoreCase);

        string? filter = null;
        if (!isAdmin)
        {
            if (isDal)
            {
                // DAL gets their own customers plus a fixed set of special customer numbers
                var dalSpecialNums = new[] { "45424", "45427", "45424K", "  45424", "  45427", "  45424K" };
                filter = $"(Slsman = 'DAL' OR {In("CustNum", dalSpecialNums)})";
            }
            else
            {
                filter = Eq("Slsman", repCode);
            }
        }

        const string props =
            "CustNum,Name,Slsman,Stat,CustType,CustTypeDescription,CorpCust," +
            "CreditHold,CreditHoldDate,CreditHoldReason,CreditHoldDescription," +
            "Addr_1,Addr_2,Addr_3,Addr_4,City,StateCode,Zip,Country," +
            "cusUf_PROG_BASIS,cusUf_FrtTerms,cusuf_c_slsmgr";

        var query = new Dictionary<string, string>
        {
            ["props"]    = props,
            ["orderby"]  = "Name",
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        if (!string.IsNullOrEmpty(filter))
            query["filter"] = filter;

        var response = Deserialize(await _csiRestClient.GetAsync("json/SLCustomers/adv", query));
        if (response.MessageCode != 0)
            throw new InvalidOperationException(response.Message);

        var customers = response.Items.Select(MapRow<Customer>).ToList();

        // Mirror the SQL WHERE exclusion on credit hold reason codes (those codes live in RepPortal DB)
        customers = customers
            .Where(c => string.IsNullOrWhiteSpace(c.CreditHoldReason) ||
                        !exclusionCodes.Contains(c.CreditHoldReason))
            .ToList();

        // Populate SalesManagerName from local BAT lookup (Chap_SalesManagers)
        foreach (var customer in customers)
        {
            customer.SalesManagerName =
                !string.IsNullOrWhiteSpace(customer.SalesManager) &&
                salesManagerLookup.TryGetValue(customer.SalesManager, out string? name)
                    ? name
                    : "To Be Assigned";
        }

        return customers;
    }

    public async Task<ItemDetail> GetItemDetailAsync(string item)
    {
        // Most recent pricing row — also carries ItmDescription from joined item_mst
        var priceQuery = new Dictionary<string, string>
        {
            ["props"]    = "Item,ItmDescription,UnitPrice1,UnitPrice2,UnitPrice3,EffectDate",
            ["filter"]   = Eq("Item", item),
            ["orderby"]  = "EffectDate DESC",
            ["rowcap"]   = "1",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var priceResponse = Deserialize(await _csiRestClient.GetAsync("json/SLItemprices/adv", priceQuery));
        if (priceResponse.MessageCode != 0 || priceResponse.Items.Count == 0)
            return null!;

        var priceRow = priceResponse.Items[0];

        // Item status lives in SLItems, not SLItemprices
        var itemQuery = new Dictionary<string, string>
        {
            ["props"]    = "Item,Stat",
            ["filter"]   = Eq("Item", item),
            ["rowcap"]   = "1",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var itemResponse = Deserialize(await _csiRestClient.GetAsync("json/SLItems/adv", itemQuery));
        string? stat = null;
        if (itemResponse.MessageCode == 0 && itemResponse.Items.Count > 0)
            stat = GetCell(itemResponse.Items[0], "Stat");

        static decimal ParseDecimal(string? raw) =>
            decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal v) ? v : 0m;

        return new ItemDetail
        {
            Item        = GetCell(priceRow, "Item") ?? item,
            Description = GetCell(priceRow, "ItmDescription") ?? "",
            Price1      = ParseDecimal(GetCell(priceRow, "UnitPrice1")),
            Price2      = ParseDecimal(GetCell(priceRow, "UnitPrice2")),
            Price3      = ParseDecimal(GetCell(priceRow, "UnitPrice3")),
            ItemStatus  = stat ?? "A"
        };
    }

    public Task<List<Dictionary<string, object>>> GetItemSalesReportDataAsync(string repCode, IEnumerable<string>? allowedRegions)
        => throw new NotImplementedException("GetItemSalesReportDataAsync IDO implementation pending.");

    public Task<(OrderLookupHeader? Header, List<OrderLookupLine> Lines)> GetOrderLookupAsync(string custNum, string normalizedPo, string repCode)
        => throw new NotImplementedException("GetOrderLookupAsync IDO implementation pending.");

    private static MgRestAdvResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<MgRestAdvResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static string? GetCell(List<MgNameValue> row, string fieldName) =>
        row.FirstOrDefault(c => string.Equals(c.Name, fieldName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static T MapRow<T>(List<MgNameValue> row)
        where T : new()
    {
        var obj = new T();
        var props = typeof(T).GetProperties();

        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<CsiFieldAttribute>();
            if (attr == null)
                continue;

            var cell = row.FirstOrDefault(c =>
                string.Equals(c.Name, attr.FieldName, StringComparison.OrdinalIgnoreCase));

            if (cell?.Value == null)
                continue;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            object? value = ConvertTo(cell.Value, targetType);
            prop.SetValue(obj, value);
        }

        return obj;
    }

    private static object? ConvertTo(string? raw, Type targetType)
    {
        if (targetType == null)
            throw new ArgumentNullException(nameof(targetType));

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlyingType != null;
        var effectiveType = underlyingType ?? targetType;

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (isNullable || !effectiveType.IsValueType)
                return null;

            throw new FormatException($"Cannot convert null/empty value to non-nullable type '{effectiveType.Name}'.");
        }

        if (effectiveType == typeof(string))
            return raw;

        if (effectiveType == typeof(DateTime))
        {
            string[] formats =
            {
                "yyyyMMdd HH:mm:ss.fff",
                "yyyyMMdd",
                "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm:ss",
                "o"
            };

            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
                return dt;

            throw new FormatException($"Cannot convert '{raw}' to DateTime.");
        }

        if (effectiveType == typeof(decimal))
        {
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                return dec;
            throw new FormatException($"Cannot convert '{raw}' to decimal.");
        }

        if (effectiveType == typeof(int))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return i;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (int)d;

            throw new FormatException($"Cannot convert '{raw}' to int.");
        }

        return Convert.ChangeType(raw, effectiveType, CultureInfo.InvariantCulture);
    }

    private static string Eq(string field, string value) =>
        $"{field} = '{value.Replace("'", "''")}'";

    private static string In(string field, IEnumerable<string> values)
    {
        var safeValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => $"'{v.Replace("'", "''")}'");

        return $"{field} IN ({string.Join(",", safeValues)})";
    }

    private static string DateGt(string field, DateTime date) =>
        $"{field} > '{date:yyyyMMdd}'";

    private sealed class InvLineRawRow
    {
        [CsiField("InvDate")] public DateTime? InvDate { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("ShipToCity")] public string? ShipToCity { get; set; }
        [CsiField("ShipToState")] public string? ShipToState { get; set; }
        [CsiField("BillToState")] public string? BillToState { get; set; }
        [CsiField("Slsman")] public string? Slsman { get; set; }
        [CsiField("SalesRegion")] public string? SalesRegion { get; set; }
        [CsiField("RegionName")] public string? RegionName { get; set; }
        [CsiField("item")] public string? Item { get; set; }
        [CsiField("qty_invoiced")] public decimal QtyInvoiced { get; set; }
        [CsiField("price")] public decimal Price { get; set; }
        [CsiField("disc")] public decimal Disc { get; set; }
        [CsiField("Period")] public string? Period { get; set; }

        public decimal NetRevenue => QtyInvoiced * Price * (100m - Disc) / 100m;
    }

    private sealed class CustNameInfo
    {
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("Name")] public string? Name { get; set; }
        [CsiField("Uf_SalesRegion")] public string? UfSalesRegion { get; set; }
    }

    private sealed class ItemDescInfo
    {
        [CsiField("Item")] public string? Item { get; set; }
        [CsiField("Description")] public string? Description { get; set; }
    }

    private sealed class KentInvHdrRaw
    {
        [CsiField("InvNum")] public string? InvNum { get; set; }
        [CsiField("InvSeq")] public int InvSeq { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("InvDate")] public DateTime? InvDate { get; set; }
        [CsiField("State")] public string? State { get; set; }
        [CsiField("Disc")] public decimal Disc { get; set; }
        [CsiField("Slsman")] public string? Slsman { get; set; }
    }

    private sealed class KentInvItemRaw
    {
        [CsiField("InvNum")] public string? InvNum { get; set; }
        [CsiField("InvSeq")] public int InvSeq { get; set; }
        [CsiField("Item")] public string? Item { get; set; }
        [CsiField("QtyInvoiced")] public decimal QtyInvoiced { get; set; }
        [CsiField("Price")] public decimal Price { get; set; }
    }

    private sealed class BolInfo
    {
        [CsiField("ShipmentId")] public int? ShipmentId { get; set; }
        [CsiField("InvoiceeState")] public string? InvoiceeState { get; set; }
        [CsiField("ConsigneeState")] public string? ConsigneeState { get; set; }
        [CsiField("Whse")] public string? Whse { get; set; }
        [CsiField("CarrierCode")] public string? CarrierCode { get; set; }
        [CsiField("ShipCode")] public string? ShipCode { get; set; }
        [CsiField("ShipCodeDesc")] public string? ShipCodeDesc { get; set; }
        [CsiField("ShipDate")] public DateTime? ShipDate { get; set; }
        [CsiField("BillTransportationTo")] public string? BillTransportationTo { get; set; }
        [CsiField("TrackingNumber")] public string? TrackingNumber { get; set; }
        [CsiField("InvoiceeName")] public string? InvoiceeName { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int? CustSeq { get; set; }
    }

    private sealed class InvHdrInfo
    {
        [CsiField("InvNum")] public string? InvNum { get; set; }
        [CsiField("InvSeq")] public int InvSeq { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("Slsman")] public string? Slsman { get; set; }
        [CsiField("AddrName")] public string? AddrName { get; set; }
        [CsiField("State")] public string? State { get; set; }
        [CsiField("ShipDate")] public DateTime? ShipDate { get; set; }
        [CsiField("CustPo")] public string? CustPo { get; set; }
        [CsiField("InvDate")] public DateTime? InvDate { get; set; }
    }

    private sealed class CoItemInfo
    {
        [CsiField("CoNum")] public string? CoNum { get; set; }
        [CsiField("CoLine")] public int CoLine { get; set; }
        [CsiField("Adr0Name")] public string? Adr0Name { get; set; }
        [CsiField("DueDate")] public DateTime? DueDate { get; set; }
        [CsiField("CoOrderDate")] public DateTime? CoOrderDate { get; set; }
    }

    private sealed class InvItemInfo
    {
        [CsiField("InvNum")] public string? InvNum { get; set; }
        [CsiField("InvSeq")] public int InvSeq { get; set; }
        [CsiField("QtyInvoiced")] public decimal QtyInvoiced { get; set; }
        [CsiField("Price")] public decimal Price { get; set; }
    }

    private sealed class CustAddrInfo
    {
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("Name")] public string? Name { get; set; }
        [CsiField("City")] public string? City { get; set; }
        [CsiField("State")] public string? State { get; set; }
    }

    private sealed class PackingListBolRow
    {
        [CsiField("ShipmentId")]     public string? ShipmentId { get; set; }
        [CsiField("ShipDate")]       public DateTime? ShipDate { get; set; }
        [CsiField("Whse")]           public string? Whse { get; set; }
        [CsiField("ShipCode")]       public string? ShipCode { get; set; }
        [CsiField("CarrierCode")]    public string? CarrierCode { get; set; }
        [CsiField("CustNum")]        public string? CustNum { get; set; }
        [CsiField("ConsigneeName")]  public string? ConsigneeName { get; set; }
        [CsiField("ConsigneeAddr1")] public string? ConsigneeAddr1 { get; set; }
        [CsiField("ConsigneeAddr2")] public string? ConsigneeAddr2 { get; set; }
        [CsiField("ConsigneeAddr3")] public string? ConsigneeAddr3 { get; set; }
        [CsiField("ConsigneeAddr4")] public string? ConsigneeAddr4 { get; set; }
        [CsiField("ConsigneeCity")]  public string? ConsigneeCity { get; set; }
        [CsiField("ConsigneeState")] public string? ConsigneeState { get; set; }
        [CsiField("ConsigneeZip")]   public string? ConsigneeZip { get; set; }
    }

    private sealed class PackingListShipRow
    {
        [CsiField("BolNumber")]      public string? BolNumber { get; set; }
        [CsiField("CoNum")]          public string? CoNum { get; set; }
        [CsiField("CoCustNum")]      public string? CustNum { get; set; }
        [CsiField("CoCustPo")]       public string? CustPo { get; set; }
        [CsiField("CoLine")]         public int CoLine { get; set; }
        [CsiField("CoiItem")]        public string? Item { get; set; }
        [CsiField("CoiDescription")] public string? ItemDesc { get; set; }
        [CsiField("CoiUM")]          public string? UM { get; set; }
        [CsiField("QtyShipped")]     public decimal QtyShipped { get; set; }
    }
}

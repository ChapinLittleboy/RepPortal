namespace RepPortal.Models;

using System;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using RepPortal.Services;

public enum ReportType
{
    MonthlyInvoicedSales,  //no parameters
    OpenOrders,            //no parameters
    Shipments,             // customer, daterange
    InvoicedAccounts,      // customer, daterange
    MonthlySales,          //no parameters (Historic territory)
    MonthlySalesByItem,    //no parameters (Historic territory)
    PivotSalesByItem       //no parameters (Historic territory)
}

public sealed record ReportRequest(
    ReportType ReportType,
    string Email,
    string? CustomerId, // null => ALL customers
    string? DateRangeCode // still undefined
);



using RepPortal.Models;

namespace RepPortal.Services;

/// <summary>
/// Provides access to CSI SyteLine IDO data for customer order lookup.
/// </summary>
public interface IIdoService
{
    /// <summary>
    /// Fetches an order header and its line items from the IDO API.
    /// </summary>
    /// <param name="custNum">Customer number (exact value from the customer dropdown).</param>
    /// <param name="normalizedPo">Customer PO / order number with all non-alphanumeric chars stripped.</param>
    /// <param name="repCode">Current rep code used for access filtering ("Admin" bypasses slsman filter).</param>
    Task<(OrderLookupHeader? Header, List<OrderLookupLine> Lines)> GetOrderLookupAsync(
        string custNum, string normalizedPo, string repCode);
}

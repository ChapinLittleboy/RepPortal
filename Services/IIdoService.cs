using RepPortal.Models;

namespace RepPortal.Services;

/// <summary>
/// IDO (Infor Data Object) API client — pure API path, no direct SQL.
/// Used by hybrid services (e.g. ItemService) when CsiOptions.UseApi is true.
/// </summary>
public interface IIdoService
{
    /// <summary>
    /// Fetches item master and current pricing from the CSI IDO API.
    /// Returns null when no item record is found.
    /// </summary>
    Task<ItemDetail?> GetItemDetailAsync(string item);
}

using RepPortal.Models;

namespace RepPortal.Services;

public interface IIdoService
{
    Task<List<Customer>> GetCustomersDetailsByRepCodeAsync(string repCode);
}

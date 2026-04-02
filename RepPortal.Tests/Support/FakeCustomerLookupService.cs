using RepPortal.Models;
using RepPortal.Services;

namespace RepPortal.Tests.Support;

internal sealed class FakeCustomerLookupService : ICustomerLookupService
{
    public List<Customer> Customers { get; } = new();

    public Task<List<Customer>> GetCustomersDetailsByRepCodeAsync()
        => Task.FromResult(Customers.ToList());
}

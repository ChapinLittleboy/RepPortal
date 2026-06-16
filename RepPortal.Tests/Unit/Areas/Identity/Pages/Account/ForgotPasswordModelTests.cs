using RepPortal.Areas.Identity.Pages.Account;

namespace RepPortal.Tests.Unit.Areas.Identity.Pages.Account;

public class ForgotPasswordModelTests
{
    [Fact]
    public void GetCompanyEmailLookupCandidates_ShouldIncludeNewDomain_WhenOldDomainEntered()
    {
        var candidates = ForgotPasswordModel.GetCompanyEmailLookupCandidates("alice@chapinmfg.com");

        Assert.Equal(new[] { "alice@chapinmfg.com", "alice@chapinusa.com" }, candidates);
    }

    [Fact]
    public void GetCompanyEmailLookupCandidates_ShouldIncludeOldDomain_WhenNewDomainEntered()
    {
        var candidates = ForgotPasswordModel.GetCompanyEmailLookupCandidates("alice@chapinusa.com");

        Assert.Equal(new[] { "alice@chapinusa.com", "alice@chapinmfg.com" }, candidates);
    }

    [Fact]
    public void GetCompanyEmailLookupCandidates_ShouldOnlyReturnEnteredEmail_ForOtherDomains()
    {
        var candidates = ForgotPasswordModel.GetCompanyEmailLookupCandidates("alice@example.com");

        Assert.Equal(new[] { "alice@example.com" }, candidates);
    }
}

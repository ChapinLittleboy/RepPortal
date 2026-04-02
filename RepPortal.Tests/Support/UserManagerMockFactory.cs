using Microsoft.AspNetCore.Identity;
using Moq;
using RepPortal.Data;

namespace RepPortal.Tests.Support;

internal static class UserManagerMockFactory
{
    public static Mock<UserManager<ApplicationUser>> Create()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mock = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        return mock;
    }
}

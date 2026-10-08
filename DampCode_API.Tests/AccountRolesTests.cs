using DampCode_API.Authentication;
using DampCode_API.Models;

namespace DampCode_API.Tests;

public sealed class AccountRolesTests
{
    [Theory]
    [InlineData(AccountType.Participant, "participante")]
    [InlineData(AccountType.Company, "empresa")]
    [InlineData(AccountType.Admin, "admin")]
    public void ToFrontRole_MapsEveryAccountType(AccountType accountType, string expected) =>
        Assert.Equal(expected, AccountRoles.ToFrontRole(accountType));

    [Fact]
    public void ToFrontRole_UnknownValue_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AccountRoles.ToFrontRole((AccountType)99));
}

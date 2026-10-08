using DampCode_API.Authentication;
using DampCode_API.Models;

namespace DampCode_API.Dto;

public static class UserResponseProjection
{
    // Conta sem perfil nem empresa (ex.: Admin) usa o e-mail como nome.
    public static IQueryable<UserResponseDto> SelectUserResponse(this IQueryable<Account> accounts) =>
        accounts.Select(account => new UserResponseDto(
            account.Id,
            (account.AccountType == AccountType.Company ? account.PrimaryCompany!.Name : account.UserProfile!.Name) ?? account.Email,
            account.Email,
            AccountRoles.ToFrontRole(account.AccountType),
            account.UserProfile == null ? null : account.UserProfile.Level,
            account.UserProfile == null ? null : account.UserProfile.Xp));
}

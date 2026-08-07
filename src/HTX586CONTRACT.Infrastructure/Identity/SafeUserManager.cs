using System.Security.Claims;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HTX586CONTRACT.Infrastructure.Identity;

/// <summary>
/// Executes every ASP.NET Core Identity UserManager operation inside a fresh DI scope.
/// This is important for Blazor Server because a normal scoped UserManager can live for
/// the whole circuit and therefore retain the same EF Core DbContext across overlapping
/// component/service operations.
/// </summary>
public sealed class SafeUserManager(IServiceScopeFactory scopeFactory)
{
    public async Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.CreateAsync(user, password);
    }

    public async Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.AddToRoleAsync(user, role);
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.DeleteAsync(user);
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.FindByIdAsync(userId);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.GetRolesAsync(user);
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string role)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.IsInRoleAsync(user, role);
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.UpdateAsync(user);
    }

    public async Task<IdentityResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.RemoveFromRolesAsync(user, roles);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.ResetPasswordAsync(user, token, newPassword);
    }

    public async Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await manager.GetUserAsync(principal);
    }

    /// <summary>
    /// Matches ASP.NET Core Identity's default UpperInvariantLookupNormalizer without
    /// touching the UserManager/DbContext. This is safe to call from import validation.
    /// </summary>
    public string? NormalizeName(string? name)
        => name?.Normalize().ToUpperInvariant();
}

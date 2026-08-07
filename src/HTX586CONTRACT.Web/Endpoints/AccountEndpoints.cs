using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;

namespace HTX586CONTRACT.Web.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(
        this WebApplication app)
    {
        // Chuyển hướng đăng nhập/đăng xuất sang POST để tránh bị CSRF. Các form login/logout sẽ có token chống CSRF.
        app.MapPost(
                "/account/login/submit",
                LoginAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        app.MapPost(
                "/account/logout/submit",
                LogoutAsync)
            .RequireAuthorization()
            .DisableAntiforgery();
    }

    private static async Task<IResult> LoginAsync(
        HttpContext httpContext,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        // Lấy dữ liệu form đăng nhập từ request body. Không dùng [FromForm] vì muốn đọc trực tiếp từ HttpContext.Request.
        var form =
            await httpContext.Request.ReadFormAsync(
                httpContext.RequestAborted);

        var loginName =
            form["LoginName"]
                .ToString()
                .Trim();

        var password =
            form["Password"]
                .ToString();

        var rememberMe =
            string.Equals(
                form["RememberMe"],
                "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                form["RememberMe"],
                "on",
                StringComparison.OrdinalIgnoreCase);

        var returnUrl =
            form["ReturnUrl"]
                .ToString();

        if (string.IsNullOrWhiteSpace(loginName) ||
            string.IsNullOrWhiteSpace(password))
        {
            return RedirectToLogin(
                "invalid",
                returnUrl);
        }

        var normalizedUserName =
            userManager.NormalizeName(loginName);

        var hasValidPhone =
            VietnamPhoneNumber.TryNormalize(loginName, out var normalizedPhone);

        /*
         * Chỉ cho phép đăng nhập bằng tên đăng nhập hoặc số điện thoại.
         * Không dùng email và mã nhân viên làm thông tin đăng nhập.
         *
         * Không dùng FindByNameAsync() vì cần phát hiện trường hợp dữ liệu
         * số điện thoại/tên đăng nhập bị trùng để tránh đăng nhập nhầm tài khoản.
         */
        var loginCandidates =
            await userManager.Users
                .AsNoTracking()
                .Where(x =>
                    !x.IsDeleted &&
                    (x.NormalizedUserName == normalizedUserName ||
                     (hasValidPhone && x.PhoneNumber != null)))
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.CreatedAt)
                .ToListAsync(
                    httpContext.RequestAborted);

        var matchedUsers = loginCandidates
            .Where(x =>
                x.NormalizedUserName == normalizedUserName ||
                (hasValidPhone &&
                 VietnamPhoneNumber.TryNormalize(x.PhoneNumber, out var storedPhone) &&
                 storedPhone == normalizedPhone))
            .Take(2)
            .ToList();

        if (matchedUsers.Count == 0)
        {
            return RedirectToLogin(
                "invalid",
                returnUrl);
        }

        /*
         * Không tự chọn tài khoản đầu tiên nếu có nhiều tài khoản
         * cùng khớp thông tin đăng nhập.
         *
         * Việc tự chọn có thể khiến người dùng đăng nhập nhầm
         * tài khoản của người khác.
         */
        if (matchedUsers.Count > 1)
        {
            return RedirectToLogin(
                "duplicate",
                returnUrl);
        }

        var matchedUser = matchedUsers[0];

        /*
         * Lấy lại user có tracking trước khi gọi Identity.
         * Bản AsNoTracking chỉ dùng để kiểm tra trùng dữ liệu.
         */
        var user =
            await userManager.Users
                .Include(x => x.AdminOffices)
                    .ThenInclude(x => x.CompanyProfile)
                .FirstOrDefaultAsync(
                    x => x.Id == matchedUser.Id && !x.IsDeleted,
                    httpContext.RequestAborted);

        if (user is null)
        {
            return RedirectToLogin(
                "invalid",
                returnUrl);
        }

        if (user.IsDeleted || !user.IsActive)
        {
            var status = user.RegistrationStatus?.Trim().ToLowerInvariant();
            return RedirectToLogin(status == "pending" ? "pending" : status == "rejected" ? "rejected" : "inactive", returnUrl);
        }

        // Dữ liệu nguồn phải nhất quán: VehicleOwner chỉ được đăng nhập
        // khi yêu cầu đăng ký đã ở trạng thái Approved.
        if (await userManager.IsInRoleAsync(user, "VehicleOwner") &&
            !string.Equals(user.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var status = user.RegistrationStatus?.Trim().ToLowerInvariant();
            return RedirectToLogin(status == "pending" ? "pending" : "rejected", returnUrl);
        }

        var result =
            await signInManager.PasswordSignInAsync(
                user,
                password,
                rememberMe,
                lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return RedirectToLogin(
                "locked",
                returnUrl);
        }

        if (result.IsNotAllowed)
        {
            return RedirectToLogin(
                "notallowed",
                returnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToLogin(
                "twofactor",
                returnUrl);
        }

        if (!result.Succeeded)
        {
            return RedirectToLogin(
                "invalid",
                returnUrl);
        }

        var roles =
            await userManager.GetRolesAsync(user);

        var isOwner = roles.Contains("Owner", StringComparer.OrdinalIgnoreCase);
        var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        // Admin phải có ít nhất một văn phòng đang hoạt động trong AdminOffice.
        // VehicleOwner nhận phạm vi làm việc qua các xe trong OfficeVehicle.
        if (!isOwner && isAdmin &&
            !user.AdminOffices.Any(x => x.IsActive && !x.IsDeleted &&
                                        x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted))
        {
            await signInManager.SignOutAsync();
            return RedirectToLogin("inactive", returnUrl);
        }

        var isVehicleOwner = roles.Contains(
            "VehicleOwner",
            StringComparer.OrdinalIgnoreCase);

        if (!isOwner && !isAdmin && !isVehicleOwner)
        {
            await signInManager.SignOutAsync();
            return RedirectToLogin("norole", returnUrl);
        }

        // Không cho returnUrl bỏ qua bước khai báo chân ký lần đầu.
        if (isVehicleOwner && string.IsNullOrWhiteSpace(user.VehicleOwnerSignatureFileUrl))
        {
            return Results.Redirect(
                "/vehicle-owner/account?signatureRequired=1");
        }

        if (IsLocalUrl(returnUrl))
        {
            return Results.Redirect(returnUrl);
        }

        if (roles.Contains(
                "Owner",
                StringComparer.OrdinalIgnoreCase))
        {
            return Results.Redirect(
                "/owner/dashboard");
        }

        if (roles.Contains(
                "Admin",
                StringComparer.OrdinalIgnoreCase))
        {
            return Results.Redirect(
                "/admin/dashboard");
        }

        if (isVehicleOwner)
        {
            return Results.Redirect(
                "/vehicle-owner/dashboard");
        }

        /*
         * Tài khoản đăng nhập thành công nhưng chưa được gán role.
         */
        await signInManager.SignOutAsync();

        return RedirectToLogin(
            "norole",
            returnUrl);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();

        var returnUrl =
            httpContext.Request.Query["returnUrl"]
                .ToString();

        if (IsLocalUrl(returnUrl))
        {
            return Results.Redirect(returnUrl);
        }

        return Results.Redirect(
            "/account/login");
    }

    private static IResult RedirectToLogin(
        string error,
        string? returnUrl)
    {
        var url =
            "/account/login?error=" +
            Uri.EscapeDataString(error);

        if (IsLocalUrl(returnUrl))
        {
            url +=
                "&returnUrl=" +
                Uri.EscapeDataString(returnUrl!);
        }

        return Results.Redirect(url);
    }

    private static bool IsLocalUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               url[0] == '/' &&
               !url.StartsWith("//") &&
               !url.StartsWith("/\\");
    }
}
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using HTX586CONTRACT.Infrastructure.Services;
using HTX586CONTRACT.Web.Components;
using HTX586CONTRACT.Web.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using HTX586CONTRACT.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

var connectionString = builder.Configuration.GetConnectionString("Vps");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Không tìm thấy connection string. Hãy cấu hình ConnectionStrings:Vps bằng user-secrets hoặc biến môi trường.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options =>
    {
        options.UseSqlServer(connectionString);
    });

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 6;
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = false;
    o.Password.RequireLowercase = false;
    o.Password.RequireNonAlphanumeric = false;
    o.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "HTX586CONTRACT.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax; o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/access-denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
});


builder.Services
    .AddAuthorizationBuilder()

    // Quyền truy cập route chỉ kiểm tra đăng nhập/phân quyền.
    // Việc bắt buộc đổi mật khẩu được khóa bằng overlay toàn cục trong MainLayout.
    .SetDefaultPolicy(
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build())

    // Page đổi mật khẩu chỉ cần đăng nhập.
    .AddPolicy(
        "PasswordChangeAllowed",
        policy =>
        {
            policy.RequireAuthenticatedUser();
        });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<
    IAuthorizationHandler,
    PasswordChangedHandler>();

builder.Services.AddScoped<
    IDriverAccountService,
    DriverAccountService>();



builder.Services.AddScoped<
    IAdminAccountService,
    AdminAccountService>();

builder.Services.AddScoped<
    ICompanyProfileService,
    CompanyProfileService>();

builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddSingleton<PdfContractTemplateRenderer>();
builder.Services.AddScoped<IContractDocumentService, ContractDocumentService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet(
    "/account/logout",
    async (
        SignInManager<ApplicationUser> signInManager,
        string? returnUrl) =>
    {
        await signInManager.SignOutAsync();

        var safeReturnUrl =
            string.IsNullOrWhiteSpace(returnUrl)
                ? "/account/login"
                : returnUrl;

        // Không cho redirect ra ngoài website.
        if (!safeReturnUrl.StartsWith('/'))
        {
            safeReturnUrl = "/account/login";
        }

        return Results.Redirect(safeReturnUrl);
    })
    .RequireAuthorization("PasswordChangeAllowed");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAccountEndpoints();
await DatabaseSeeder.SeedAsync(app.Services);
app.Run();
/*
## User secrets

```bash
dotnet user-secrets init --project src/HTX586CONTRACT.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/HTX586CONTRACT.Web
```

## Migration và chạy

```bash
dotnet clean
dotnet restore
dotnet build
dotnet ef migrations add Init --project src/ContractManagement.Infrastructure --startup-project src/HTX586CONTRACT.Web
dotnet ef database update --project src/ContractManagement.Infrastructure --startup-project src/HTX586CONTRACT.Web
dotnet ef database update --project src\ContractManagement.Infrastructure\ContractManagement.Infrastructure.csproj --startup-project src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj --context ApplicationDbContext
dotnet watch run /a --project src/HTX586CONTRACT.Web
```
*/
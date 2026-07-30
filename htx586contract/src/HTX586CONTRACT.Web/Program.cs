using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using HTX586CONTRACT.Infrastructure.Services;
using HTX586CONTRACT.Web.Components;
using HTX586CONTRACT.Web.Endpoints;
using HTX586CONTRACT.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 256 * 1024;
    });

builder.Services.AddMudServices();

//Default Vps
var connectionString = builder.Configuration.GetConnectionString("Vps");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:Vps. Hãy dùng appsettings.json, user-secrets hoặc biến môi trường ConnectionStrings__Vps.");
}

var useForwardedHeaders = !builder.Environment.IsDevelopment();
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "HTX586CONTRACT.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";

    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
});

builder.Services
    .AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("PasswordChangeAllowed", policy =>
        policy.RequireAuthenticatedUser());

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IDriverAccountService, DriverAccountService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddSingleton<IUploadFileStorage, LocalUploadFileStorage>();
builder.Services.AddSingleton<PdfContractTemplateRenderer>();
builder.Services.AddScoped<MasterSignatureService>();
builder.Services.AddScoped<DriverRegistrationNotificationState>();
builder.Services.AddScoped<IContractDocumentService, ContractDocumentService>();

var dataRootPath = StoragePathResolver.ResolveDataRootPath(
    builder.Environment.ContentRootPath);
var dataProtectionKeysPath = StoragePathResolver.ResolveDataProtectionKeysPath(
    builder.Environment.ContentRootPath);

Directory.CreateDirectory(dataRootPath);
Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services
    .AddDataProtection()
    .SetApplicationName("HTX586CONTRACT")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

var app = builder.Build();

app.Logger.LogInformation(
    "HTX586CONTRACT DataRoot: {DataRootPath}",
    dataRootPath);

if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error/500");
    app.UseHsts();
}

app.UseStatusCodePages(async context =>
{
    var request = context.HttpContext.Request;
    var response = context.HttpContext.Response;
    var statusCode = response.StatusCode;

    if (statusCode is not (401 or 403 or 404 or 408 or 500 or 503) ||
        (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)) ||
        request.Path.StartsWithSegments("/error") ||
        request.Path.StartsWithSegments("/account/access-denied"))
    {
        return;
    }

    var acceptsHtml =
        request.Headers.Accept.Count == 0 ||
        request.Headers.Accept.Any(value =>
            value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);

    if (!acceptsHtml)
    {
        return;
    }

    var originalUrl = string.Concat(
        request.PathBase.Value,
        request.Path.Value,
        request.QueryString.Value);

    response.Redirect(
        $"/error/{statusCode}?ReturnUrl={Uri.EscapeDataString(originalUrl)}");

    await Task.CompletedTask;
});

app.UseHttpsRedirection();
app.UseStaticFiles();

var uploadRootPath = StoragePathResolver.ResolveUploadRootPath(
    app.Environment.ContentRootPath);
Directory.CreateDirectory(uploadRootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadRootPath),
    RequestPath = StoragePathResolver.PublicUploadPath
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet(
        "/account/logout",
        async (SignInManager<ApplicationUser> signInManager, string? returnUrl) =>
        {
            await signInManager.SignOutAsync();

            var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) &&
                                returnUrl.StartsWith('/') &&
                                !returnUrl.StartsWith("//") &&
                                !returnUrl.StartsWith("/\\")
                ? returnUrl
                : "/account/login";

            return Results.Redirect(safeReturnUrl);
        })
    .RequireAuthorization("PasswordChangeAllowed");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAccountEndpoints();
await DatabaseSeeder.InitializeAsync(app.Services);
app.Run();

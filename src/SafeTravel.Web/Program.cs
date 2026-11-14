using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;
using SafeTravel.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ----- database: SQL Server 2019 in production, SQLite only for automated tests and quick local runs -----
// The provider is read when the context is first built, so hosting and test overrides are honoured.
builder.Services.AddDbContextPool<AppDbContext>((sp, o) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["Database:Provider"] ?? "SqlServer";
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) 
    {
        // A SQL Server connection string means nothing to SQLite, so the quick local run falls back to a file.
        var cs = config.GetConnectionString("Default");
        o.UseSqlite(cs is null || cs.Contains("Server=", StringComparison.OrdinalIgnoreCase) ? "Data Source=safetravel.db" : cs);
    }
    else o.UseSqlServer(config.GetConnectionString("Default"), sql => sql.UseCompatibilityLevel(150).MigrationsAssembly("SafeTravel.Web"));
});

// ----- sign-in -----
builder.Services.AddIdentityCore<AppUser>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsFactory>();

builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("Set Jwt:Key to a secret of at least 32 characters (environment variable Jwt__Key).");
    jwt.Key = "development-only-key-change-me-0123456789";
    builder.Services.PostConfigure<JwtOptions>(o => o.Key = jwt.Key);
}

const string Smart = "Smart";
builder.Services.AddAuthentication(o => { o.DefaultScheme = Smart; o.DefaultChallengeScheme = Smart; })
    .AddPolicyScheme(Smart, Smart, o => o.ForwardDefaultSelector = ctx =>
        ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? JwtBearerDefaults.AuthenticationScheme : IdentityConstants.ApplicationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateIssuerSigningKey = true, ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name, RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = ".SafeTravel.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/denied";
    // The API answers with a status code. Only pages redirect to the sign-in screen.
    o.Events.OnRedirectToLogin = ctx => { if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 401; else ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 403; else ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask; };
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Staff", p => p.RequireRole(Roles.Staff))
    .AddPolicy("Suppliers", p => p.RequireRole(Roles.SuperAdmin, Roles.SupplierManager))
    .AddPolicy("SuppliersRead", p => p.RequireRole(Roles.SuperAdmin, Roles.SupplierManager, Roles.CallCentreAgent, Roles.FinanceOfficer))
    .AddPolicy("Content", p => p.RequireRole(Roles.SuperAdmin, Roles.ContentReviewer))
    .AddPolicy("Alerts", p => p.RequireRole(Roles.SuperAdmin, Roles.AlertsOfficer))
    .AddPolicy("Finance", p => p.RequireRole(Roles.SuperAdmin, Roles.FinanceOfficer))
    .AddPolicy("Admin", p => p.RequireRole(Roles.SuperAdmin))
    .AddPolicy("Supplier", p => p.RequireRole(Roles.Supplier).RequireClaim("supplier_id"));

builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = ".SafeTravel.Csrf"; o.Cookie.SameSite = SameSiteMode.Strict; });

// ----- application services -----
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
// Pages carry Sinhala and Tamil text. Send those letters as they are, not as numeric character codes.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<INotificationSender, LogNotificationSender>();
builder.Services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
builder.Services.AddScoped<AuditService>().AddScoped<SettingsService>().AddScoped<ListingService>().AddScoped<ProductService>().AddScoped<SupplierService>()
    .AddScoped<ContentService>().AddScoped<SafetyContentService>().AddScoped<MapService>().AddScoped<TravellerService>().AddScoped<PartnerService>().AddSingleton<Texts>().AddScoped<ReviewService>().AddScoped<AlertService>().AddScoped<PaymentService>().AddScoped<DashboardService>()
    .AddScoped<TokenService>().AddScoped<SafeTravel.Web.Api.SignInChecker>().AddScoped<Seeder>().AddScoped<PortalData>().AddSingleton<UploadStore>().AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddHostedService<RenewalWorker>();

// ----- web: MVC pages, REST API, caching, limits -----
builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddOutputCache(o =>
{
    // Public reads are cached for a short time so alert peaks are served from memory, not the database.
    o.AddPolicy("content", p => p.Expire(TimeSpan.FromSeconds(60)).Tag(CacheTags.Content));
    o.AddPolicy("listings", p => p.Expire(TimeSpan.FromSeconds(60)).SetVaryByQuery("*").Tag(CacheTags.Listings));
    o.AddPolicy("alerts", p => p.Expire(TimeSpan.FromSeconds(15)).Tag(CacheTags.Alerts));
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c => !c.Request.Path.StartsWithSegments("/api")
        ? RateLimitPartition.GetNoLimiter("pages")
        : RateLimitPartition.GetSlidingWindowLimiter(Ip(c), _ => new SlidingWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:ApiPerMinute", 300), Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6 }));
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:AuthPerMinute", 10), Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("write", c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimits:PublicWritesPerMinute", 20), Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 6 * 1024 * 1024);

var app = builder.Build();

app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        RuleException r => (r.Status, r.Message),
        DbUpdateConcurrencyException => (409, "Someone else changed this at the same time. Reload and try again."),
        _ => (500, "Something went wrong on our side. Try again in a moment.")
    };
    ctx.Response.StatusCode = status;
    if (ctx.Request.Path.StartsWithSegments("/api")) await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title }, options: null, contentType: "application/problem+json");
    else await ctx.Response.WriteAsync(title);
}));
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) { app.UseHsts(); app.UseHttpsRedirection(); }

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "public,max-age=604800" });
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health", async (AppDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));
app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// ----- create or update the database, then seed -----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsSqlite()) await db.Database.EnsureCreatedAsync();
    else if (config.GetValue("Database:MigrateOnStart", true)) await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<Seeder>().RunAsync(config["Seed:StaffPassword"], config.GetValue("Seed:SampleData", false));
}

app.Run();

public partial class Program;

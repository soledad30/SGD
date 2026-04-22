using GestorDocumentoApp.Data;
using GestorDocumentoApp.Migrations;
using GestorDocumentoApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.

builder.Services.AddDbContext<ScmDocumentContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddIdentity<IdentityUser,IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = true;
})
    .AddEntityFrameworkStores<ScmDocumentContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddScoped<GithubService>();
builder.Services.AddScoped<ChangeRequestLifecycleService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization(opt =>
{
    opt.FallbackPolicy = opt.DefaultPolicy;
});
builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.ContentType = "text/plain";
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again in a moment.");
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var userId = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;
        var key = userId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RequestPipeline");
var githubToken = app.Configuration["GitHub:Token"];
var githubWebhookSecret = app.Configuration["GitHub:WebhookSecret"];

if (string.IsNullOrWhiteSpace(githubToken))
{
    logger.LogWarning("GitHub token is not configured. Repository/commit/PR validation may fail for private repositories or rate-limited requests.");
}

if (string.IsNullOrWhiteSpace(githubWebhookSecret))
{
    logger.LogWarning("GitHub webhook secret is not configured. Webhook signature validation will reject all requests.");
}


using (var scope = app.Services.CreateScope())
{
    var services= scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManage = services.GetRequiredService<RoleManager<IdentityRole>>();
    var dbContext = services.GetRequiredService<ScmDocumentContext>();
    await DbSeeder.SeedAsync(userManager, roleManage, dbContext, app.Configuration);
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error is not null)
            {
                logger.LogError(
                    exceptionFeature.Error,
                    "Unhandled exception. Path: {Path}. TraceId: {TraceId}",
                    exceptionFeature.Path,
                    context.TraceIdentifier);
            }

            context.Response.Redirect("/Home/Error");
            await Task.CompletedTask;
        });
    });
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error is not null)
            {
                logger.LogError(
                    exceptionFeature.Error,
                    "Unhandled exception (development). Path: {Path}. TraceId: {TraceId}",
                    exceptionFeature.Path,
                    context.TraceIdentifier);
            }

            context.Response.Redirect("/Home/Error");
            await Task.CompletedTask;
        });
    });
}

app.UseCors(opt =>
{
    opt.AllowAnyMethod();
    opt.WithOrigins(["http://127.0.0.1:3000"]);
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();
        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms. TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            context.TraceIdentifier);
    }
});

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();


app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapHealthChecks("/health");


app.Run();

public partial class Program;

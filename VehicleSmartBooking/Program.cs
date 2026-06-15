using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Options;
using VehicleBooking.Web.Domain.Options;
using VehicleBooking.Web.Domain.Services;
using VehicleSmartBooking.Features.Dashboard.Queries;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<VehicleBookingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login"; // default ����ѧ��� login
        options.AccessDeniedPath = "/home/notpermission";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Cookie.Name = "VSB.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // dev ok
    });

builder.Services.Configure<SsoOptions>(
    builder.Configuration.GetSection("Sso")
);

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("SmtpSettings")
);

builder.Services.Configure<EmailNotificationOptions>(
    builder.Configuration.GetSection("EmailNotification")
);

builder.Services.Configure<VapidOptions>(
    builder.Configuration.GetSection("Vapid")
);

// removed ISsoClient registration
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IDriverWorkflowService, DriverWorkflowService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddScoped<ApprovalChainBuilder>();
builder.Services.AddScoped<IDashboardQueryService, DashboardQueryService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IWebPushSender, WebPushSender>();
builder.Services.AddScoped<IDriverBookingNotificationService, DriverBookingNotificationService>();

// IHttpContextAccessor needed for CurrentUserService to read session
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VehicleBookingDbContext>();
    await DbSeeder.SeedDevAsync(db);

    // Warn if VAPID keys are missing and log generated keys as a convenience
    var vapid = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<VapidOptions>>().Value;
    if (!vapid.IsConfigured)
    {
        var startupLog = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        startupLog.LogWarning("VAPID keys not configured — Web Push is disabled. Add keys to appsettings.json under \"Vapid\".");
        try
        {
            var keys = WebPush.VapidHelper.GenerateVapidKeys();
            startupLog.LogWarning("Generated VAPID keys (copy to appsettings.json and keep PrivateKey secret):");
            startupLog.LogWarning("  \"Vapid:Subject\": \"mailto:admin@yourdomain.com\"");
            startupLog.LogWarning("  \"Vapid:PublicKey\": \"{PublicKey}\"", keys.PublicKey);
            startupLog.LogWarning("  \"Vapid:PrivateKey\": \"{PrivateKey}\"", keys.PrivateKey);
        }
        catch { /* non-fatal */ }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable session before authentication so controllers/middleware can access session data
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}")
;


app.Run();

using InventoryErp.Application;
using InventoryErp.Application.Interfaces;
using InventoryErp.Infrastructure;
using InventoryErp.Infrastructure.Identity;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Seeding;
using InventoryErp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Implemented here rather than in Infrastructure because it writes into wwwroot, which only
// the web project knows about.
builder.Services.AddScoped<ILogoStorage, LogoStorage>();

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<InventoryErpDbContext>()
    .AddDefaultTokenProviders();

// The default Identity UI is deliberately not registered: it would expose Register, forgot-password
// and account-management pages, which are out of scope. AccountController serves login/logout only.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Everything requires an authenticated user unless it opts out with [AllowAnonymous].
// A fallback policy is safer than per-controller [Authorize]: a new controller is protected by
// default rather than protected only if someone remembers the attribute.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Apply pending migrations, then seed sample data on first run. The seeder is a no-op once the
// database holds a company, so this is safe on every startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InventoryErpDbContext>();
    await context.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// AllowAnonymous is required: static asset endpoints carry no authorization metadata, so the
// global FallbackPolicy would otherwise apply to them and redirect every CSS/JS request to the
// login page — serving HTML where the browser expects a stylesheet.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

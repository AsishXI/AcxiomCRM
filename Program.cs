using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
    builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Identity/Account/Login";
    o.AccessDeniedPath = "/Identity/Account/AccessDenied";
    o.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 401;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 403;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AcxiomCRM.Services.AuditService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    await next();
    if (ctx.Request.Method != "POST") return;
    var path = ctx.Request.Path.Value ?? "";
    bool isLogin = path.Equals("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase);
    bool isLogout = path.Equals("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase);
    if (!isLogin && !isLogout) return;

    string who = isLogin && ctx.Request.HasFormContentType ? ctx.Request.Form["Input.Email"].ToString() : (ctx.User.Identity?.Name ?? "");
    bool ok = ctx.Response.StatusCode == 302;
    var db = ctx.RequestServices.GetRequiredService<ApplicationDbContext>();
    db.AuditLogs.Add(new AcxiomCRM.Models.AuditLog
    {
        UserName = who,
        Action = isLogout ? "Logout" : ok ? "Login" : "FailedLogin",
        EntityName = "Account",
        NewValue = isLogout ? "Signed out" : ok ? "Success" : "Invalid credentials or lockout",
        IpAddress = ctx.Connection.RemoteIpAddress?.ToString()
    });
    await db.SaveChangesAsync();
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    sp.GetRequiredService<ApplicationDbContext>().Database.Migrate();
    var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = sp.GetRequiredService<UserManager<IdentityUser>>();

    foreach (var r in new[] { "Admin", "Manager", "SalesExecutive" })
        if (!await roleMgr.RoleExistsAsync(r)) await roleMgr.CreateAsync(new IdentityRole(r));

    async Task SeedUser(string email, string role)
    {
        if (await userMgr.FindByEmailAsync(email) != null) return;
        var u = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        if ((await userMgr.CreateAsync(u, "Test@1234")).Succeeded)
            await userMgr.AddToRoleAsync(u, role);
    }
    await SeedUser("admin@acxiom.com", "Admin");
    await SeedUser("manager@acxiom.com", "Manager");
    await SeedUser("sales@acxiom.com", "SalesExecutive");
}
app.Run();

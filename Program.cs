using App_thi_tin_hoc.Data;
using App_thi_tin_hoc.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register DbContext with SQL Server Connection String
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register JudgingService
builder.Services.AddScoped<JudgingService>();

// Add Cookie Authentication
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath = "/Home/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        DbInitializer.Initialize(context);

        // Repair double-encoded contest titles/descriptions
        var contests = context.Contests.ToList();
        bool changed = false;
        foreach (var c in contests)
        {
            if (c.Description.Contains("&#") || c.Description.Contains("&amp;"))
            {
                c.Description = System.Net.WebUtility.HtmlDecode(System.Net.WebUtility.HtmlDecode(c.Description));
                changed = true;
            }
            if (c.Title.Contains("&#") || c.Title.Contains("&amp;"))
            {
                c.Title = System.Net.WebUtility.HtmlDecode(System.Net.WebUtility.HtmlDecode(c.Title));
                changed = true;
            }
        }
        if (changed)
        {
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Session
app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

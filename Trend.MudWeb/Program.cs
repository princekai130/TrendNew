using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using Trend.MudWeb;
using Trend.MudWeb.Components;
using Trend.MudWeb.Models;
using Trend.MudWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfigurasi DbContext SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TrendContext>(options =>
    options.UseSqlite(connectionString));

// --- REGISTRASI SERVICES BERDASARKAN CLASS DIAGRAM LAPORAN ---

// 2. Registrasi Repository
builder.Services.AddHttpClient<ScrapingService>();
builder.Services.AddScoped<TrendRepo>();
builder.Services.AddScoped<TrendAnalyzer>();
builder.Services.AddScoped<Report>();
builder.Services.AddScoped<Trend.MudWeb.Services.Notifications>();

// 3. Tambahkan layanan MudBlazor
builder.Services.AddMudServices(config => {
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
});

// 4. Registrasi Konfigurasi API (dari appsettings.json)
builder.Services.Configure<ScraperSettings>(builder.Configuration.GetSection("ScraperSettings"));

builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Konfigurasi Autentikasi & Otorisasi
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/api/auth/logout";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PremiumOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.HasClaim("SubscriptionStatus", "Premium")
        ));
});

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<CustomAuthStateProvider>(sp =>
    (CustomAuthStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PremiumOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.HasClaim("SubscriptionStatus", "Premium")
        ));
});
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// PENTING: UseRouting HARUS sebelum UseAuthentication
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// CRITICAL FIX: Mapping endpoints
// JANGAN panggil app.MapBlazorHub() secara terpisah!
// AddInteractiveServerRenderMode() sudah otomatis memanggil MapBlazorHub
app.MapControllers();

app.MapStaticAssets();

// PERBAIKAN: Hanya panggil MapRazorComponents SATU KALI
// Jangan ada app.MapBlazorHub() lagi setelah ini!
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
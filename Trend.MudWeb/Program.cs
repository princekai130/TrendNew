using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Trend.MudWeb;
using Trend.MudWeb.Components;
using Trend.MudWeb.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfigurasi DbContext SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TrendContext>(options =>
    options.UseSqlite(connectionString));

// 2. Registrasi Repository sebagai Scoped
builder.Services.AddScoped<TrendRepo>();

// 3. Tambahkan layanan MudBlazor
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Tambahkan di Program.cs
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.Cookie.Name = "Trendz_Auth";
        options.LoginPath = "/login"; // Halaman jika belum login
        options.AccessDeniedPath = "/access-denied"; // Halaman jika user Free buka fitur Premium
    });

builder.Services.AddCascadingAuthenticationState(); // Penting untuk Blazor
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PremiumOnly", policy => policy.RequireClaim("SubscriptionStatus", "Premium"));
});

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapControllers();
app.MapBlazorHub();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

using Blazored.LocalStorage;
using MudBlazor.Services;
using FourNationsFantasy.Components;
using FourNationsFantasy.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Nhl.Api;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("ConnectionString__FNF");
var sbUrl = Environment.GetEnvironmentVariable("SupabaseUrl__FNF");
var sbKey = Environment.GetEnvironmentVariable("SupabaseKey__FNF");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddScoped<IFNFData>(provider =>
{
    return new FNFData(connectionString, provider);
});

builder.Services.AddScoped<CustomUserService>(provider =>
{
    var localStorageService = provider.GetRequiredService<ILocalStorageService>();
    var fnfData = provider.GetRequiredService<IFNFData>();
    return new CustomUserService(localStorageService, fnfData, sbUrl!, sbKey!);
});
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<CustomAuthenticationStateProvider>());

builder.Services.AddSingleton<INhlApi, NhlApi>();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

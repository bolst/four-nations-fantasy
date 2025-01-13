using Blazored.LocalStorage;
using MudBlazor.Services;
using FourNationsFantasy.Components;
using FourNationsFantasy.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("ConnectionString__FNF");
var sbUrl = Environment.GetEnvironmentVariable("SupabaseUrl__FNF");
var sbKey = Environment.GetEnvironmentVariable("SupabaseKey__FNF");

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddScoped<IFNFData>(provider =>
{
    return new FNFData(connectionString, provider);
});

builder.Services.AddScoped<CustomUserService>(provider =>
{
    var localStorageService = provider.GetRequiredService<ILocalStorageService>();
    return new CustomUserService(localStorageService, sbUrl!, sbKey!);
});

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

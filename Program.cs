using RentLog.Components;
using RentLog.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 16 * 1024 * 1024);

// Add authentication services
builder.Services.AddScoped<IAuthService, MockAuthService>();
builder.Services.AddScoped<IIssueService, MockIssueService>();
builder.Services.AddScoped<IRentService, MockRentService>();
builder.Services.AddScoped<IMediaService, MockMediaService>();
builder.Services.AddScoped<RentLogAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<RentLogAuthStateProvider>());
builder.Services.AddAuthenticationCore();
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

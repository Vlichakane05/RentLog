using RentLog.Components;
using RentLog.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "https://lwtunhohgvpvuimwdmja.supabase.co";
var supabaseAnonKey = builder.Configuration["Supabase:AnonKey"] ?? "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imx3dHVuaG9oZ3ZwdnVpbXdkbWphIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTEyOTY3NDQsImV4cCI6MjEwNjg3Mjc0NH0.5KvVyw19C0e-B3jye1gSbonvS_-O9DD9BqtpGEGFLc8";

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 72 * 1024 * 1024);

builder.Services.AddHttpClient();
builder.Services.AddSingleton(new BackendMode(false, null));
builder.Services.AddScoped<IEmailNotificationService, ResendEmailNotificationService>();

// Direct live Supabase database and authentication services
builder.Services.AddScoped(_ => new SupabaseClientProvider(supabaseUrl, supabaseAnonKey));
builder.Services.AddScoped<IPropertyManagementService, SupabasePropertyManagementService>();
builder.Services.AddScoped<IAuthService, SupabaseAuthService>();
builder.Services.AddScoped<IIssueService, SupabaseIssueService>();
builder.Services.AddScoped<IRentService, SupabaseRentService>();
builder.Services.AddScoped<IPropertyService, SupabasePropertyService>();
builder.Services.AddScoped<ITenancyService, SupabaseTenancyService>();
builder.Services.AddScoped<SupabaseMediaService>();
builder.Services.AddScoped<IMediaService>(services => services.GetRequiredService<SupabaseMediaService>());
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

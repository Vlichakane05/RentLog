using RentLog.Components;
using RentLog.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

var supabaseUrl = builder.Configuration["Supabase:Url"] ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabaseAnonKey = builder.Configuration["Supabase:AnonKey"] ?? Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
var hasValidSupabaseUrl = Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var parsedSupabaseUrl) &&
    (parsedSupabaseUrl.Scheme == Uri.UriSchemeHttps ||
     (builder.Environment.IsDevelopment() && parsedSupabaseUrl.IsLoopback && parsedSupabaseUrl.Scheme == Uri.UriSchemeHttp));
var hasSupabaseKey = !string.IsNullOrWhiteSpace(supabaseAnonKey);
var useMockServices = !hasValidSupabaseUrl || !hasSupabaseKey;
if (useMockServices && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Supabase configuration is required outside Development. Configure Supabase:Url and Supabase:AnonKey using environment variables or a secret store.");
}

var setupMessage = useMockServices
    ? "Supabase settings are missing or invalid. Rent Log is running with demo data; configure Supabase:Url and Supabase:AnonKey to use your Supabase project."
    : null;
if (useMockServices)
{
    Console.WriteLine($"Development mode: {setupMessage}");
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 72 * 1024 * 1024);

builder.Services.AddSingleton(new BackendMode(useMockServices, setupMessage));
if (useMockServices)
{
    builder.Services.AddScoped<IAuthService, MockAuthService>();
    builder.Services.AddScoped<MockIssueService>();
    builder.Services.AddSingleton<MockLandlordContractorManagementService>();
    builder.Services.AddScoped<MockContractorService>();
    builder.Services.AddScoped<IIssueService>(services => services.GetRequiredService<MockIssueService>());
    builder.Services.AddScoped<ILandlordContractorJobsService>(services => services.GetRequiredService<MockIssueService>());
    builder.Services.AddScoped<IRentService, MockRentService>();
    builder.Services.AddScoped<IPropertyService, MockPropertyService>();
    builder.Services.AddScoped<ITenancyService, MockTenancyService>();
    builder.Services.AddScoped<IMediaService, MockMediaService>();
    builder.Services.AddScoped<IRegistrationService, MockRegistrationService>();
    builder.Services.AddScoped<ILandlordContractorManagementService>(
        services => services.GetRequiredService<MockLandlordContractorManagementService>());
    builder.Services.AddScoped<IContractorService>(
        services => services.GetRequiredService<MockContractorService>());
}
else
{
    builder.Services.AddScoped(_ => new SupabaseClientProvider(parsedSupabaseUrl!.ToString(), supabaseAnonKey!));
    builder.Services.AddScoped<IAuthService, SupabaseAuthService>();
    builder.Services.AddScoped<IIssueService, SupabaseIssueService>();
    builder.Services.AddScoped<IRentService, SupabaseRentService>();
    builder.Services.AddScoped<IPropertyService, SupabasePropertyService>();
    builder.Services.AddScoped<ITenancyService, SupabaseTenancyService>();
    builder.Services.AddScoped<IRegistrationService, SupabaseRegistrationService>();
    builder.Services.AddScoped<SupabaseContractorService>();
    builder.Services.AddScoped<IContractorService>(
        services => services.GetRequiredService<SupabaseContractorService>());
    builder.Services.AddScoped<ILandlordContractorJobsService>(
        services => services.GetRequiredService<SupabaseContractorService>());
    builder.Services.AddScoped<ILandlordContractorManagementService>(
        services => services.GetRequiredService<IContractorService>());
    builder.Services.AddScoped<SupabaseMediaService>();
    builder.Services.AddScoped<IMediaService>(services => services.GetRequiredService<SupabaseMediaService>());
}
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
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting.WindowsServices;

using Serilog;

using StorageServer;
using StorageServer.Components;
using StorageServer.Endpoints.Api;
using StorageServer.Endpoints.S3;
using StorageServer.Middleware;
using StorageServer.Storage;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
});

// Path
builder.Configuration.SetBasePath(AppContext.BaseDirectory);

// Service
builder.Host
    .UseWindowsService()
    .UseSystemd();

// Allow large file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = Int64.MaxValue;
});

// Logging
builder.Logging.ClearProviders();
builder.Services.AddSerilog(options => options.ReadFrom.Configuration(builder.Configuration));

// Storage service
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IStorageService, StorageService>();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API
builder.Services.AddProblemDetails();

//--------------------------------------------------------------------------------
// Configure request pipeline.
//--------------------------------------------------------------------------------
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseWhen(
        static c => c.Request.Path.StartsWithSegments("/storage/", StringComparison.OrdinalIgnoreCase) ||
                    c.Request.Path.StartsWithSegments("/api/", StringComparison.OrdinalIgnoreCase),
        static b => b.UseExceptionHandler(),
        static b => b.UseExceptionHandler("/error", createScopeForErrors: true));
}

// URL rewriting must run before routing
app.UseMiddleware<VirtualHostStyleMiddleware>();

// Routing
app.UseRouting();

// S3 request-id, CORS, and exception handling
app.UseMiddleware<S3Middleware>();

// End point
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();

// S3 API: /storage/*
app.MapS3Endpoints();
// Web UI API: /api/*
app.MapFileEndpoints();
app.MapVersionEndpoints();

// Blazor pages
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Startup log
app.Logger.InfoServiceStart();
app.Logger.InfoServiceSettingsRuntime(RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
app.Logger.InfoServiceSettingsEnvironment(typeof(Program).Assembly.GetName().Version, Environment.CurrentDirectory);
app.Logger.InfoServiceSettingsGC(GCSettings.IsServerGC, GCSettings.LatencyMode, GCSettings.LargeObjectHeapCompactionMode);

app.Run();

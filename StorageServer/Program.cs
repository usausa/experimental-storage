using StorageServer.Api.S3;
using StorageServer.Api.Web;
using StorageServer.Components;
using StorageServer.Middleware;
using StorageServer.Storage;

var builder = WebApplication.CreateBuilder(args);

// Storage service
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IStorageService, StorageService>();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// URL rewriting must run before routing
app.UseMiddleware<VirtualHostStyleMiddleware>();
app.UseRouting();

// S3 request-id, CORS, and exception handling (unified)
app.UseMiddleware<S3Middleware>();

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

app.Run();

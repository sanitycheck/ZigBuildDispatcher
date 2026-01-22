using ZigBuildDispatcher;
using ZigBuildDispatcher.Sample.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(new BuildDispatcherOptions
{
    MaxConcurrency = Math.Max(1, Environment.ProcessorCount),
    WorkspaceRoot = Path.Combine(Path.GetTempPath(), "zig-build-dispatcher-sample"),
    SharedGlobalCacheDir = Path.Combine(
        Path.GetTempPath(),
        "zig-build-dispatcher-sample",
        "zig-global-cache"),
    CleanupWorkspaceOnSuccess = true,
    CleanupWorkspaceOnFailure = true
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<BuildDispatcherOptions>();
    var rules = new[]
    {
        BuildArgumentSelectorRule.FileNamePrefix("-Dartifact="),
        BuildArgumentSelectorRule.ExtensionPrefix("-DartifactExt="),
        BuildArgumentSelectorRule.PatternPrefix("-DartifactPattern=")
    };

    return new BuildDispatcher(options, new BuildArgumentSelectorStrategy(rules));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

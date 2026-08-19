using InventoryApp.Client.Services;
using InventoryApp.Contracts.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;

namespace InventoryApp.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddMauiBlazorWebView();

        // Fluent UI Blazor: components, icons and emoji assets.
        builder.Services.AddFluentUIComponents(options =>
        {
            options.ValidateClassNames = false;
        });

        // Transport layer.
        builder.Services.AddSingleton<ITokenStore, SecureTokenStore>();
        builder.Services.AddSingleton<AuthInterceptor>();
        builder.Services.AddSingleton<GrpcChannelProvider>();
        builder.Services.AddSingleton<GrpcCallExecutor>();
        builder.Services.AddSingleton<LookupCache>();
        builder.Services.AddSingleton<InventoryApiClient>();

        // App services.
        builder.Services.AddSingleton<AppState>();
        builder.Services.AddSingleton<FileExportService>();

        // Authentication / authorization mirrored from the shared contract.
        builder.Services.AddSingleton<InventoryAuthStateProvider>();
        builder.Services.AddSingleton<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<InventoryAuthStateProvider>());
        builder.Services.AddAuthorizationCore(options => options.AddInventoryPolicies());
        builder.Services.AddCascadingAuthenticationState();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
#endif

        return builder.Build();
    }
}

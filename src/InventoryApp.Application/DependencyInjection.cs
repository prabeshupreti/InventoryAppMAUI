using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IStockLedger, StockLedger>();
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddScoped<ICategoryApplicationService, CategoryApplicationService>();
        services.AddScoped<ISupplierApplicationService, SupplierApplicationService>();
        services.AddScoped<IProductApplicationService, ProductApplicationService>();
        services.AddScoped<IInventoryApplicationService, InventoryApplicationService>();
        services.AddScoped<IPurchaseApplicationService, PurchaseApplicationService>();
        services.AddScoped<ISalesApplicationService, SalesApplicationService>();
        services.AddScoped<IReportApplicationService, ReportApplicationService>();

        return services;
    }
}

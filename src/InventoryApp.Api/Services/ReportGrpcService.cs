using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Reporting;
using InventoryApp.Contracts.Security;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class ReportGrpcService(IReportApplicationService service)
    : ReportService.ReportServiceBase
{
    [Authorize(Policy = Permissions.ViewDashboard)]
    public override Task<DashboardResponse> GetDashboard(Empty request, ServerCallContext context) =>
        service.GetDashboardAsync(context.CancellationToken);

    [Authorize(Policy = Permissions.ViewReports)]
    public override Task<InventoryValuationResponse> GetInventoryValuation(
        InventoryReportRequest request, ServerCallContext context) =>
        service.GetInventoryValuationAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewReports)]
    public override Task<InventoryValuationResponse> GetStockStatusReport(
        InventoryReportRequest request, ServerCallContext context) =>
        service.GetInventoryValuationAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewReports)]
    public override Task<TradeReportResponse> GetPurchaseReport(TradeReportRequest request, ServerCallContext context) =>
        service.GetPurchaseReportAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewReports)]
    public override Task<TradeReportResponse> GetSalesReport(TradeReportRequest request, ServerCallContext context) =>
        service.GetSalesReportAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ExportReports)]
    public override Task<CsvExport> ExportCsv(ExportRequest request, ServerCallContext context) =>
        service.ExportCsvAsync(request, context.CancellationToken);
}

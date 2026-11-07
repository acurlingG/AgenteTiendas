using MonitoreoTiendas.Api.Models;
using MonitoreoTiendas.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ReportStore>();

var app = builder.Build();

app.UsePathBase("/backend");

app.MapGet("/", () => "API de monitoreo Curlingtech");

app.MapGet("/api/health", () => Results.Ok(new
{
    Status = "OK",
    Service = "MonitoreoTiendas.Api"
}));

app.MapPost(
    "/api/reports",
    (AgentReportRequest report, ReportStore store) =>
    {
        if (string.IsNullOrWhiteSpace(report.StoreCode))
        {
            return Results.BadRequest(new
            {
                Message = "StoreCode es obligatorio."
            });
        }

        if (string.IsNullOrWhiteSpace(report.StoreName))
        {
            return Results.BadRequest(new
            {
                Message = "StoreName es obligatorio."
            });
        }

        if (report.Player is null)
        {
            return Results.BadRequest(new
            {
                Message = "Player es obligatorio."
            });
        }

        var savedReport = store.Save(report);

        return Results.Accepted(
            $"/api/stores/{Uri.EscapeDataString(report.StoreCode)}",
            new
            {
                Message = "Reporte recibido.",
                savedReport.ReceivedAt
            });
    });

app.MapGet(
    "/api/stores",
    (ReportStore store) => Results.Ok(store.GetAll()));

app.MapGet(
    "/api/stores/{code}",
    (string code, ReportStore store) =>
    {
        var report = store.GetByStoreCode(code);

        IResult result = report is null
            ? Results.NotFound(new
            {
                Message = $"No se encontró la tienda {code}."
            })
            : Results.Ok(report);

        return result;
    });

app.Run();
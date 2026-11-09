using System.Text;
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
    "/api/stores/compliance-report",
    (ReportStore storeRepository) =>
    {
        var builder = new StringBuilder();
        builder.AppendLine("FechaHora,CodigoTienda,NombreTienda,Pais,EstadoPlayer,Cancion,Artista,CumplimientoAutorizado,Detalles");

        foreach (var store in storeRepository.GetAll())
        {
            foreach (var log in store.AuditLogs)
            {
                var fechaHora = log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var storeCode = store.Report.StoreCode.Replace("\"", "\"\"");
                var storeName = store.Report.StoreName.Replace("\"", "\"\"");
                var pais = store.Report.Pais.Replace("\"", "\"\"");
                var state = log.State.Replace("\"", "\"\"");
                var mediaTitle = log.MediaTitle.Replace("\"", "\"\"");
                var mediaArtist = log.MediaArtist.Replace("\"", "\"\"");
                var autorizado = log.IsAuthorized ? "SI" : "NO";
                var details = log.Details.Replace("\"", "\"\"");

                builder.AppendLine($"\"{fechaHora}\",\"{storeCode}\",\"{storeName}\",\"{pais}\",\"{state}\",\"{mediaTitle}\",\"{mediaArtist}\",\"{autorizado}\",\"{details}\"");
            }
        }

        var csvBytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Results.File(
            csvBytes,
            "text/csv",
            $"Reporte_Cumplimiento_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    });

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
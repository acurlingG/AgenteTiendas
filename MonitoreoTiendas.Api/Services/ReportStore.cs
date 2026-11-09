using System.Collections.Concurrent;
using MonitoreoTiendas.Api.Models;

namespace MonitoreoTiendas.Api.Services;

public sealed class ReportStore
{
    private readonly ConcurrentDictionary<string, StoredAgentReport> _reports =
        new(StringComparer.OrdinalIgnoreCase);

    public StoredAgentReport Save(AgentReportRequest report)
    {
        var timestamp = DateTimeOffset.UtcNow;

        var storedReport = _reports.AddOrUpdate(
            report.StoreCode,
            code =>
            {
                var newReport = new StoredAgentReport(report, timestamp);
                newReport.RegisterAuditLog(report, timestamp);
                return newReport;
            },
            (code, existing) =>
            {
                existing.Update(report, timestamp);
                return existing;
            });

        return storedReport;
    }

    public IReadOnlyCollection<StoredAgentReport> GetAll()
    {
        return _reports.Values
            .OrderBy(item => item.Report.StoreName)
            .ToArray();
    }

    public StoredAgentReport? GetByStoreCode(string storeCode)
    {
        return _reports.TryGetValue(storeCode, out var report)
            ? report
            : null;
    }
}

public sealed class StoredAgentReport
{
    public AgentReportRequest Report { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public List<AuditLogEntry> AuditLogs { get; } = [];

    public StoredAgentReport(AgentReportRequest report, DateTimeOffset receivedAt)
    {
        Report = report;
        ReceivedAt = receivedAt;
    }

    public void Update(AgentReportRequest report, DateTimeOffset receivedAt)
    {
        Report = report;
        ReceivedAt = receivedAt;
        RegisterAuditLog(report, receivedAt);
    }

    public void RegisterAuditLog(AgentReportRequest report, DateTimeOffset timestamp)
    {
        bool isAuthorized = report.Player.IsOpen &&
                            report.Player.State == "Playing" &&
                            !report.Player.Details.Contains("ALERTA", StringComparison.OrdinalIgnoreCase);

        AuditLogs.Add(new AuditLogEntry(
            Timestamp: timestamp,
            State: report.Player.State,
            MediaTitle: report.Player.Title ?? "",
            MediaArtist: report.Player.Artist ?? "",
            IsAuthorized: isAuthorized,
            Details: report.Player.Details ?? ""
        ));

        // Mantener las últimas 500 lecturas por tienda para optimizar la memoria
        if (AuditLogs.Count > 500)
        {
            AuditLogs.RemoveAt(0);
        }
    }
}

public sealed record AuditLogEntry(
    DateTimeOffset Timestamp,
    string State,
    string MediaTitle,
    string MediaArtist,
    bool IsAuthorized,
    string Details
);
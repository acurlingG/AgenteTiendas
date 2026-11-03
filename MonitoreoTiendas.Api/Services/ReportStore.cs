using System.Collections.Concurrent;
using MonitoreoTiendas.Api.Models;

namespace MonitoreoTiendas.Api.Services;

public sealed class ReportStore
{
    private readonly ConcurrentDictionary<string, StoredAgentReport> _reports =
        new(StringComparer.OrdinalIgnoreCase);

    public StoredAgentReport Save(AgentReportRequest report)
    {
        var storedReport = new StoredAgentReport(
            Report: report,
            ReceivedAt: DateTimeOffset.UtcNow);

        _reports[report.StoreCode] = storedReport;

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
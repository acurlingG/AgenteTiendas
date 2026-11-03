namespace MonitoreoTiendas.Api.Models;

public sealed class AgentReportRequest
{
    public string StoreCode { get; init; } = "";
    public string StoreName { get; init; } = "";
    public string WindowsVersion { get; init; } = "";
    public string WindowsEdition { get; init; } = "";
    public PlayerReportRequest? Player { get; init; }
    public DateTimeOffset ReportedAt { get; init; }
}

public sealed class PlayerReportRequest
{
    public string PlayerName { get; init; } = "";
    public bool IsOpen { get; init; }
    public string State { get; init; } = "";
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Details { get; init; } = "";
}

public sealed record StoredAgentReport(
    AgentReportRequest Report,
    DateTimeOffset ReceivedAt);
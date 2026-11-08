namespace MonitoreoTiendas.Api.Models;

public sealed class AgentReportRequest
{
    public string StoreCode { get; init; } = "";
    public string StoreName { get; init; } = "";
    public bool ActivoMusica { get; init; } = true;
    public string WindowsVersion { get; init; } = "";
    public string WindowsEdition { get; init; } = "";
    public PlayerReportRequest? Player { get; init; }
    public MachineStatusRequest? Machine { get; init; }
    public NetworkStatusRequest? Network { get; init; }
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

public sealed class MachineStatusRequest
{
    public double? CpuUsagePercent { get; init; }
    public ulong? TotalMemoryBytes { get; init; }
    public ulong? AvailableMemoryBytes { get; init; }
    public List<DiskStatusRequest> Disks { get; init; } = [];
}

public sealed class DiskStatusRequest
{
    public string Drive { get; init; } = "";
    public ulong TotalBytes { get; init; }
    public ulong FreeBytes { get; init; }
}

public sealed class NetworkStatusRequest
{
    public string Target { get; init; } = "";
    public bool IsReachable { get; init; }
    public long? RoundtripTimeMs { get; init; }
    public string Details { get; init; } = "";
}

public sealed record StoredAgentReport(
    AgentReportRequest Report,
    DateTimeOffset ReceivedAt);
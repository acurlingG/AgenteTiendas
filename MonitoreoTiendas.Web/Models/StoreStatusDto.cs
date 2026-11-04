namespace MonitoreoTiendas.Web.Models;

public sealed class StoreStatusDto
{
    public AgentReportDto Report { get; set; } = new();
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class AgentReportDto
{
    public string StoreCode { get; set; } = "";
    public string StoreName { get; set; } = "";
    public string WindowsVersion { get; set; } = "";
    public string WindowsEdition { get; set; } = "";
    public PlayerStatusDto Player { get; set; } = new();
    public MachineStatusDto? Machine { get; set; }
    public NetworkStatusDto? Network { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
}



public sealed class PlayerStatusDto
{
    public string PlayerName { get; set; } = "";
    public bool IsOpen { get; set; }
    public string State { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Details { get; set; } = "";
}

public sealed class MachineStatusDto
{
    public double? CpuUsagePercent { get; set; }
    public ulong? TotalMemoryBytes { get; set; }
    public ulong? AvailableMemoryBytes { get; set; }
    public List<DiskStatusDto> Disks { get; set; } = [];
}

public sealed class DiskStatusDto
{
    public string Drive { get; set; } = "";
    public ulong TotalBytes { get; set; }
    public ulong FreeBytes { get; set; }
}

public sealed class NetworkStatusDto
{
    public string Target { get; set; } = "";
    public bool IsReachable { get; set; }
    public long? RoundtripTimeMs { get; set; }
    public string Details { get; set; } = "";
}
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
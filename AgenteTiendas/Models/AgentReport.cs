namespace AgenteTiendas.Models;

public sealed record AgentReport(
    string StoreCode,
    string StoreName,
    string WindowsVersion,
    string WindowsEdition,
    PlayerStatus Player,
    MachineStatus Machine,
    NetworkStatus Network,
    DateTimeOffset ReportedAt);
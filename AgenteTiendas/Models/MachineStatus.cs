namespace AgenteTiendas.Models;

public sealed record MachineStatus(
    double? CpuUsagePercent,
    ulong? TotalMemoryBytes,
    ulong? AvailableMemoryBytes,
    IReadOnlyList<DiskStatus> Disks);

public sealed record DiskStatus(
    string Drive,
    ulong TotalBytes,
    ulong FreeBytes);

public sealed record NetworkStatus(
    string Target,
    bool IsReachable,
    long? RoundtripTimeMs,
    string Details);
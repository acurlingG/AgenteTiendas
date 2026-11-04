using System.Runtime.InteropServices;
using AgenteTiendas.Models;

namespace AgenteTiendas.Services;

public sealed class SystemMetricsDetector
{
    public async Task<MachineStatus> DetectAsync()
    {
        var cpuTask = ReadCpuUsageAsync();
        var memory = ReadMemory();
        var disks = ReadDisks();

        var cpu = await cpuTask;

        return new MachineStatus(
            CpuUsagePercent: cpu,
            TotalMemoryBytes: memory.TotalBytes,
            AvailableMemoryBytes: memory.AvailableBytes,
            Disks: disks);
    }

    private static async Task<double?> ReadCpuUsageAsync()
    {
        if (!TryReadCpuTimes(out var idle1, out var kernel1, out var user1))
            return null;

        await Task.Delay(TimeSpan.FromSeconds(1));

        if (!TryReadCpuTimes(out var idle2, out var kernel2, out var user2))
            return null;

        var total1 = kernel1 + user1;
        var total2 = kernel2 + user2;
        var totalDelta = total2 - total1;
        var idleDelta = idle2 - idle1;

        if (totalDelta == 0 || idleDelta > totalDelta)
            return null;

        return Math.Round(
            (double)(totalDelta - idleDelta) / totalDelta * 100,
            1);
    }

    private static bool TryReadCpuTimes(
        out ulong idle,
        out ulong kernel,
        out ulong user)
    {
        idle = kernel = user = 0;

        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            return false;

        idle = idleTime.ToUInt64();
        kernel = kernelTime.ToUInt64();
        user = userTime.ToUInt64();

        return true;
    }

    private static (ulong? TotalBytes, ulong? AvailableBytes) ReadMemory()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
            return (null, null);

        return (status.TotalPhysical, status.AvailablePhysical);
    }

    private static IReadOnlyList<DiskStatus> ReadDisks()
    {
        var disks = new List<DiskStatus>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                    continue;

                disks.Add(new DiskStatus(
                    Drive: drive.Name,
                    TotalBytes: (ulong)drive.TotalSize,
                    FreeBytes: (ulong)drive.AvailableFreeSpace));
            }
            catch (IOException)
            {
                // La unidad pudo desconectarse durante la consulta.
            }
            catch (UnauthorizedAccessException)
            {
                // La cuenta del agente no puede consultar esta unidad.
            }
        }

        return disks;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idleTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
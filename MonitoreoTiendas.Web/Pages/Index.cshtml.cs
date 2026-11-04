using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitoreoTiendas.Web.Models;

namespace MonitoreoTiendas.Web.Pages;

public sealed class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<StoreStatusDto> Stores { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public async Task OnGetAsync()
    {
        UpdatedAt = DateTimeOffset.Now;

        try
        {
            var client = _httpClientFactory.CreateClient("MonitorApi");

            Stores = await client.GetFromJsonAsync<List<StoreStatusDto>>(
                "api/stores") ?? [];
        }
        catch (Exception)
        {
            ErrorMessage =
                "No se pudo consultar la API. Confirma que MonitoreoTiendas.Api esté ejecutándose.";
        }
    }

    public bool IsOffline(StoreStatusDto store)
    {
        return DateTimeOffset.UtcNow - store.ReceivedAt
            > TimeSpan.FromSeconds(90);
    }

    public string GetStatus(StoreStatusDto store)
    {
        if (IsOffline(store))
            return "Sin comunicación";

        if (!store.Report.Player.IsOpen)
            return "Reproductor cerrado";

        return store.Report.Player.State switch
        {
            "Playing" => "Reproduciendo",
            "NoSignal" => "Abierto sin señal",
            "NotFound" => "Reproductor no encontrado",
            _ => "Estado desconocido"
        };
    }

    public string GetCpuSummary(StoreStatusDto store) =>
    store.Report.Machine?.CpuUsagePercent is double cpu
        ? $"{cpu:0.0}%"
        : "N/D";

    public string GetMemorySummary(StoreStatusDto store)
    {
        var machine = store.Report.Machine;

        if (machine is null ||
            !machine.TotalMemoryBytes.HasValue ||
            machine.TotalMemoryBytes.Value == 0 ||
            !machine.AvailableMemoryBytes.HasValue)
        {
            return "N/D";
        }

        var total = machine.TotalMemoryBytes.Value;
        var used = total - Math.Min(total, machine.AvailableMemoryBytes.Value);
        var percent = (double)used / total * 100;

        return $"{percent:0}% ({ToGb(used):0.0}/{ToGb(total):0.0} GB)";
    }

    public string GetDiskSummary(StoreStatusDto store)
    {
        var disks = store.Report.Machine?.Disks;

        if (disks is null || disks.Count == 0)
            return "N/D";

        return string.Join(" · ", disks.Select(disk =>
            $"{disk.Drive}: {ToGb(disk.FreeBytes):0.0}/{ToGb(disk.TotalBytes):0.0} GB libres"));
    }

    public string GetNetworkSummary(StoreStatusDto store)
    {
        var network = store.Report.Network;

        if (network is null || string.IsNullOrWhiteSpace(network.Target))
            return "No configurado";

        return network.IsReachable
            ? $"OK · {network.Target} · {network.RoundtripTimeMs} ms"
            : $"FAILED · {network.Target}";
    }

    public string GetNetworkClass(StoreStatusDto store) =>
        store.Report.Network is null ||
        string.IsNullOrWhiteSpace(store.Report.Network.Target)
            ? "network-na"
            : store.Report.Network.IsReachable
                ? "network-ok"
                : "network-failed";

    private static double ToGb(ulong bytes) =>
        bytes / 1024d / 1024 / 1024;

    public string GetStatusClass(StoreStatusDto store)
    {
        if (IsOffline(store))
            return "offline";

        if (!store.Report.Player.IsOpen)
            return "closed";

        return store.Report.Player.State switch
        {
            "Playing" => "playing",
            "NoSignal" => "no-signal",
            _ => "unknown"
        };
    }



    public int PlayingCount =>
        Stores.Count(store =>
            !IsOffline(store) &&
            store.Report.Player.State == "Playing");

    public int NoSignalCount =>
        Stores.Count(store =>
            !IsOffline(store) &&
            store.Report.Player.IsOpen &&
            store.Report.Player.State == "NoSignal");

    public int OfflineCount =>
        Stores.Count(IsOffline);
}
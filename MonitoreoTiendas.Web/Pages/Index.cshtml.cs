using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitoreoTiendas.Web.Models;

namespace MonitoreoTiendas.Web.Pages;

public sealed class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    // Propiedad para la tarjeta de SLA Global
    public int ActiveMusicStores =>
        Stores.Count(store => store.Report.ActivoMusica);

    public double GlobalSlaPercent => ActiveMusicStores > 0
        ? Math.Round((double)PlayingCount / ActiveMusicStores * 100, 1)
        : 0;

    // Métricas para Auditoría de Cumplimiento Publicitario
    public double GetCompliancePercent(StoreStatusDto store)
    {
        if (!store.Report.ActivoMusica) return 100.0;

        if (!IsOffline(store) &&
            store.Report.Player.IsOpen &&
            store.Report.Player.State == "Playing" &&
            !store.Report.Player.Details.Contains("ALERTA", StringComparison.OrdinalIgnoreCase))
        {
            return 100.0;
        }

        return 0.0;
    }

    public double GlobalCompliancePercent
    {
        get
        {
            var activeStores = Stores.Where(s => s.Report.ActivoMusica).ToList();
            if (activeStores.Count == 0) return 100.0;

            var totalCompliance = activeStores.Sum(s => GetCompliancePercent(s));
            return Math.Round(totalCompliance / activeStores.Count, 1);
        }
    }

    // Lista dinámica de Alertas Preventivas
    public List<string> SystemAlerts
    {
        get
        {
            var alerts = new List<string>();
            foreach (var store in Stores)
            {
                var status = GetStatusClass(store);
                if (status == "offline")
                {
                    alerts.Add($"🔴 CRÍTICO | {store.Report.StoreName} ({store.Report.StoreCode}) — Sin comunicación desde hace más de 15 minutos. Posible caída de red o energía.");
                }
                else if (status == "closed")
                {
                    alerts.Add($"⚠️ ADVERTENCIA | {store.Report.StoreName} ({store.Report.StoreCode}) — El reproductor de medios se encuentra cerrado.");
                }
                else if (status == "not-authorized")
                {
                    alerts.Add($"🚨 CONTENIDO NO AUTORIZADO | {store.Report.StoreName} ({store.Report.StoreCode}) — Se está reproduciendo contenido fuera de la lista configurada. {store.Report.Player.Details}");
                }

                if (HasCpuWarning(store))
                {
                    alerts.Add($"⚠️ RECURSOS | {store.Report.StoreName} ({store.Report.StoreCode}) — CPU en nivel crítico ({store.Report.Machine?.CpuUsagePercent:F1}%). Riesgo de bloqueo.");
                }
                if (HasDiskWarning(store))
                {
                    alerts.Add($"⚠️ ALMACENAMIENTO | {store.Report.StoreName} ({store.Report.StoreCode}) — Disco principal con espacio bajo.");
                }
            }
            return alerts;
        }
    }

    // Evaluación de Umbrales
    public bool HasCpuWarning(StoreStatusDto store) =>
        store.Report.Machine?.CpuUsagePercent >= 90;

    public bool HasRamWarning(StoreStatusDto store)
    {
        var machine = store.Report.Machine;

        if (machine?.TotalMemoryBytes is not ulong total ||
            total == 0 ||
            machine.AvailableMemoryBytes is not ulong available)
        {
            return false;
        }

        var usedPercent = (double)(total - Math.Min(total, available)) / total * 100;
        return usedPercent >= 85;
    }

    public bool HasDiskWarning(StoreStatusDto store)
    {
        var driveC = store.Report.Machine?.Disks?
            .FirstOrDefault(disk => disk.Drive.StartsWith("C", StringComparison.OrdinalIgnoreCase));

        return driveC is not null &&
               driveC.TotalBytes > 0 &&
               (double)driveC.FreeBytes / driveC.TotalBytes < 0.15;
    }

    public IndexModel(
        IHttpClientFactory httpClientFactory,
        ILogger<IndexModel> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory
            ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public List<StoreStatusDto> Stores { get; private set; } = [];
    public List<string> AvailableCountries { get; private set; } = [];
    public string SelectedCountry { get; private set; } = "";
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public string ComplianceReportUrl =>
        new Uri(
            new Uri(_configuration["ApiSettings:BaseUrl"]
                ?? "http://localhost:5241/backend/"),
            "api/stores/compliance-report")
        .ToString();

    public async Task OnGetAsync()
    {
        UpdatedAt = DateTimeOffset.Now;

        try
        {
            var client = _httpClientFactory.CreateClient("MonitorApi");

            var allStores = await client.GetFromJsonAsync<List<StoreStatusDto>>(
                "api/stores") ?? [];

            AvailableCountries = allStores
                .Select(store => store.Report.Pais.Trim())
                .Where(country => !string.IsNullOrWhiteSpace(country))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(country => country)
                .ToList();

            SelectedCountry = Request.Query["pais"].ToString().Trim();
            if (SelectedCountry.Equals("Todos", StringComparison.OrdinalIgnoreCase))
                SelectedCountry = "";

            Stores = string.IsNullOrWhiteSpace(SelectedCountry)
                ? allStores
                : allStores
                    .Where(store => store.Report.Pais.Equals(
                        SelectedCountry,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo consultar la API de monitoreo.");
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

        if (!store.Report.ActivoMusica)
            return "No requiere monitoreo";

        if (!store.Report.Player.IsOpen)
            return "Reproductor cerrado";

        return store.Report.Player.State switch
        {
            "Playing" => "Reproduciendo",
            "NotAuthorized" => "Contenido no autorizado",
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

        if (IsOffline(store))
            return "SIN COMUNICACIÓN";

        if (network is null || string.IsNullOrWhiteSpace(network.Target))
            return "No configurado";

        return network.IsReachable
            ? $"OK · {network.Target} · {network.RoundtripTimeMs} ms"
            : $"FAILED · {network.Target}";
    }

    public string GetNetworkClass(StoreStatusDto store)
    {
        if (IsOffline(store))
            return "network-failed";

        var network = store.Report.Network;

        if (network is null || string.IsNullOrWhiteSpace(network.Target))
            return "network-na";

        return network.IsReachable ? "network-ok" : "network-failed";
    }

    private static double ToGb(ulong bytes) =>
        bytes / 1024d / 1024 / 1024;

    public string GetStatusClass(StoreStatusDto store)
    {
        if (IsOffline(store))
            return "offline";

        if (!store.Report.ActivoMusica)
            return "not-required";

        if (!store.Report.Player.IsOpen)
            return "closed";

        return store.Report.Player.State switch
        {
            "Playing" => "playing",
            "NotAuthorized" => "not-authorized",
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
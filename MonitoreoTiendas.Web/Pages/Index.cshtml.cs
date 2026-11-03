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
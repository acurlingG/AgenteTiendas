namespace AgenteTiendas.Models;

public sealed class StoreConfiguration
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Pais { get; set; } = "";
    public int IntervaloReporteSegundos { get; set; } = 30;
    public bool ActivoMusica { get; set; } = true;
    public string UrlApi { get; set; } = "http://localhost:5080";
    public string DestinoMonitoreoRed { get; set; } = "";
    public string RutaListaReproduccion { get; set; } = "";
}
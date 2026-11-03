namespace AgenteTiendas.Models;

public sealed class StoreConfiguration
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int IntervaloReporteSegundos { get; set; } = 30;
    public string UrlApi { get; set; } = "http://localhost:5080";
}
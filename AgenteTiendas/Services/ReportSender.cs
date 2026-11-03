using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenteTiendas.Models;

namespace AgenteTiendas.Services;

public sealed class ReportSender
{
    private readonly HttpClient _httpClient;
    private readonly Uri _reportEndpoint;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    public ReportSender(HttpClient httpClient, string apiUrl)
    {
        _httpClient = httpClient;

        var baseUrl = apiUrl.TrimEnd('/');
        _reportEndpoint = new Uri($"{baseUrl}/api/reports");
    }

    public async Task SendAsync(
        AgentReport report,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            _reportEndpoint,
            report,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
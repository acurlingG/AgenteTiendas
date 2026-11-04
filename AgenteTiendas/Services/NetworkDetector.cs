using System.Net.NetworkInformation;
using System.Net.Sockets;
using AgenteTiendas.Models;

namespace AgenteTiendas.Services;

public sealed class NetworkDetector
{
    private static readonly TimeSpan Timeout =
        TimeSpan.FromMilliseconds(1500);

    public async Task<NetworkStatus> CheckAsync(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new NetworkStatus(
                Target: "",
                IsReachable: false,
                RoundtripTimeMs: null,
                Details: "No se configuró DestinoMonitoreoRed.");
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target, Timeout);
            var isReachable = reply.Status == IPStatus.Success;

            return new NetworkStatus(
                Target: target,
                IsReachable: isReachable,
                RoundtripTimeMs: isReachable ? reply.RoundtripTime : null,
                Details: isReachable
                    ? "El destino respondió al ping."
                    : $"El destino no respondió: {reply.Status}.");
        }
        catch (Exception exception) when (
            exception is PingException or SocketException or ArgumentException)
        {
            return new NetworkStatus(
                Target: target,
                IsReachable: false,
                RoundtripTimeMs: null,
                Details: $"No se pudo consultar el destino: {exception.Message}");
        }
    }
}
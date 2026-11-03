using AgenteTiendas.Models;
using System.Diagnostics;

namespace AgenteTiendas.Services;

public sealed class MonitoringAgent
{
    private readonly StoreConfiguration _configuration;
    private readonly WindowsDetector _windowsDetector;
    private readonly PlayerDetector _playerDetector;
    private readonly ReportSender _reportSender;
    public MonitoringAgent(
        StoreConfiguration configuration,
        WindowsDetector windowsDetector,
        PlayerDetector playerDetector,
        ReportSender reportSender)
    {
        _configuration = configuration;
        _windowsDetector = windowsDetector;
        _playerDetector = playerDetector;
        _reportSender = reportSender;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"Agente iniciado para {_configuration.Nombre} " +
            $"({_configuration.Codigo}).");

        Console.WriteLine("Presiona Ctrl+C para detenerlo.");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            var cycleStarted = Stopwatch.GetTimestamp();

            var windows = _windowsDetector.Detect();
            var player = await _playerDetector.DetectAsync();

            var report = new AgentReport(
                StoreCode: _configuration.Codigo,
                StoreName: _configuration.Nombre,
                WindowsVersion: windows.Version,
                WindowsEdition: windows.Edition,
                Player: player,
                ReportedAt: DateTimeOffset.UtcNow);

            PrintReport(report);

            try
            {
                await _reportSender.SendAsync(report, cancellationToken);
                Console.WriteLine("Reporte enviado a la API.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"No se pudo enviar el reporte: {exception.Message}");
            }

            var elapsed = Stopwatch.GetElapsedTime(cycleStarted);
            var remaining = TimeSpan.FromSeconds(
                _configuration.IntervaloReporteSegundos) - elapsed;

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }
    }
    private static void PrintReport(AgentReport report)
    {
        Console.WriteLine(
            $"Hora: {report.ReportedAt:yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine(
            $"Tienda: {report.StoreName} ({report.StoreCode})");

        Console.WriteLine(
            $"Windows: {report.WindowsEdition} ({report.WindowsVersion})");

        Console.WriteLine(
            $"Reproductor: {report.Player.PlayerName}");

        Console.WriteLine(
            $"Abierto: {(report.Player.IsOpen ? "Sí" : "No")}");

        Console.WriteLine(
            $"Estado de reproducción: {report.Player.State}");

        Console.WriteLine(
            $"Detalle: {report.Player.Details}");

        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"Título: {report.Player.Title}");
        Console.WriteLine($"Artista: {report.Player.Artist}");
    }
}
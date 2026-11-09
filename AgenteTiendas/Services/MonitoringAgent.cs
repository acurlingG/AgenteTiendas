using AgenteTiendas.Models;
using System.Diagnostics;

namespace AgenteTiendas.Services;

public sealed class MonitoringAgent
{
    private readonly StoreConfiguration _configuration;
    private readonly WindowsDetector _windowsDetector;
    private readonly PlayerDetector _playerDetector;
    private readonly SystemMetricsDetector _systemMetricsDetector;
    private readonly NetworkDetector _networkDetector;
    private readonly ReportSender _reportSender;

    private readonly string _configurationPath;
    private readonly ConfigurationReader _configurationReader;
    private string _networkTarget;
    private bool _configurationWarningShown;

    public MonitoringAgent(
    StoreConfiguration configuration,
    string configurationPath,
    ConfigurationReader configurationReader,
    WindowsDetector windowsDetector,
    PlayerDetector playerDetector,
    SystemMetricsDetector systemMetricsDetector,
    NetworkDetector networkDetector,
    ReportSender reportSender)
    {
        _configuration = configuration;
        _windowsDetector = windowsDetector;
        _playerDetector = playerDetector;
        _systemMetricsDetector = systemMetricsDetector;
        _networkDetector = networkDetector;
        _reportSender = reportSender;
        _configurationPath = configurationPath;
        _configurationReader = configurationReader;
        _networkTarget = configuration.DestinoMonitoreoRed;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"Agente iniciado para {_configuration.Nombre} ({_configuration.Codigo}).");
        Console.WriteLine("Presiona Ctrl+C para detenerlo.");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            ReloadNetworkTarget();

            var cycleStarted = Stopwatch.GetTimestamp();
            var windows = _windowsDetector.Detect();

            var playerTask = _configuration.ActivoMusica
    ? _playerDetector.DetectAsync(_configuration.RutaListaReproduccion)
    : Task.FromResult(new PlayerStatus(
        PlayerName: "No requerido",
        IsOpen: false,
        State: PlaybackState.NotQueried,
        Title: "",
        Artist: "",
        Details: "El monitoreo de música está desactivado para esta máquina."));
            var machineTask = _systemMetricsDetector.DetectAsync();
            var networkTask = _networkDetector.CheckAsync(_networkTarget);

            await Task.WhenAll(playerTask, machineTask, networkTask);

            var report = new AgentReport(
                StoreCode: _configuration.Codigo,
                StoreName: _configuration.Nombre,
                Pais: _configuration.Pais,
                ActivoMusica: _configuration.ActivoMusica,
                WindowsVersion: windows.Version,
                WindowsEdition: windows.Edition,
                Player: playerTask.Result,
                Machine: machineTask.Result,
                Network: networkTask.Result,
                ReportedAt: DateTimeOffset.UtcNow);

            PrintReport(report);

            try
            {
                await _reportSender.SendAsync(report, cancellationToken);
                Console.WriteLine("Reporte enviado a la API.");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"No se pudo enviar el reporte: {exception.Message}");
            }

            var elapsed = Stopwatch.GetElapsedTime(cycleStarted);
            var remaining =
                TimeSpan.FromSeconds(_configuration.IntervaloReporteSegundos)
                - elapsed;

            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, cancellationToken);
        }
    }

    private void ReloadNetworkTarget()
    {
        try
        {
            var updatedConfiguration =
                _configurationReader.Read(_configurationPath);

            var updatedTarget =
                updatedConfiguration.DestinoMonitoreoRed.Trim();

            _configuration.Pais = updatedConfiguration.Pais.Trim();

            if (!string.Equals(
                    _configuration.RutaListaReproduccion,
                    updatedConfiguration.RutaListaReproduccion,
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"Ruta de lista actualizada: " +
                    $"'{_configuration.RutaListaReproduccion}' -> " +
                    $"'{updatedConfiguration.RutaListaReproduccion}'");

                _configuration.RutaListaReproduccion =
                    updatedConfiguration.RutaListaReproduccion;
            }

            if (_configuration.ActivoMusica != updatedConfiguration.ActivoMusica)
            {
                Console.WriteLine(
                    $"Monitoreo de música actualizado: " +
                    $"'{(_configuration.ActivoMusica ? "activo" : "inactivo")}' -> " +
                    $"'{(updatedConfiguration.ActivoMusica ? "activo" : "inactivo")}'");

                _configuration.ActivoMusica = updatedConfiguration.ActivoMusica;
            }

            if (!string.Equals(
                    _networkTarget,
                    updatedTarget,
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"Destino de red actualizado: " +
                    $"'{_networkTarget}' -> '{updatedTarget}'");

                _networkTarget = updatedTarget;
            }

            _configurationWarningShown = false;
        }
        catch (Exception exception)
        {
            // Conserva el último destino válido si el JSON está incompleto
            // o no se puede leer durante una edición.
            if (!_configurationWarningShown)
            {
                Console.WriteLine(
                    "No se pudo volver a leer config.json. " +
                    $"Se conserva el destino '{_networkTarget}'. " +
                    exception.Message);

                _configurationWarningShown = true;
            }
        }
    }

    private static void PrintReport(AgentReport report)
    {
        Console.WriteLine($"Hora: {report.ReportedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine(
            $"Tienda: {report.StoreName} ({report.StoreCode})");
        Console.WriteLine(
            $"Monitoreo de música: {(report.ActivoMusica ? "Activo" : "No requerido")}");
        Console.WriteLine(
            $"Windows: {report.WindowsEdition} ({report.WindowsVersion})");
        Console.WriteLine($"Reproductor: {report.Player.PlayerName}");
        Console.WriteLine(
            $"Abierto: {(report.Player.IsOpen ? "Sí" : "No")}");
        Console.WriteLine(
            $"Estado de reproducción: {report.Player.State}");
        Console.WriteLine($"Detalle: {report.Player.Details}");

        Console.WriteLine(report.Machine.CpuUsagePercent is double cpu
            ? $"CPU: {cpu:0.0}%"
            : "CPU: No disponible");

        var totalMemory = report.Machine.TotalMemoryBytes;
        var availableMemory = report.Machine.AvailableMemoryBytes;

        if (totalMemory is > 0 && availableMemory.HasValue)
        {
            var usedMemory = totalMemory.Value - availableMemory.Value;
            var usedPercent =
                (double)usedMemory / totalMemory.Value * 100;

            Console.WriteLine(
                $"Memoria: {usedPercent:0.0}% usada " +
                $"({ToGb(usedMemory):0.0} de {ToGb(totalMemory.Value):0.0} GB)");
        }
        else
        {
            Console.WriteLine("Memoria: No disponible");
        }

        foreach (var disk in report.Machine.Disks)
        {
            Console.WriteLine(
                $"Disco {disk.Drive}: {ToGb(disk.FreeBytes):0.0} GB libres " +
                $"de {ToGb(disk.TotalBytes):0.0} GB");
        }

        var networkText = report.Network.IsReachable
            ? $"OK ({report.Network.RoundtripTimeMs} ms)"
            : "FAILED";

        Console.WriteLine(
            $"Red {report.Network.Target}: {networkText} - " +
            report.Network.Details);

        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"Título: {report.Player.Title}");
        Console.WriteLine($"Artista: {report.Player.Artist}");
    }

    private static double ToGb(ulong bytes) =>
        bytes / 1024d / 1024 / 1024;
}
using AgenteTiendas.Services;

var configurationPath = Path.Combine(
    AppContext.BaseDirectory,
    "config.json");

try
{
    var configurationReader = new ConfigurationReader();
    var configuration = configurationReader.Read(configurationPath);

    var windowsDetector = new WindowsDetector();
    var playerDetector = new PlayerDetector();
    var systemMetricsDetector = new SystemMetricsDetector();
    var networkDetector = new NetworkDetector();

    using var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    var reportSender = new ReportSender(
        httpClient,
        configuration.UrlApi);

    var agent = new MonitoringAgent(
        configuration,
        configurationPath,
        configurationReader,
        windowsDetector,
        playerDetector,
        systemMetricsDetector,
        networkDetector,
        reportSender);

    using var cancellationTokenSource = new CancellationTokenSource();

    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationTokenSource.Cancel();
    };

    await agent.RunAsync(cancellationTokenSource.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Agente detenido.");
}
catch (Exception exception)
{
    Console.WriteLine($"Error del agente: {exception.Message}");
    Environment.ExitCode = 1;
}
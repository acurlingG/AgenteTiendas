namespace AgenteTiendas.Services;

public sealed class WindowsDetector
{
    public WindowsInfo Detect()
    {
        var version = Environment.OSVersion.Version;

        // Windows 11 comienza con la compilación 22000.
        var edition = version.Build >= 22000
            ? "Windows 11"
            : "Windows 10 o anterior";

        return new WindowsInfo(
            Edition: edition,
            Version: version.ToString());
    }
}

public sealed record WindowsInfo(string Edition, string Version);
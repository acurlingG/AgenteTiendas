using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using AgenteTiendas.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Windows.Media.Control;
using AgentPlaybackState = AgenteTiendas.Models.PlaybackState;

namespace AgenteTiendas.Services;

public sealed class PlayerDetector
{
    private static readonly (string ProcessName, string DisplayName)[] PlayerTypes =
    [
        ("Microsoft.Media.Player", "Reproductor multimedia"),
        ("wmplayer", "Windows Media Player Legacy"),
        ("mplayer2", "Windows Media Player antiguo")
    ];

    public async Task<PlayerStatus> DetectAsync(string? playlistConfigPath = null)
    {
        var openPlayers = FindOpenPlayers();

        if (openPlayers.Count == 0)
        {
            return new PlayerStatus(
                PlayerName: "Windows Media Player / Reproductor multimedia",
                IsOpen: false,
                State: AgentPlaybackState.NotFound,
                Title: "",
                Artist: "",
                Details: "No se encontró abierto ninguno de los reproductores compatibles.");
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return new PlayerStatus(
                PlayerName: JoinPlayerNames(openPlayers),
                IsOpen: true,
                State: AgentPlaybackState.Unknown,
                Title: "",
                Artist: "",
                Details: "La medición de audio requiere Windows 10 build 19041 o posterior.");
        }

        // 1. Medición de audio por proceso
        var measurements = await Task.WhenAll(openPlayers.Select(MeasureAudioAsync));

        // 2. Obtención de metadatos y validación de Lista Definida
        var (mediaTitle, mediaArtist, expectedPlaylist, isValidPlaylist) =
            await GetMediaMetadataAsync(playlistConfigPath);

        string detailMessage;
        if (!string.IsNullOrWhiteSpace(expectedPlaylist))
        {
            detailMessage = isValidPlaylist
                ? $"Lista autorizada activa ({expectedPlaylist})"
                : $"⚠️ ALERTA: Lista o canción no autorizada. Lista esperada: {expectedPlaylist}";
        }
        else
        {
            detailMessage = "Se detectó señal de audio del reproductor.";
        }

        var playing = measurements.FirstOrDefault(result => result.HasSignal == true);

        if (playing is not null)
        {
            return new PlayerStatus(
                PlayerName: playing.Player.DisplayName,
                IsOpen: true,
                State: isValidPlaylist
                    ? AgentPlaybackState.Playing
                    : AgentPlaybackState.NotAuthorized,
                Title: mediaTitle,
                Artist: mediaArtist,
                Details: detailMessage);
        }

        var withoutSignal = measurements.FirstOrDefault(result => result.HasSignal == false);

        if (withoutSignal is not null)
        {
            return new PlayerStatus(
                PlayerName: withoutSignal.Player.DisplayName,
                IsOpen: true,
                State: AgentPlaybackState.NoSignal,
                Title: mediaTitle,
                Artist: mediaArtist,
                Details: "El reproductor está abierto pero no emite audio.");
        }

        return new PlayerStatus(
            PlayerName: JoinPlayerNames(openPlayers),
            IsOpen: true,
            State: AgentPlaybackState.Unknown,
            Title: mediaTitle,
            Artist: mediaArtist,
            Details: "Se encontraron reproductores abiertos pero no se pudo medir su audio.");
    }

    private static async Task<(string Title, string Artist, string ExpectedPlaylist, bool IsValidPlaylist)> GetMediaMetadataAsync(string? playlistConfigPath)
    {
        string title = "";
        string artist = "";
        var playlistConfigured = !string.IsNullOrWhiteSpace(playlistConfigPath);
        string expectedPlaylist = GetPlaylistNameFromPath(playlistConfigPath);
        if (playlistConfigured && string.IsNullOrWhiteSpace(expectedPlaylist))
            expectedPlaylist = playlistConfigPath!;

        bool isValidPlaylist = !playlistConfigured;

        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var currentSession = manager.GetCurrentSession();

            if (currentSession is not null)
            {
                var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
                title = mediaProperties.Title ?? "";
                artist = mediaProperties.Artist ?? "";
            }
        }
        catch
        {
            // Silenciar errores de consulta a GSMTC
        }

        // Si se configuró una lista específica, se valida la canción contra
        // las entradas reales del archivo de reproducción.
        if (!string.IsNullOrWhiteSpace(expectedPlaylist))
        {
            var entries = ReadPlaylistEntries(playlistConfigPath);
            isValidPlaylist = entries.Count > 0 &&
                              IsCurrentTrackInPlaylist(title, artist, entries);

            artist = string.IsNullOrWhiteSpace(artist)
                ? $"Lista: {expectedPlaylist}"
                : artist;
        }

        return (title, artist, expectedPlaylist, isValidPlaylist);
    }

    private static bool IsCurrentTrackInPlaylist(
        string title,
        string artist,
        IReadOnlyCollection<string> entries)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var currentTitle = NormalizeTrackName(title);
        var currentArtist = NormalizeTrackName(artist);

        return entries.Any(entry =>
        {
            var normalizedEntry = NormalizeTrackName(entry);
            var fileName = NormalizeTrackName(
                Path.GetFileNameWithoutExtension(entry));

            var titleMatches = !string.IsNullOrWhiteSpace(currentTitle) &&
                               (normalizedEntry.Contains(currentTitle, StringComparison.OrdinalIgnoreCase) ||
                                currentTitle.Contains(fileName, StringComparison.OrdinalIgnoreCase));

            var artistMatches = !string.IsNullOrWhiteSpace(currentArtist) &&
                                (normalizedEntry.Contains(currentArtist, StringComparison.OrdinalIgnoreCase) ||
                                 currentArtist.Contains(fileName, StringComparison.OrdinalIgnoreCase));

            return titleMatches || artistMatches;
        });
    }

    private static List<string> ReadPlaylistEntries(string? path)
    {
        var entries = new List<string>();

        var playlistFile = ResolvePlaylistFile(path);

        if (playlistFile is null)
            return entries;

        try
        {
            var extension = Path.GetExtension(playlistFile);

            if (extension.Equals(".wpl", StringComparison.OrdinalIgnoreCase))
            {
                var document = XDocument.Load(playlistFile);

                entries.AddRange(document.Descendants()
                    .Where(element => element.Name.LocalName.Equals("media", StringComparison.OrdinalIgnoreCase))
                    .Select(element => (string?)element.Attribute("src"))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!));
            }
            else
            {
                foreach (var line in File.ReadLines(playlistFile))
                {
                    var value = line.Trim();

                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    if (value.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                    {
                        var separator = value.IndexOf(',');
                        if (separator >= 0 && separator < value.Length - 1)
                            entries.Add(value[(separator + 1)..].Trim());

                        continue;
                    }

                    if (!value.StartsWith('#'))
                        entries.Add(value);
                }
            }
        }
        catch
        {
            // Una lista inválida se considera no autorizada.
        }

        return entries;
    }

    private static string? ResolvePlaylistFile(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (File.Exists(path))
                return path;

            if (!Directory.Exists(path))
                return null;

            return Directory.GetFiles(path)
                .Where(file =>
                    Path.GetExtension(file).Equals(".wpl", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(file).Equals(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(file).Equals(".m3u", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(file).Equals(".zpl", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeTrackName(string value)
    {
        var withoutDiacritics = string.Concat(
            value.Normalize(NormalizationForm.FormD)
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark));

        return string.Join(' ', withoutDiacritics
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static string GetPlaylistNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        try
        {
            var playlistFile = ResolvePlaylistFile(path);
            if (playlistFile is not null)
                return Path.GetFileNameWithoutExtension(playlistFile);
        }
        catch
        {
            // Silenciar excepciones de archivo
        }

        return "";
    }

    private static List<OpenPlayer> FindOpenPlayers()
    {
        var players = new List<OpenPlayer>();

        foreach (var (processName, displayName) in PlayerTypes)
        {
            var processes = Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                try
                {
                    players.Add(new OpenPlayer(process.Id, displayName));
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return players.DistinctBy(player => player.ProcessId).ToList();
    }

    private static async Task<AudioMeasurement> MeasureAudioAsync(OpenPlayer player)
    {
        try
        {
            var signalDetected = 0;

            await using var recorder = await new WasapiRecorderBuilder()
                .WithProcessLoopback((uint)player.ProcessId, ProcessLoopbackMode.IncludeTargetProcessTree)
                .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
                .BuildAsync();

            recorder.DataAvailable += (buffer, _, _, _) =>
            {
                var samples = MemoryMarshal.Cast<byte, float>(buffer);

                foreach (var sample in samples)
                {
                    if (MathF.Abs(sample) > 0.0005f)
                    {
                        Interlocked.Exchange(ref signalDetected, 1);
                        break;
                    }
                }
            };

            recorder.StartRecording();
            await Task.Delay(TimeSpan.FromSeconds(2));
            recorder.StopRecording();

            return new AudioMeasurement(player, HasSignal: Volatile.Read(ref signalDetected) == 1, Error: null);
        }
        catch (Exception exception)
        {
            return new AudioMeasurement(player, HasSignal: null, Error: exception.Message);
        }
    }

    private static string JoinPlayerNames(IEnumerable<OpenPlayer> players) =>
        string.Join(", ", players.Select(player => player.DisplayName).Distinct());

    private sealed record OpenPlayer(int ProcessId, string DisplayName);
    private sealed record AudioMeasurement(OpenPlayer Player, bool? HasSignal, string? Error);
}
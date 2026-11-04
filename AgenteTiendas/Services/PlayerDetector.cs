using System.Diagnostics;
using System.Runtime.InteropServices;
using AgenteTiendas.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
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

    public async Task<PlayerStatus> DetectAsync()
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

        // Se mide cada proceso. Así, un reproductor abierto pero en silencio
        // no oculta a otro que sí está emitiendo audio.
        var measurements = await Task.WhenAll(
            openPlayers.Select(MeasureAudioAsync));

        var playing = measurements.FirstOrDefault(
            result => result.HasSignal == true);

        if (playing is not null)
        {
            return new PlayerStatus(
                PlayerName: playing.Player.DisplayName,
                IsOpen: true,
                State: AgentPlaybackState.Playing,
                Title: "",
                Artist: "",
                Details: "Se detectó señal de audio del reproductor durante la medición.");
        }

        var withoutSignal = measurements.FirstOrDefault(
            result => result.HasSignal == false);

        if (withoutSignal is not null)
        {
            return new PlayerStatus(
                PlayerName: withoutSignal.Player.DisplayName,
                IsOpen: true,
                State: AgentPlaybackState.NoSignal,
                Title: "",
                Artist: "",
                Details: "El reproductor está abierto, pero no se detectó señal de audio en 2 segundos. Puede estar pausado, detenido o reproduciendo silencio.");
        }

        var errors = string.Join(
            " | ",
            measurements
                .Where(result => result.Error is not null)
                .Select(result => $"{result.Player.DisplayName}: {result.Error}"));

        return new PlayerStatus(
            PlayerName: JoinPlayerNames(openPlayers),
            IsOpen: true,
            State: AgentPlaybackState.Unknown,
            Title: "",
            Artist: "",
            Details: $"Se encontraron reproductores abiertos, pero no se pudo medir su audio. {errors}");
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

        return players
            .DistinctBy(player => player.ProcessId)
            .ToList();
    }

    private static async Task<AudioMeasurement> MeasureAudioAsync(
        OpenPlayer player)
    {
        try
        {
            var signalDetected = 0;

            await using var recorder = await new WasapiRecorderBuilder()
                .WithProcessLoopback(
                    (uint)player.ProcessId,
                    ProcessLoopbackMode.IncludeTargetProcessTree)
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

            return new AudioMeasurement(
                player,
                HasSignal: Volatile.Read(ref signalDetected) == 1,
                Error: null);
        }
        catch (Exception exception)
        {
            return new AudioMeasurement(
                player,
                HasSignal: null,
                Error: exception.Message);
        }
    }

    private static string JoinPlayerNames(IEnumerable<OpenPlayer> players) =>
        string.Join(", ", players.Select(player => player.DisplayName).Distinct());

    private sealed record OpenPlayer(int ProcessId, string DisplayName);

    private sealed record AudioMeasurement(
        OpenPlayer Player,
        bool? HasSignal,
        string? Error);
}
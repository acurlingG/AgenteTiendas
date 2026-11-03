using System.Diagnostics;
using System.Runtime.InteropServices;
using AgenteTiendas.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using AgentPlaybackState = AgenteTiendas.Models.PlaybackState;

namespace AgenteTiendas.Services;

public sealed class PlayerDetector
{
    public async Task<PlayerStatus> DetectAsync()
    {
        var processes = Process.GetProcessesByName("wmplayer");
        var process = processes.FirstOrDefault();

        foreach (var item in processes)
        {
            if (item.Id != process?.Id)
            {
                item.Dispose();
            }
        }

        if (process is null)
        {
            return new PlayerStatus(
                PlayerName: "Windows Media Player clásico",
                IsOpen: false,
                State: AgentPlaybackState.NotFound,
                Title: "",
                Artist: "",
                Details: "No se encontró abierto el reproductor clásico.");
        }

        var processId = process.Id;
        process.Dispose();

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return new PlayerStatus(
                PlayerName: "Windows Media Player clásico",
                IsOpen: true,
                State: AgentPlaybackState.Unknown,
                Title: "",
                Artist: "",
                Details: "La detección de audio por proceso requiere Windows 10 build 19041 o posterior.");
        }

        try
        {
            var signalDetected = 0;

            await using var recorder = await new WasapiRecorderBuilder()
                .WithProcessLoopback(
                    (uint)processId,
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

            if (Volatile.Read(ref signalDetected) == 1)
            {
                return new PlayerStatus(
                    PlayerName: "Windows Media Player clásico",
                    IsOpen: true,
                    State: AgentPlaybackState.Playing,
                    Title: "",
                    Artist: "",
                    Details: "Se detectó señal de audio de WMP durante la medición.");
            }

            return new PlayerStatus(
                PlayerName: "Windows Media Player clásico",
                IsOpen: true,
                State: AgentPlaybackState.NoSignal,
                Title: "",
                Artist: "",
                Details: "No se detectó señal de audio de WMP en 2 segundos. Puede estar pausado, detenido o reproduciendo silencio.");
        }
        catch (Exception exception)
        {
            return new PlayerStatus(
                PlayerName: "Windows Media Player clásico",
                IsOpen: true,
                State: AgentPlaybackState.Unknown,
                Title: "",
                Artist: "",
                Details: $"No se pudo medir el audio de WMP: {exception.Message}");
        }
    }
}
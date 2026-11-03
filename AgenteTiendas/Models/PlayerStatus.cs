namespace AgenteTiendas.Models;

public enum PlaybackState
{
    NotFound,
    NotQueried,
    Playing,
    NoSignal,
    Paused,
    Stopped,
    Unknown
}

public sealed record PlayerStatus(
    string PlayerName,
    bool IsOpen,
    PlaybackState State,
    string Title,
    string Artist,
    string Details);
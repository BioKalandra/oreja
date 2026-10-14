namespace Oreja.Recording;

public enum RecorderState { Idle, Recording, Error }

[Flags]
public enum Degradation
{
    None = 0,
    NoMicrophone = 1,
    MicrophoneSilent = 2,
    NoSystemAudio = 4,
}

public sealed record RecorderStatus(
    RecorderState State,
    Degradation Degradation = Degradation.None,
    DateTime? StartedAt = null,
    string? ErrorMessage = null)
{
    public static readonly RecorderStatus Idle = new(RecorderState.Idle);
}

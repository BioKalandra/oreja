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

/// <summary>Grund eines Fehlerzustands; den Text dazu liefert die Oberfläche.</summary>
public enum RecorderError { WriteFailed, Internal, NoDevice }

public sealed record RecorderStatus(
    RecorderState State,
    Degradation Degradation = Degradation.None,
    DateTime? StartedAt = null,
    RecorderError? Error = null)
{
    public static readonly RecorderStatus Idle = new(RecorderState.Idle);
}

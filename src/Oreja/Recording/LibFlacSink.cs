using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Oreja.Recording.LibFlacNative;

namespace Oreja.Recording;

/// <summary>
/// Kodiert 16-Bit-Mono-PCM mit libFLAC. Jeder fertige FLAC-Frame wird sofort in den Stream
/// geschrieben und geflusht, damit ein Absturz höchstens die letzten Sekunden kostet.
/// </summary>
public sealed unsafe class LibFlacSink : IFlacSink
{
    private const int CallbackOk = 0;
    private const int CallbackError = 1;

    private readonly Stream _stream;
    private GCHandle _self;
    private IntPtr _encoder;
    private int[] _buffer = new int[4096];
    private Exception? _ioError;
    private bool _completed;

    public static LibFlacSink Create(string path, int sampleRate) =>
        new(new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read), sampleRate);

    public LibFlacSink(Stream stream, int sampleRate)
    {
        _stream = stream;
        int status;
        try
        {
            _encoder = FLAC__stream_encoder_new();
            if (_encoder == IntPtr.Zero) throw new InvalidOperationException("libFLAC: Encoder konnte nicht angelegt werden");

            _self = GCHandle.Alloc(this);
            FLAC__stream_encoder_set_channels(_encoder, 1);
            FLAC__stream_encoder_set_bits_per_sample(_encoder, 16);
            FLAC__stream_encoder_set_sample_rate(_encoder, (uint)sampleRate);
            FLAC__stream_encoder_set_compression_level(_encoder, 5);
            status = FLAC__stream_encoder_init_stream(_encoder, &OnWrite, &OnSeek, &OnTell, IntPtr.Zero, GCHandle.ToIntPtr(_self));
        }
        catch
        {
            // z. B. DllNotFoundException: die bereits geöffnete Datei nicht offen lassen
            Dispose();
            throw;
        }

        if (status != 0)
        {
            var error = _ioError;
            Dispose();
            throw new IOException($"libFLAC: Initialisierung fehlgeschlagen (Status {status})", error);
        }
    }

    public void Write(ReadOnlySpan<short> samples)
    {
        ObjectDisposedException.ThrowIf(_encoder == IntPtr.Zero, this);
        if (samples.IsEmpty) return;
        if (_buffer.Length < samples.Length) _buffer = new int[samples.Length];
        for (int i = 0; i < samples.Length; i++) _buffer[i] = samples[i];

        fixed (int* p = _buffer)
        {
            if (FLAC__stream_encoder_process_interleaved(_encoder, p, (uint)samples.Length) == 0)
                throw new IOException("libFLAC: Kodieren fehlgeschlagen", _ioError);
        }
    }

    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_encoder == IntPtr.Zero, this);
        if (_completed) return;
        _completed = true;
        if (FLAC__stream_encoder_finish(_encoder) == 0)
            throw new IOException("libFLAC: Abschließen der Datei fehlgeschlagen", _ioError);
        _stream.Flush();
    }

    public void Dispose()
    {
        if (_encoder != IntPtr.Zero)
        {
            // delete() schließt den Stream ab, falls Complete() nicht aufgerufen wurde.
            FLAC__stream_encoder_delete(_encoder);
            _encoder = IntPtr.Zero;
        }
        if (_self.IsAllocated) _self.Free();
        _stream.Dispose();
    }

    private static LibFlacSink From(IntPtr clientData) => (LibFlacSink)GCHandle.FromIntPtr(clientData).Target!;

    // Die Callbacks dürfen nie werfen: Eine Ausnahme, die in nativen Code läuft, beendet den Prozess.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnWrite(IntPtr encoder, byte* buffer, nuint bytes, uint samples, uint currentFrame, IntPtr clientData)
    {
        var sink = From(clientData);
        try
        {
            sink._stream.Write(new ReadOnlySpan<byte>(buffer, checked((int)bytes)));
            sink._stream.Flush();
            return CallbackOk;
        }
        catch (Exception ex)
        {
            sink._ioError = ex;
            return CallbackError;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnSeek(IntPtr encoder, ulong offset, IntPtr clientData)
    {
        var sink = From(clientData);
        try
        {
            sink._stream.Position = checked((long)offset);
            return CallbackOk;
        }
        catch (Exception ex)
        {
            sink._ioError = ex;
            return CallbackError;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnTell(IntPtr encoder, ulong* offset, IntPtr clientData)
    {
        var sink = From(clientData);
        try
        {
            *offset = checked((ulong)sink._stream.Position);
            return CallbackOk;
        }
        catch (Exception ex)
        {
            sink._ioError = ex;
            return CallbackError;
        }
    }
}

using System.Runtime.InteropServices;

namespace Oreja.Recording;

/// <summary>P/Invoke-Deklarationen für libFLAC 1.5.0 (native/libFLAC.dll, BSD-3-Clause).</summary>
internal static unsafe class LibFlacNative
{
    private const string Lib = "libFLAC";

    [DllImport(Lib)] public static extern IntPtr FLAC__stream_encoder_new();
    [DllImport(Lib)] public static extern void FLAC__stream_encoder_delete(IntPtr encoder);
    [DllImport(Lib)] public static extern int FLAC__stream_encoder_set_channels(IntPtr encoder, uint value);
    [DllImport(Lib)] public static extern int FLAC__stream_encoder_set_bits_per_sample(IntPtr encoder, uint value);
    [DllImport(Lib)] public static extern int FLAC__stream_encoder_set_sample_rate(IntPtr encoder, uint value);
    [DllImport(Lib)] public static extern int FLAC__stream_encoder_set_compression_level(IntPtr encoder, uint value);

    [DllImport(Lib)]
    public static extern int FLAC__stream_encoder_init_stream(
        IntPtr encoder,
        delegate* unmanaged[Cdecl]<IntPtr, byte*, nuint, uint, uint, IntPtr, int> writeCallback,
        delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr, int> seekCallback,
        delegate* unmanaged[Cdecl]<IntPtr, ulong*, IntPtr, int> tellCallback,
        IntPtr metadataCallback,
        IntPtr clientData);

    [DllImport(Lib)] public static extern int FLAC__stream_encoder_process_interleaved(IntPtr encoder, int* buffer, uint samples);
    [DllImport(Lib)] public static extern int FLAC__stream_encoder_finish(IntPtr encoder);
}

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Oreja.Infrastructure;

namespace Oreja.Recording;

/// <summary>
/// Findet libFLAC.dll, ohne sich auf %TEMP% zu verlassen: Eine Kopie neben der Exe hat Vorrang
/// (Entwicklung, Tests, von Hand abgelegt); sonst wird die in die Exe eingebettete DLL nach
/// %LOCALAPPDATA%\Oreja\native\&lt;Hash&gt;\ entpackt – und bei jedem Start geprüft und notfalls neu
/// geschrieben. Die Selbstentpackung der Single-File-Exe nach %TEMP% hat das nicht getan: Löschte
/// die Windows-Speicheroptimierung die DLL, blieb der leere Ordner liegen und galt als entpackt.
/// </summary>
internal static class LibFlacLoader
{
    public const string FileName = LibFlacNative.Lib + ".dll";
    public const string ResourceName = "Oreja.native." + FileName;

    private static readonly object Gate = new();
    private static IntPtr _handle;

    /// <summary>Leitet alle P/Invoke-Aufrufe nach <see cref="LibFlacNative.Lib"/> über den Loader. Nur einmal pro Prozess.</summary>
    public static void Register(string nativeRoot, Log log) =>
        NativeLibrary.SetDllImportResolver(typeof(LibFlacLoader).Assembly, (name, _, _) =>
            name == LibFlacNative.Lib ? Load(nativeRoot, log) : IntPtr.Zero);

    /// <summary>
    /// Lädt die DLL; bei einem Fehler wird protokolliert und <see cref="IntPtr.Zero"/> geliefert, worauf
    /// die Laufzeit eine <see cref="DllNotFoundException"/> wirft. Der nächste Aufruf versucht es erneut.
    /// </summary>
    private static IntPtr Load(string nativeRoot, Log log)
    {
        lock (Gate)
        {
            if (_handle != IntPtr.Zero) return _handle;
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, FileName);
                if (!File.Exists(path))
                {
                    using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                        ?? throw new FileNotFoundException($"Eingebettete Ressource {ResourceName} fehlt");
                    using var buffer = new MemoryStream();
                    resource.CopyTo(buffer);
                    path = EnsureExtracted(nativeRoot, buffer.ToArray());
                }
                _handle = NativeLibrary.Load(path);
                log.Info($"libFLAC geladen: {path}");
                return _handle;
            }
            catch (Exception ex)
            {
                log.Error("libFLAC konnte nicht geladen werden", ex);
                return IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// Sorgt dafür, dass <paramref name="content"/> unverändert unter
    /// <c>nativeRoot\&lt;Hash&gt;\libFLAC.dll</c> liegt, und liefert den Pfad. Eine fehlende oder
    /// abweichende Datei wird über eine temporäre Datei ersetzt, damit nie eine halbe DLL entsteht.
    /// </summary>
    internal static string EnsureExtracted(string nativeRoot, byte[] content)
    {
        byte[] hash = SHA256.HashData(content);
        string folder = Path.Combine(nativeRoot, Convert.ToHexString(hash, 0, 8));
        string path = Path.Combine(folder, FileName);
        if (HasContent(path, content.Length, hash)) return path;

        Directory.CreateDirectory(folder);
        string temp = Path.Combine(folder, $"{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die Datei kann gesperrt sein, weil ein anderer Prozess genau diese DLL geladen hat – dann passt sie.
            if (!HasContent(path, content.Length, hash)) throw;
        }
        finally
        {
            File.Delete(temp);
        }
        return path;
    }

    private static bool HasContent(string path, long length, byte[] hash)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length != length) return false;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return SHA256.HashData(stream).AsSpan().SequenceEqual(hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

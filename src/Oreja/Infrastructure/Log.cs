using System.Globalization;

namespace Oreja.Infrastructure;

/// <summary>Threadsicheres Zeilenprotokoll mit einer Rotationsstufe (oreja.log → oreja.log.1).</summary>
public sealed class Log
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly long _maxBytes;

    public Log(string path, long maxBytes = 1_000_000)
    {
        _path = path;
        _maxBytes = maxBytes;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            try
            {
                var file = new FileInfo(_path);
                if (file.Exists && file.Length >= _maxBytes) File.Move(_path, _path + ".1", overwrite: true);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                File.AppendAllText(_path, $"{timestamp} {level} {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Protokollieren darf die App nie zum Absturz bringen.
            }
        }
    }
}

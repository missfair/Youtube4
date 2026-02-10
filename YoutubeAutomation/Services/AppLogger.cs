using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace YoutubeAutomation.Services;

/// <summary>
/// Simple file-based logger for diagnosing crashes and issues.
/// Writes to %APPDATA%/YoutubeAutomation/logs/ folder.
/// </summary>
public static class AppLogger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;

    public static string LogFilePath => _logFilePath ?? "(not initialized)";

    public static void Initialize()
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YoutubeAutomation", "logs");
            Directory.CreateDirectory(logDir);

            _logFilePath = Path.Combine(logDir, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            // Clean up old logs (keep last 10)
            var oldLogs = Directory.GetFiles(logDir, "log_*.txt")
                .OrderByDescending(f => f)
                .Skip(10)
                .ToArray();
            foreach (var old in oldLogs)
            {
                try { File.Delete(old); } catch { }
            }

            Log("=== YoutubeAutomation started ===");
            Log($"Log file: {_logFilePath}");
            Log($".NET: {Environment.Version}");
            Log($"OS: {Environment.OSVersion}");
            Log($"64-bit process: {Environment.Is64BitProcess}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppLogger.Initialize failed: {ex.Message}");
        }
    }

    public static void Log(string message,
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? file = null)
    {
        try
        {
            var fileName = file != null ? Path.GetFileNameWithoutExtension(file) : "?";
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{fileName}.{caller}] {message}";

            Debug.WriteLine(line);

            if (_logFilePath == null) return;
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch { }
    }

    public static void LogError(Exception ex, string context = "",
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? file = null)
    {
        var msg = string.IsNullOrWhiteSpace(context)
            ? $"ERROR: {ex.GetType().Name}: {ex.Message}"
            : $"ERROR [{context}]: {ex.GetType().Name}: {ex.Message}";

        if (ex.InnerException != null)
            msg += $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";

        if (ex is AggregateException ae)
        {
            foreach (var inner in ae.InnerExceptions)
                msg += $" | AggInner: {inner.GetType().Name}: {inner.Message}";
        }

        msg += $" | StackTrace: {ex.StackTrace}";

        Log(msg, caller, file);
    }

    public static void LogUnhandled(Exception ex, string source)
    {
        try
        {
            var separator = new string('=', 50);
            var msg = $"\n{separator}\nUNHANDLED EXCEPTION [{source}]\n{separator}\n" +
                      $"Type: {ex.GetType().FullName}\n" +
                      $"Message: {ex.Message}\n" +
                      $"StackTrace:\n{ex.StackTrace}\n";

            if (ex.InnerException != null)
            {
                msg += $"\nInner Exception:\n" +
                       $"Type: {ex.InnerException.GetType().FullName}\n" +
                       $"Message: {ex.InnerException.Message}\n" +
                       $"StackTrace:\n{ex.InnerException.StackTrace}\n";
            }

            msg += separator;

            Log(msg);
        }
        catch { }
    }
}

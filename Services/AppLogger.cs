using System.Collections.Concurrent;
using System.IO;

namespace Hanime1Downloader.CSharp.Services;

public static class AppLogger
{
    private static readonly string AppLogPath = Path.Combine(AppContext.BaseDirectory, "app.log");
    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<string, DateTime> LastMessageTimes = new(StringComparer.Ordinal);

    public static void Info(string category, string message)
    {
        Write("INFO", category, message, null);
    }

    public static void InfoThrottled(string category, string message, TimeSpan interval)
    {
        if (!ShouldWrite(category, message, interval))
        {
            return;
        }

        Write("INFO", category, message, null);
    }

    public static void Error(string category, string message, Exception? exception = null)
    {
        Write("ERROR", category, message, exception);
    }

    private static bool ShouldWrite(string category, string message, TimeSpan interval)
    {
        var key = $"{category}|{message}";
        var now = DateTime.UtcNow;
        var last = LastMessageTimes.GetOrAdd(key, DateTime.MinValue);
        if (now - last < interval)
        {
            return false;
        }

        LastMessageTimes[key] = now;
        return true;
    }

    private static void Write(string level, string category, string message, Exception? exception)
    {
#if DEBUG
        var text = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [{level}] [{category}] {message}{Environment.NewLine}";
        if (exception is not null)
        {
            text += exception + Environment.NewLine;
        }
        lock (SyncRoot)
        {
            File.AppendAllText(AppLogPath, text + Environment.NewLine);
        }
#endif
    }
}

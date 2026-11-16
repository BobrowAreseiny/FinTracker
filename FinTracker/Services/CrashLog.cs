using System.Text;

namespace FinTracker.Services;

/// <summary>
/// Журнал падений в файл. Если приложение упадёт на телефоне, точную причину
/// можно посмотреть прямо в приложении (экран ошибки) или прислать разработчику.
/// </summary>
public static class CrashLog
{
    private const int MaxFileChars = 120_000;
    private static readonly object Gate = new();

    public static string LogPath
    {
        get
        {
            try
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "fintracker_crash.log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "fintracker_crash.log");
            }
        }
    }

    /// <summary>Записывает исключение в журнал. Сам никогда не бросает исключений.</summary>
    public static void Write(Exception? ex, string? context = null)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("==============================================");
            sb.AppendLine($"Время (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrWhiteSpace(context))
                sb.AppendLine($"Где: {context}");

            if (ex is null)
            {
                sb.AppendLine("Без исключения");
            }
            else
            {
                sb.AppendLine($"Тип: {ex.GetType().FullName}");
                sb.AppendLine($"Сообщение: {ex.Message}");
                sb.AppendLine(ex.ToString());

                var inner = ex.InnerException;
                int depth = 0;
                while (inner is not null && depth++ < 3)
                {
                    sb.AppendLine($"--- Вложенное ({depth}) ---");
                    sb.AppendLine(inner.ToString());
                    inner = inner.InnerException;
                }
            }
            sb.AppendLine();

            lock (Gate)
            {
                string path = LogPath;
                string existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;

                // Не даём файлу расти бесконечно
                if (existing.Length > MaxFileChars)
                    existing = existing[^MaxFileChars..];

                File.WriteAllText(path, existing + sb, Encoding.UTF8);
            }
        }
        catch
        {
            // Логирование не должно само валить приложение.
        }
    }

    /// <summary>Последние символы журнала — показываются на экране ошибки. null, если пусто.</summary>
    public static string? ReadLast(int maxChars = 4000)
    {
        try
        {
            string path = LogPath;
            if (!File.Exists(path)) return null;

            string text = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text)) return null;

            if (text.Length <= maxChars) return text;

            return "…" + text[^maxChars..];
        }
        catch (Exception ex)
        {
            return "Не удалось прочитать журнал: " + ex.Message;
        }
    }

    public static void Clear()
    {
        try
        {
            string path = LogPath;
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}

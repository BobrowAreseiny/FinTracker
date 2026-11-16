using Android.Content;
using Android.Provider;
using FinTracker.Services;

namespace FinTracker.Platforms.Android;

/// <summary>
/// Чтение СМС напрямую из базы сообщений телефона.
/// Нужно потому, что уведомление о СМС не появляется, если мессенджер открыт
/// или текст сообщения скрыт на заблокированном экране.
/// </summary>
public static class SmsReader
{
    private const string PrefLastScan = "FT_LastSmsScan";
    private const int DefaultDaysBack = 3;

    /// <summary>Есть ли разрешение на чтение СМС.</summary>
    public static bool HasPermission()
    {
        try
        {
            var ctx = global::Android.App.Application.Context;
            return ctx.CheckSelfPermission("android.permission.READ_SMS") == global::Android.Content.PM.Permission.Granted;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проходит по входящим СМС, пришедшим после прошлого запуска, и добавляет операции.
    /// Повторно одни и те же сообщения не разбираются — отметка хранится в настройках.
    /// </summary>
    public static int ScanInbox()
    {
        if (!HasPermission()) return 0;

        var db = IPlatformApplication.Current?.Services?.GetService<DatabaseService>();
        if (db is null) return 0;

        var ctx = global::Android.App.Application.Context;

        long sinceMs = Preferences.Default.Get(PrefLastScan, 0L);
        if (sinceMs <= 0)
        {
            // Первый запуск — смотрим последние дни, чтобы подтянуть пропущенное
            sinceMs = (long)(DateTime.UtcNow.AddDays(-DefaultDaysBack) - DateTime.UnixEpoch).TotalMilliseconds;
        }

        long newest = sinceMs;
        int added = 0;

        try
        {
            var uri = global::Android.Net.Uri.Parse("content://sms/inbox");

            using var cursor = ctx.ContentResolver!.Query(
                uri,
                new[] { "_id", "body", "date" },
                "date > ?",
                new[] { sinceMs.ToString() },
                "date ASC");

            if (cursor is null) return 0;

            int bodyIdx = cursor.GetColumnIndex("body");
            int dateIdx = cursor.GetColumnIndex("date");

            while (cursor.MoveToNext())
            {
                string body = bodyIdx >= 0 ? cursor.GetString(bodyIdx) ?? string.Empty : string.Empty;
                long ms = dateIdx >= 0 ? cursor.GetLong(dateIdx) : 0;

                if (ms > newest) newest = ms;
                if (string.IsNullOrWhiteSpace(body)) continue;

                var tx = TransactionParser.ParseNotification(body);
                if (tx is null) continue;

                tx.Source = "sms";
                if (ms > 0)
                    tx.Date = DateTime.UnixEpoch.AddMilliseconds(ms).ToLocalTime();

                db.AddTransaction(tx);
                added++;
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "SmsReader.ScanInbox");
        }

        if (newest > sinceMs)
            Preferences.Default.Set(PrefLastScan, newest);

        return added;
    }
}

namespace FinTracker.Services;

/// <summary>
/// Кроссплатформенная обёртка над чтением СМС.
/// На Android читает базу сообщений и принимает новые СМС в реальном времени;
/// на других платформах недоступно (iOS не даёт доступа к СМС).
/// </summary>
public static class SmsAccess
{
    public static bool SupportsSms
    {
#if ANDROID
        get => true;
#else
        get => false;
#endif
    }

    public static bool HasPermission()
    {
#if ANDROID
        try
        {
            return Platforms.Android.SmsReader.HasPermission();
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    /// <summary>Запрашивает оба разрешения: чтение списка СМС и приём новых.</summary>
    public static async Task<bool> RequestPermissionAsync()
    {
#if ANDROID
        try
        {
            var read = await Permissions.RequestAsync<Permissions.Sms>();
            var receive = await Permissions.RequestAsync<Platforms.Android.ReceiveSmsPermission>();

            return read == PermissionStatus.Granted && receive == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "SmsAccess.RequestPermissionAsync");
            return false;
        }
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    /// <summary>Разбирает новые входящие СМС. Возвращает число добавленных операций.</summary>
    public static int ScanInbox()
    {
#if ANDROID
        try
        {
            return Platforms.Android.SmsReader.ScanInbox();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "SmsAccess.ScanInbox");
            return 0;
        }
#else
        return 0;
#endif
    }
}

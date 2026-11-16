#if ANDROID
using Android.Content;
#endif

namespace FinTracker.Services;

/// <summary>
/// Проверка и запрос доступа к чтению уведомлений (Android).
/// Без этого доступа автоподхват оплат не работает, и об этом нужно честно
/// сообщать пользователю, а не молчать.
/// </summary>
public static class NotificationAccess
{
    public static bool IsEnabled()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            string? flat = Android.Provider.Settings.Secure.GetString(
                context.ContentResolver,
                "enabled_notification_listeners");

            if (string.IsNullOrWhiteSpace(flat)) return false;

            string packageName = context.PackageName ?? string.Empty;
            if (string.IsNullOrEmpty(packageName)) return false;

            return flat.Contains(packageName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    public static bool SupportsAutoCapture
    {
#if ANDROID
        get => true;
#else
        get => false;
#endif
    }

    public static bool OpenSettings()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var intent = new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
            return true;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }
}

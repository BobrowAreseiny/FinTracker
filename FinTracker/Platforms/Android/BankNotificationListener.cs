using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using FinTracker.Services;

namespace FinTracker.Platforms.Android;

[Service(Label = "FinTracker — чтение оплат", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE", Exported = true)]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public class BankNotificationListener : NotificationListenerService
{
    /// <summary>
    /// Приложения, уведомления которых разбираются в операции.
    /// </summary>
    private static readonly string[] AllowedPackages =
    [
        // --- Банки Беларуси ---
        "by.alfabank.insync",
        "by.priorbank.priorclick",
        "by.softclub.belarusbank",
        "com.mobicon.mbank2.belarusbank",
        "by.mtbank.moby",
        "by.bapb.mob.on",
        "by.belinvestbank.biz.on",
        "by.vtb.mobile",
        "by.bps_sberbank.sberbankmobile",
        "by.bsb.mobile",
        "by.belinvestbank.internetbank",
        "by.dabrabyt.mobile",

        // --- Samsung Pay / Samsung Wallet ---
        // На разных прошивках уведомление об оплате публикует один из этих пакетов
        "com.samsung.android.spay",
        "com.samsung.android.spayfw",
        "com.samsung.android.spaymini",
        "com.samsung.android.wallet",

        // --- СМС / сообщения ---
        "com.samsung.android.messaging",        // Samsung Messages — приложение по умолчанию на Galaxy
        "com.google.android.apps.messaging",    // Google Messages
        "com.android.mms",                      // AOSP
        "com.samsung.android.mms",
    ];

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        try
        {
            if (sbn is null) return;

            // Автоучёт выключен — не обрабатываем уведомления вообще.
            if (!Microsoft.Maui.Storage.Preferences.Default.Get("FT_AutoTrack", true)) return;

            if (sbn.IsOngoing) return;

            var notification = sbn.Notification;
            if (notification is null) return;

            // Сводки групп дублируют содержимое обычных уведомлений.
            if ((notification.Flags & NotificationFlags.GroupSummary) != 0) return;

            string packageName = sbn.PackageName ?? string.Empty;
            if (!AllowedPackages.Contains(packageName)) return;

            // Свои уведомления не трогаем
            if (string.Equals(packageName, PackageName, StringComparison.Ordinal)) return;

            var extras = notification.Extras;
            if (extras is null) return;

            // ВАЖНО: EXTRA_BIG_TEXT и EXTRA_TEXT — это CharSequence (может быть SpannableString),
            // поэтому Bundle.GetString() для них возвращает null. Читаем через Get().ToString().
            string title = First(AsText(extras, Notification.ExtraTitle),
                                 AsText(extras, Notification.ExtraTitleBig));

            string body = First(AsText(extras, Notification.ExtraBigText),
                                AsText(extras, Notification.ExtraText),
                                AsText(extras, Notification.ExtraSubText),
                                AsText(extras, Notification.ExtraInfoText),
                                AsText(extras, Notification.ExtraTextLines));

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body)) return;

            string fullText = string.Join(" | ", new[] { title, body }.Where(s => !string.IsNullOrWhiteSpace(s)));

            // Без цифр суммы нет — не гоняем парсер.
            if (fullText.Length == 0 || !fullText.Any(char.IsDigit)) return;

            System.Diagnostics.Debug.WriteLine($"[FinTracker] пуш ({packageName}): {fullText}");

            var transaction = TransactionParser.ParseNotification(fullText);
            if (transaction is null) return;

            // Канал влияет только на метку источника: защита от дублей общая,
            // чтобы оплата из Samsung Wallet и СМС от банка не добавились дважды.
            transaction.Source = IsMessagingPackage(packageName) ? "sms" : "push";

            var dbService = IPlatformApplication.Current?.Services?.GetService<DatabaseService>();
            if (dbService is null) return;

            dbService.AddTransaction(transaction);

            // Отмечаем уведомление прочитанным, чтобы банк не «переотправлял» его в трей
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    CancelNotification(sbn.Key);
            }
            catch { }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "BankNotificationListener.OnNotificationPosted");
        }
    }

    /// <summary>Приложения сообщений — их источник помечаем как "sms".</summary>
    private static bool IsMessagingPackage(string packageName)
    {
        return packageName is "com.samsung.android.messaging"
                          or "com.samsung.android.mms"
                          or "com.google.android.apps.messaging"
                          or "com.android.mms";
    }

    private static string First(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v!.Trim();
        }
        return string.Empty;
    }

    /// <summary>Достаёт текст из Bundle для любого типа значения (String / SpannableString / CharSequence[]).</summary>
    private static string? AsText(Bundle extras, string key)
    {
        try
        {
            var value = extras.Get(key);
            if (value is null) return null;

            // Приводим к object: для Java.Lang.Object проверка «is T[]» недопустима
            // на уровне типов и не компилируется.
            object boxed = value;

            // CharSequence[] (EXTRA_TEXT_LINES) — склеиваем строки
            if (boxed is Java.Lang.ICharSequence[] charSequences)
            {
                var parts = charSequences
                    .Where(l => !string.IsNullOrWhiteSpace(l?.ToString()))
                    .Select(l => l!.ToString()!.Trim());

                return string.Join(" ", parts);
            }

            // Java-массив объектов (на части прошивок приходит именно так)
            if (boxed is Java.Lang.Object[] javaObjects)
            {
                var parts = javaObjects
                    .Where(o => o is not null)
                    .Select(o => o!.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                return string.Join(" ", parts!);
            }

            return value.ToString();
        }
        catch
        {
            return null;
        }
    }
}

using Android.App;
using Android.Content;
using Android.Telephony;
using FinTracker.Services;

namespace FinTracker.Platforms.Android;

/// <summary>
/// Приём входящих СМС в реальном времени — не зависит от того, открыт ли мессенджер
/// и показывается ли уведомление о сообщении.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true, Permission = "android.permission.BROADCAST_SMS")]
[IntentFilter(["android.provider.Telephony.SMS_RECEIVED"], Priority = 999)]
public class SmsReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        try
        {
            if (intent?.Action != "android.provider.Telephony.SMS_RECEIVED") return;

            // Автоучёт выключен — не разбираем СМС.
            if (!Microsoft.Maui.Storage.Preferences.Default.Get("FT_AutoTrack", true)) return;

            var bundle = intent.Extras;
            if (bundle is null) return;

            var raw = bundle.Get("pdus");
            if (raw is null) return;

            // raw — Java-массив байтовых массивов
            object boxed = raw;
            if (boxed is not Java.Lang.Object[] items) return;

            var db = IPlatformApplication.Current?.Services?.GetService<DatabaseService>();
            if (db is null) return;

            foreach (var item in items)
            {
                object element = item;
                if (element is not byte[] pdu) continue;

                var sms = global::Android.Telephony.SmsMessage.CreateFromPdu(pdu);
                if (sms is null) continue;

                string body = sms.MessageBody ?? string.Empty;
                if (string.IsNullOrWhiteSpace(body)) continue;

                // Без цифр суммы нет.
                if (!body.Any(char.IsDigit)) continue;

                var tx = TransactionParser.ParseNotification(body);
                if (tx is null) continue;

                tx.Source = "sms";

                long ms = sms.TimestampMillis;
                if (ms > 0)
                    tx.Date = DateTime.UnixEpoch.AddMilliseconds(ms).ToLocalTime();

                db.AddTransaction(tx);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "SmsReceiver.OnReceive");
        }
    }
}

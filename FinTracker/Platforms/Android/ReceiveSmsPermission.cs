namespace FinTracker.Platforms.Android;

/// <summary>
/// Разрешение на приём входящих СМС (RECEIVE_SMS).
/// Требуется, чтобы BroadcastReceiver получал новые сообщения.
/// </summary>
public class ReceiveSmsPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
    [
        (global::Android.Manifest.Permission.ReceiveSms, true)
    ];
}

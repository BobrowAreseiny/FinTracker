using Android.App;
using Android.Runtime;
using FinTracker.Services;

namespace FinTracker
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            // Android передаёт сюда исключения, которые иначе уронили бы процесс.
            // Записываем причину в журнал, чтобы её было видно при следующем запуске.
            AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            {
                CrashLog.Write(e.Exception, "AndroidEnvironment.UnhandledException");
            };

            base.OnCreate();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}

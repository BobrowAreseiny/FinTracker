using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FinTracker.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Диагностика: необработанные исключения WinUI должны попадать в журнал,
            // иначе приложение на десктопе закрывается без объяснения причины.
            UnhandledException += (_, e) =>
            {
                FinTracker.Services.CrashLog.Write(e.Exception, "WinUI.UnhandledException");
            };

            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            {
                if (Environment.GetEnvironmentVariable("FT_TRACE") == "1")
                    FinTracker.Services.CrashLog.Write(e.Exception, "WinUI.FirstChance");
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}

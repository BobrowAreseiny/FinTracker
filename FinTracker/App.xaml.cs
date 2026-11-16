using FinTracker.Services;

namespace FinTracker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                return new Window(new AppShell());
            }
            catch (Exception ex)
            {
                // Вместо тихого падения на старте показываем причину
                CrashLog.Write(ex, "App.CreateWindow");
                return new Window(CrashPage.FromException(ex, "App.CreateWindow"));
            }
        }
    }
}

using FinTracker.Services;

namespace FinTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Экран входа по отпечатку доступен по маршруту "login".
            // Плагин отпечатка поддерживает только Android, поэтому на других
            // платформах маршрут не регистрируем.
#if ANDROID
            Routing.RegisterRoute("login", typeof(LoginPage));
#endif

            CreateMainPage();
        }

        /// <summary>
        /// Главная страница создаётся здесь явно. Раньше она создавалась из
        /// ContentTemplate (DataTemplate → Activator без DI), и любая проблема
        /// в конструкторе MainPage приводила к вылету приложения без объяснений.
        /// Теперь ошибка показывается на экране и пишется в журнал.
        /// </summary>
        private void CreateMainPage()
        {
            try
            {
                MainShellContent.Content = new MainPage();
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "AppShell.CreateMainPage");

                try
                {
                    MainShellContent.Content = CrashPage.FromException(ex, "Создание главного экрана");
                }
                catch
                {
                    // Если даже аварийный экран не создался — пусть падает дальше,
                    // но причина уже записана в журнал.
                    throw;
                }
            }
        }
    }
}

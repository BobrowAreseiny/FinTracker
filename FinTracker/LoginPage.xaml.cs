using FinTracker.Services;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace FinTracker;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void LoginButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (!await CrossFingerprint.Current.IsAvailableAsync())
            {
                LoginStatusLabel.Text = "Сканер отпечатка недоступен на этом устройстве";
                Haptics.Warning();
                return;
            }

            var request = new AuthenticationRequestConfiguration("Вход", "Приложите палец для доступа к финансам");
            var result = await CrossFingerprint.Current.AuthenticateAsync(request);

            if (result.Authenticated)
            {
                Haptics.Success();
                await GoToMainAsync();
            }
            else
            {
                LoginStatusLabel.Text = "Вход не подтверждён. Попробуйте ещё раз.";
                Haptics.Warning();
            }
        }
        catch (Exception ex)
        {
            LoginStatusLabel.Text = "Не удалось выполнить вход: " + ex.Message;
            Haptics.Warning();
        }
    }

    private void SkipButton_Clicked(object sender, EventArgs e)
    {
        Haptics.Click();
        _ = GoToMainAsync();
    }

    /// <summary>Переход на главный экран через Shell, чтобы не терять навигацию приложения.</summary>
    private static async Task GoToMainAsync()
    {
        var shell = Shell.Current;
        if (shell != null)
        {
            await shell.GoToAsync("//MainPage");
            return;
        }

        // Запасной вариант, если Shell ещё не инициализирован
        var db = IPlatformApplication.Current?.Services?.GetService<DatabaseService>();
        if (db != null)
            Application.Current.MainPage = new NavigationPage(new MainPage(db));
    }
}

using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using FinTracker;
using FinTracker.Services;

namespace FinTracker;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Скриншоты в приложении разрешены: FLAG_SECURE больше не ставим (раньше он
        // блокировал и снимки экрана, и превью в меню недавних приложений).
    }

    /// <summary>
    /// Подслушиваем касания, чтобы распознать свайпы (вбок — назад/вперёд по вкладкам,
    /// вниз — закрыть шторку). Событие не потребляем: оно идёт дальше по дереву как обычно.
    /// </summary>
    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        try
        {
            if (e is not null) GestureRouter.Feed(e);
        }
        catch { }

        return base.DispatchTouchEvent(e);
    }

    /// <summary>
    /// Аппаратная кнопка «Назад». Обрабатываем её сами: приложение не должно закрываться,
    /// а должно возвращать пользователя на предыдущую вкладку (или закрывать открытое окно).
    /// </summary>
    public override void OnBackPressed()
    {
        try
        {
            var page = Shell.Current?.CurrentPage;
            if (page is MainPage mainPage && mainPage.HandleBackRequest())
                return;
        }
        catch { }

        base.OnBackPressed();
    }
}

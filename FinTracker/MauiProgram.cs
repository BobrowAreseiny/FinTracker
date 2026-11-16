using FinTracker.Services;
using Microsoft.Extensions.Logging;
using Microcharts.Maui;

namespace FinTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Глобальный перехват: любая необработанная ошибка попадает в журнал,
            // чтобы её причину можно было увидеть на телефоне.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                CrashLog.Write(e.ExceptionObject as Exception, "AppDomain.UnhandledException");

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                CrashLog.Write(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };

            var builder = MauiApp.CreateBuilder();

#if ANDROID
            // Поля ввода в макете — скруглённые контейнеры: системное подчёркивание Android
            // выглядит как лишняя линия поверх рамки.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("FinTrackerNoUnderline", (handler, view) =>
            {
                handler.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            });
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("FinTrackerNoUnderline", (handler, view) =>
            {
                handler.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            });
#endif

            builder
                .UseMauiApp<App>()
                .UseMicrocharts()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Inter — шрифт эталонного макета Soft Matte Pro
                    fonts.AddFont("Inter-Regular.ttf", "Inter");
                    fonts.AddFont("Inter-Medium.ttf", "InterMedium");
                    fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                    fonts.AddFont("Inter-Bold.ttf", "InterBold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            // База данных — один на всё приложение (её использует и слушатель уведомлений банка)
            builder.Services.AddSingleton<FinTracker.Services.DatabaseService>();

            return builder.Build();
        }
    }
}

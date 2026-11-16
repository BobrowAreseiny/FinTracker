using FinTracker.Services;

namespace FinTracker;

/// <summary>
/// Экран с текстом ошибки. Собирается кодом (без XAML), чтобы сам не мог
/// упасть при загрузке и always показать причину падения.
/// </summary>
public class CrashPage : ContentPage
{
    public CrashPage(string header, string details)
    {
        Title = "Ошибка";
        BackgroundColor = Color.FromArgb("#F8F9FA");

        var headerLabel = new Label
        {
            Text = header,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#EF4444"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var hintLabel = new Label
        {
            Text = "Пришлите этот текст разработчику — по нему видно точную причину.",
            FontSize = 12,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var detailsLabel = new Label
        {
            Text = details,
            FontSize = 11,
            TextColor = Color.FromArgb("#111827"),
            LineBreakMode = LineBreakMode.CharacterWrap
        };

        var detailsCard = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E5E7EB"),
            StrokeThickness = 1,
            Padding = 12,
            Content = new ScrollView { Content = detailsLabel }
        };

        var copyButton = new Button
        {
            Text = "Скопировать текст ошибки",
            BackgroundColor = Color.FromArgb("#6366F1"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 0,
            HeightRequest = 44
        };
        copyButton.Clicked += async (_, _) =>
        {
            try
            {
                await Clipboard.Default.SetTextAsync(details);
                copyButton.Text = "Скопировано ✓";
            }
            catch
            {
                copyButton.Text = "Не удалось скопировать";
            }
        };

        var clearButton = new Button
        {
            Text = "Скрыть и перезапустить",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#6B7280"),
            FontSize = 13,
            HeightRequest = 40
        };
        clearButton.Clicked += (_, _) =>
        {
            CrashLog.Clear();
            if (Application.Current is not null)
                Application.Current.MainPage = new AppShell();
        };

        var root = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(18, 34, 18, 18),
            RowSpacing = 10
        };

        root.Add(headerLabel, 0, 0);
        root.Add(hintLabel, 0, 1);
        root.Add(detailsCard, 0, 2);
        root.Add(copyButton, 0, 3);
        root.Add(clearButton, 0, 4);

        Content = root;
    }

    public static CrashPage FromException(Exception ex, string? context = null)
    {
        string details = $"Тип: {ex.GetType().FullName}\nСообщение: {ex.Message}\n\n";
        if (!string.IsNullOrWhiteSpace(context))
            details += $"Где: {context}\n\n";

        details += ex.ToString();

        var inner = ex.InnerException;
        int depth = 0;
        while (inner is not null && depth++ < 3)
        {
            details += $"\n\n--- Вложенное ({depth}) ---\n{inner}";
            inner = inner.InnerException;
        }

        return new CrashPage("Приложение столкнулось с ошибкой", details);
    }
}

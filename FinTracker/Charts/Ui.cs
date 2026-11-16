using Microsoft.Maui.Controls.Shapes;
using FinTracker.Controls;
using FinTracker.Services;

namespace FinTracker.Charts;

/// <summary>
/// Мелкие фабрики элементов в стиле Soft Matte Pro.
/// Позволяют строить динамические списки кодом, не дублируя десятки одинаковых свойств.
/// </summary>
public static class Ui
{
    public static readonly Color Bg = Color.FromArgb("#08080A");
    public static readonly Color Card = Color.FromArgb("#101012");
    public static readonly Color Accent = Color.FromArgb("#8B5CF6");
    public static readonly Color AccentHover = Color.FromArgb("#7C3AED");
    public static readonly Color Success = Color.FromArgb("#4ADE80");
    public static readonly Color Danger = Color.FromArgb("#F87171");
    public static readonly Color Warning = Color.FromArgb("#FBBF24");
    public static readonly Color TextMain = Colors.White;
    public static readonly Color TextSecondary = Color.FromArgb("#9E9E9E");
    public static readonly Color CardStroke = Color.FromArgb("#12FFFFFF");
    public static readonly Color SubtleBg = Color.FromArgb("#08FFFFFF");
    public static readonly Color SubtleStroke = Color.FromArgb("#0FFFFFFF");

    public static readonly Color ControlBg = Color.FromArgb("#0AFFFFFF");
    public static readonly Color ControlBgActive = Color.FromArgb("#1A8B5CF6");
    public static readonly Color ControlStroke = Color.FromArgb("#0DFFFFFF");
    public static readonly Color ControlStrokeActive = Color.FromArgb("#408B5CF6");

    public static Color WithAlpha(string hex, float alpha) => Color.FromArgb(hex).WithAlpha(alpha);

    public static Label Txt(string text, double size = 13, Color? color = null,
        bool semibold = false, bool bold = false, TextAlignment align = TextAlignment.Start)
    {
        return new Label
        {
            Text = text,
            FontSize = size,
            TextColor = color ?? TextMain,
            FontFamily = bold ? "InterBold" : semibold ? "InterSemiBold" : "Inter",
            HorizontalTextAlignment = align,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
    }

    public static LucideIcon Icon(string name, double size = 16, Color? tint = null, double stroke = 2)
        => new() { Icon = name, Size = size, Tint = tint ?? TextSecondary, Stroke = stroke };

    public static Border Box(double radius = 24, Color? bg = null, Color? stroke = null, double thickness = 1, double pad = 0)
    {
        return new Border
        {
            BackgroundColor = bg ?? Card,
            Stroke = stroke ?? CardStroke,
            StrokeThickness = thickness,
            StrokeShape = new RoundRectangle { CornerRadius = radius },
            Padding = new Thickness(pad)
        };
    }

    public static Border Badge(string icon, string hexBg, string hexBorder, string hexIcon, double box = 40, double iconSize = 16, double radius = 13)
    {
        var iconView = Icon(icon, iconSize, Color.FromArgb(hexIcon));
        return new Border
        {
            BackgroundColor = Color.FromArgb(hexBg),
            Stroke = Color.FromArgb(hexBorder),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = radius },
            WidthRequest = box,
            HeightRequest = box,
            Padding = 0,
            Content = iconView,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        };
    }

    public static Border Pill(string text, Color? bg = null, Color? stroke = null, Color? textColor = null,
        double fontSize = 12, string? icon = null, double padH = 12, double padV = 6, bool semibold = true)
    {
        var stack = new HorizontalStackLayout { Spacing = 5, HorizontalOptions = LayoutOptions.Center };
        if (!string.IsNullOrEmpty(icon))
            stack.Add(Icon(icon, 12, textColor ?? Accent));

        stack.Add(Txt(text, fontSize, textColor ?? Accent, semibold: semibold));

        var border = new Border
        {
            BackgroundColor = bg ?? Accent.WithAlpha(0.15f),
            Stroke = stroke ?? Accent.WithAlpha(0.28f),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(padH, padV),
            Content = stack
        };

        return border;
    }

    /// <summary>Кнопка-пилюля с вибрацией и лёгким откликом на нажатие.</summary>
    public static Border Button(string text, Action onClick, Color? bg = null, Color? textColor = null,
        Color? stroke = null, string? icon = null, double fontSize = 12.5, double padH = 16, double padV = 10,
        bool semibold = true, bool haptic = true)
    {
        var border = Pill(text, bg ?? Accent, stroke ?? Accent, textColor ?? Colors.White, fontSize, icon, padH, padV, semibold);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (haptic) Haptics.Click();
            onClick();
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    public static void Tap(View view, Action onClick, bool haptic = true)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (haptic) Haptics.Click();
            onClick();
        };
        view.GestureRecognizers.Add(tap);
    }
}

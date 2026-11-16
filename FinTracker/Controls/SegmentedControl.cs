using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;
using FinTracker.Services;

namespace FinTracker.Controls;

/// <summary>
/// iOS-подобный сегментированный переключатель Soft Matte: пилюля-подложка и
/// активный сегмент с мягкой заливкой (как в эталонном HTML).
/// Поддерживает обе темы: тёмную Matte и тёплую молочную.
/// </summary>
public sealed class SegmentedControl : ContentView
{
    private static readonly List<WeakReference<SegmentedControl>> Instances = new();
    private static bool _light;
    private static Color _accent = Color.FromArgb("#8B5CF6");

    private readonly Border _shell;
    private readonly Grid _grid = new() { ColumnSpacing = 0 };
    private readonly List<Border> _items = [];
    private readonly List<Label> _labels = [];
    private readonly List<LucideIcon?> _icons = [];

    public event EventHandler<int>? SelectionChanged;

    public int SelectedIndex { get; private set; }

    public double FontSize { get; set; } = 12.5;

    public Thickness ItemPadding { get; set; } = new(13, 6);

    public string? ActiveBackgroundHex { get; set; }

    public Color ActiveTextColor { get; set; } = Colors.White;

    public Color IdleTextColor { get; set; } = Color.FromArgb("#9E9E9E");

    public SegmentedControl()
    {
        _shell = new Border
        {
            BackgroundColor = Color.FromArgb("#0BFFFFFF"),
            Stroke = Color.FromArgb("#0EFFFFFF"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(4),
            Content = _grid
        };

        Content = _shell;
        HorizontalOptions = LayoutOptions.Start;

        Instances.Add(new WeakReference<SegmentedControl>(this));
        ApplyColors();
    }

    /// <summary>Глобально обновляет оформление всех переключателей под текущую тему.</summary>
    public static void ApplyTheme(bool isLight, Color accent)
    {
        _light = isLight;
        _accent = accent;

        for (int i = Instances.Count - 1; i >= 0; i--)
        {
            if (Instances[i].TryGetTarget(out var control))
            {
                control.ApplyColors();
            }
            else
            {
                Instances.RemoveAt(i);
            }
        }
    }

    public void SetItems(IEnumerable<string> items, int selected = 0, bool fullWidth = false)
        => SetItemsCore(items.Select(t => (Text: t, Icon: (string?)null)).ToList(), selected, fullWidth);

    /// <summary>Сегменты с монохромной Lucide-иконкой перед текстом (как в макете).</summary>
    public void SetItemsWithIcons(IReadOnlyList<(string Text, string Icon)> items, int selected = 0, bool fullWidth = false)
        => SetItemsCore(items.Select(t => (Text: t.Text, Icon: (string?)t.Icon)).ToList(), selected, fullWidth);

    private void SetItemsCore(List<(string Text, string? Icon)> list, int selected, bool fullWidth)
    {
        _items.Clear();
        _labels.Clear();
        _icons.Clear();
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();

        for (int i = 0; i < list.Count; i++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(fullWidth ? GridLength.Star : GridLength.Auto));

            var label = new Label
            {
                Text = list[i].Text,
                FontFamily = "InterMedium",
                FontSize = FontSize,
                TextColor = IdleTextColor,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.NoWrap
            };

            LucideIcon? icon = null;
            View content = label;

            if (!string.IsNullOrEmpty(list[i].Icon))
            {
                icon = new LucideIcon
                {
                    Icon = list[i].Icon!,
                    Size = 13,
                    Stroke = 2,
                    Tint = IdleTextColor,
                    VerticalOptions = LayoutOptions.Center
                };
                content = new HorizontalStackLayout
                {
                    Spacing = 6,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { icon, label }
                };
            }

            var cell = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                BackgroundColor = Colors.Transparent,
                Padding = ItemPadding,
                Content = content
            };

            int index = i;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                Haptics.Click();
                Select(index);
            };
            cell.GestureRecognizers.Add(tap);

            _grid.Add(cell, i, 0);
            _items.Add(cell);
            _labels.Add(label);
            _icons.Add(icon);
        }

        HorizontalOptions = fullWidth ? LayoutOptions.Fill : LayoutOptions.Start;
        if (fullWidth) _shell.HorizontalOptions = LayoutOptions.Fill;

        Select(Math.Clamp(selected, 0, Math.Max(list.Count - 1, 0)), raise: false);
    }

    public void Select(int index, bool raise = true)
    {
        if (_items.Count == 0) return;

        index = Math.Clamp(index, 0, _items.Count - 1);
        SelectedIndex = index;

        ApplyColors();

        if (raise) SelectionChanged?.Invoke(this, index);
    }

    private void ApplyColors()
    {
        _shell.BackgroundColor = _light ? Color.FromArgb("#ECE9E1") : Color.FromArgb("#0BFFFFFF");
        _shell.Stroke = _light ? Color.FromArgb("#121C1917") : Color.FromArgb("#0EFFFFFF");

        Color activeBg = !string.IsNullOrEmpty(ActiveBackgroundHex)
            ? Color.FromArgb(ActiveBackgroundHex)
            : (_light ? Colors.White : Color.FromArgb("#1FFFFFFF"));

        Color activeText = ActiveTextColor == Colors.White && _light
            ? Color.FromArgb("#1C1917")
            : ActiveTextColor;

        Color idleText = _light ? Color.FromArgb("#3F3A36") : IdleTextColor;

        for (int i = 0; i < _items.Count; i++)
        {
            bool active = i == SelectedIndex;

            _items[i].BackgroundColor = active ? activeBg : Colors.Transparent;
            _items[i].Shadow = active
                ? new Shadow
                {
                    Brush = Colors.Black,
                    Offset = new Point(0, 2),
                    Radius = 6,
                    Opacity = _light ? 0.08f : 0.30f
                }
                : null;

            _labels[i].TextColor = active ? activeText : idleText;
            _labels[i].FontFamily = active ? "InterSemiBold" : "InterMedium";
            if (_icons[i] is not null) _icons[i]!.Tint = active ? activeText : idleText;
        }
    }
}

using Microsoft.Maui.Controls.Shapes;
using MPath = Microsoft.Maui.Controls.Shapes.Path;

namespace FinTracker.Controls;

/// <summary>
/// Иконка Lucide, нарисованная векторным <see cref="Path"/>.
/// В отличие от картинки её можно перекрашивать как обычный элемент (Stroke),
/// поэтому бейджи категорий выглядят точно как в эталонном макете.
/// </summary>
public sealed class LucideIcon : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(LucideIcon), "circle-off",
        propertyChanged: (b, _, _) => ((LucideIcon)b).Apply());

    public static readonly BindableProperty TintProperty = BindableProperty.Create(
        nameof(Tint), typeof(Color), typeof(LucideIcon), Colors.White,
        propertyChanged: (b, _, _) => ((LucideIcon)b).Apply());

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(LucideIcon), 16d,
        propertyChanged: (b, _, _) => ((LucideIcon)b).ApplySize());

    public static readonly BindableProperty StrokeProperty = BindableProperty.Create(
        nameof(Stroke), typeof(double), typeof(LucideIcon), 2d,
        propertyChanged: (b, _, _) => ((LucideIcon)b).Apply());

    private readonly MPath _path = new()
    {
        Aspect = Stretch.Uniform,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Fill = Colors.Transparent
    };

    public LucideIcon()
    {
        WidthRequest = Size;
        HeightRequest = Size;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        InputTransparent = true;
        Content = _path;
        Apply();
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Color Tint
    {
        get => (Color)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double Stroke
    {
        get => (double)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    private void ApplySize()
    {
        WidthRequest = Size;
        HeightRequest = Size;
    }

    private void Apply()
    {
        _path.Stroke = Tint;
        _path.StrokeThickness = Stroke;

        string key = string.IsNullOrWhiteSpace(Icon) ? "circle-off" : Icon;
        if (!LucideData.Paths.TryGetValue(key, out string? data))
            data = LucideData.Paths["circle-off"];

        try
        {
            _path.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!;
        }
        catch
        {
            _path.Data = null;
        }
    }
}

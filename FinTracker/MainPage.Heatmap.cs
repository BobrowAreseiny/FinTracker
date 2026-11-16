using System.Globalization;
using System.Text;
using System.Text.Json;
using FinTracker.Charts;
using FinTracker.Controls;
using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace FinTracker;

public partial class MainPage : ContentPage
{
private void RenderHeatmap()
{
    var today = DateTime.Today;
    int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

    var daily = new decimal[daysInMonth + 1];
    foreach (var t in _transactions)
    {
        if (t.Date.Year != today.Year || t.Date.Month != today.Month) continue;
        if (_accountFilter != "all" && !MatchesAccount(t, _accountFilter)) continue;
        if (IsTransfer(t) || IsDebtLoan(t)) continue;
        if (!IsExpense(t)) continue;
        daily[t.Date.Day] += t.Amount;
    }

    decimal max = daily.Max();
    int activeDays = daily.Count(d => d > 0);

    HeatmapGrid.Children.Clear();
    HeatmapGrid.RowDefinitions.Clear();
    _heatmapCells.Clear();

    int rows = (int)Math.Ceiling(daysInMonth / 7.0);
    for (int r = 0; r < rows; r++) HeatmapGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

    for (int day = 1; day <= daysInMonth; day++)
    {
        decimal value = daily[day];
        double share = max > 0 ? (double)(value / max) : 0;

        Color bg;
        Color fg;

        if (share > 0.6)
        {
            bg = _accentColor;
            fg = Colors.White;
        }
        else if (share > 0.3)
        {
            bg = _accentColor.WithAlpha(0.60f);
            fg = Colors.White;
        }
        else if (share > 0)
        {
            bg = _accentColor.WithAlpha(0.22f);
            fg = PrimaryTextColor;
        }
        else
        {
            bg = SubtleFillColor;
            fg = SecondaryTextColor;
        }

        bool isSelected = _selectedHeatDay == day;

        var cell = new Border
        {
            BackgroundColor = bg,
            Stroke = isSelected ? PrimaryTextColor : Colors.Transparent,
            StrokeThickness = 1.2,
            StrokeShape = new RoundRectangle { CornerRadius = 9 },
            HeightRequest = 36,
            Scale = isSelected ? 1.06 : 1.0,
            Padding = 0,
            Content = new Label
            {
                Text = day.ToString(),
                FontFamily = "InterSemiBold",
                FontSize = 11,
                TextColor = fg,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        int dayNumber = day;
        decimal dayValue = value;
        HapticsClick(cell, () => SelectHeatDay(dayNumber, dayValue));

        Grid.SetColumn(cell, (day - 1) % 7);
        Grid.SetRow(cell, (day - 1) / 7);
        HeatmapGrid.Children.Add(cell);
        _heatmapCells.Add(cell);
    }

    if (_selectedHeatDay >= 1 && _selectedHeatDay <= daysInMonth)
    {
        HeatmapInfoLabel.Text = $"{_selectedHeatDay} {MonthShort[today.Month - 1]}: потрачено {Money(daily[_selectedHeatDay])}";
    }
    else
    {
        HeatmapInfoLabel.Text = $"{activeDays} активных дней • Нажмите для обзора";
    }
}

private void SelectHeatDay(int day, decimal value)
{
    _selectedHeatDay = day;

    for (int i = 0; i < _heatmapCells.Count; i++)
    {
        bool selected = i + 1 == day;
        _heatmapCells[i].Stroke = selected ? PrimaryTextColor : Colors.Transparent;
        _heatmapCells[i].Scale = selected ? 1.06 : 1.0;
    }

    HeatmapInfoLabel.Text = $"{day} {MonthShort[DateTime.Today.Month - 1]}: потрачено {Money(value)}";
}

private void OnDonutTapped(object? sender, TappedEventArgs e)
{
    var point = e.GetPosition(DonutChart);
    if (point is null) return;

    var items = CategoryTotals(_analyticsFlow);
    if (items.Count == 0) return;

    double cx = DonutChart.Width / 2, cy = DonutChart.Height / 2;
    double dx = point.Value.X - cx, dy = point.Value.Y - cy;
    double radius = Math.Min(DonutChart.Width, DonutChart.Height) / 2;

    double distance = Math.Sqrt(dx * dx + dy * dy);
    if (distance > radius || distance < radius * 0.45) return;

    double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
    angle = (angle + 90 + 360) % 360;

    double total = (double)items.Sum(i => i.Value);
    double acc = 0;

    for (int i = 0; i < items.Count; i++)
    {
        acc += (double)items[i].Value / total * 360;
        if (angle <= acc)
        {
            _selectedCatIndex = _selectedCatIndex == i ? null : i;
            RenderDonut();
            return;
        }
    }
}

// ================= СЧЕТА И ДОЛГИ =================

}

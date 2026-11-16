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
private List<(CategoryDef Cat, decimal Value)> CategoryTotals(string flow)
{
    var (start, end) = PeriodRange();
    bool wantIncome = flow == "income";
    var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    foreach (var t in _transactions)
    {
        if (t.Date < start || t.Date >= end) continue;
        if (_accountFilter != "all" && !MatchesAccount(t, _accountFilter)) continue;
        if (IsTransfer(t) || IsDebtLoan(t)) continue;
        if (wantIncome ? !IsIncome(t) : !IsExpense(t)) continue;

        string name = string.IsNullOrWhiteSpace(t.Category) ? "Другое" : t.Category!;
        map[name] = map.TryGetValue(name, out decimal cur) ? cur + t.Amount : t.Amount;
    }

    return map.OrderByDescending(k => k.Value)
    .Select(k => (ResolveCategory(k.Key), k.Value))
    .ToList();
}

private void RenderAnalytics()
{
    var (income, expense) = PeriodTotals();

    AnIncomeLabel.Text = Money(income);
    AnExpenseLabel.Text = Money(expense);

    decimal ratio = income > 0 ? expense / income : 0m;
    AnRatioLabel.Text = income > 0
    ? $"{Math.Round(ratio * 100, MidpointRounding.AwayFromZero)}% дохода"
    : expense > 0 ? "нет дохода" : "0% дохода";

    int percent = income > 0 ? (int)Math.Min(Math.Round(expense / income * 100), 100) : (expense > 0 ? 100 : 0);
    AbsoluteLayout.SetLayoutBounds(AnExpenseFill, new Rect(0, 0, Math.Clamp(percent / 100.0, 0, 1), 8));
    AbsoluteLayout.SetLayoutFlags(AnExpenseFill, AbsoluteLayoutFlags.WidthProportional);

    AnalyticsAdviceLabel.Text = BuildAnalyticsAdvice();

    RenderDonut();
    RenderDynamics();
}

private string BuildAnalyticsAdvice()
{
    var (income, expense) = PeriodTotals();
    if (expense <= 0) return "Расходов за период нет.";

    var categories = CategoryTotals("expense");
    string topName = categories.Count > 0 ? Trim(categories[0].Cat.Cat, 20) : "крупные траты";
    decimal topValue = categories.Count > 0 ? categories[0].Value : 0m;

    decimal factor = _period switch { "week" => 0.25m, "year" => 12m, _ => 1m };
    decimal limit = Math.Max(_monthlyLimit * factor, 1m);
    decimal remain = limit - expense;

    if (remain < 0)
    {
        decimal over = -remain;
        if (topValue > 0 && topValue >= over)
        {
            int pct = (int)Math.Round(over / topValue * 100, MidpointRounding.AwayFromZero);
            return $"Лимит превышен на {Money(over)}. Сократите «{topName}» на {pct}%.";
        }

        return $"Лимит превышен на {Money(over)}. Начните с «{topName}».";
    }

    if (income > 0 && expense > income)
    {
        return $"Расходы выше доходов на {Money(expense - income)}. Сократите «{topName}».";
    }

    var today = DateTime.Today;
    int daysLeft = _period switch
    {
        "week" => Math.Max(7 - (((int)today.DayOfWeek + 6) % 7), 1),
        "year" => Math.Max((new DateTime(today.Year, 12, 31) - today).Days + 1, 1),
        _ => Math.Max(DateTime.DaysInMonth(today.Year, today.Month) - today.Day + 1, 1)
    };

    if (expense >= limit * 0.8m)
    {
        decimal daily = remain / Math.Max(daysLeft, 1);
        return $"До лимита {Money(remain)}. Безопасный темп — {Money(daily)}/день. Следите за «{topName}».";
    }

    if (topValue > 0)
    {
        int share = (int)Math.Round(topValue / expense * 100, MidpointRounding.AwayFromZero);
        return $"В пределах лимита. Осталось {Money(remain)}. Крупнейшая категория — «{topName}» ({share}%).";
    }

    return $"В пределах лимита. Осталось {Money(remain)}.";
}

private void RenderDonut()
{
    var items = CategoryTotals(_analyticsFlow);
    CatCountLabel.Text = items.Count + " кат.";

    bool empty = items.Count == 0;
    decimal total = items.Sum(i => i.Value);

    var segments = empty
    ? new List<(string Name, float Value, Color Color)> { ("Нет данных", 1f, _isLightTheme ? Color.FromArgb("#E5E0D5") : Color.FromArgb("#374151")) }
    : items.Select(i => (i.Cat.Cat, (float)i.Value, Color.FromArgb(i.Cat.ChartColor))).ToList();

    if (_selectedCatIndex is int sel && (sel < 0 || sel >= items.Count)) _selectedCatIndex = null;

    DonutChart.Drawable = new DonutChartDrawable
    {
        Segments = segments,
        SelectedIndex = _selectedCatIndex ?? -1
    };
    DonutChart.Invalidate();

    bool selected = _selectedCatIndex is int index && index < items.Count;
    DonutCenterTitle.Text = selected
    ? items[_selectedCatIndex!.Value].Cat.Cat
    : _analyticsFlow == "expense" ? "Всего расходов" : "Всего доходов";
    DonutCenterValue.Text = Money(selected ? items[_selectedCatIndex!.Value].Value : total);

    CategoryBreakdownList.Children.Clear();

    for (int i = 0; i < items.Count; i++)
    {
        int pct = total > 0 ? (int)Math.Round(items[i].Value / total * 100) : 0;

        if (i > 0)
        {
            CategoryBreakdownList.Add(new BoxView
            {
                HeightRequest = 1,
                Color = HairlineStrokeColor,
                Margin = new Thickness(0, 4, 0, 8)
            });
        }

        CategoryBreakdownList.Add(BuildBreakdownRow(items[i].Cat, items[i].Value, pct, i));
    }
}

private View BuildBreakdownRow(CategoryDef cat, decimal value, int percent, int index)
{
    bool selected = _selectedCatIndex == index;

    var top = new Grid
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
        Margin = new Thickness(0, 0, 0, 6)
    };

    var left = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center };
    left.Add(new Border
    {
        WidthRequest = 10,
        HeightRequest = 10,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 999 },
        BackgroundColor = Color.FromArgb(cat.ChartColor),
        VerticalOptions = LayoutOptions.Center
    });
    left.Add(new Label
    {
        Text = cat.Cat,
        FontFamily = "InterMedium",
        FontSize = 13.5,
        TextColor = PrimaryTextColor,
        VerticalTextAlignment = TextAlignment.Center
    });

    var right = new HorizontalStackLayout { Spacing = 12, VerticalOptions = LayoutOptions.Center };
    right.Add(new Label
    {
        Text = percent + "%",
        FontFamily = "Inter",
        FontSize = 12,
        TextColor = SecondaryTextColor,
        VerticalTextAlignment = TextAlignment.Center
    });
    right.Add(new Label
    {
        Text = Money(value),
        FontFamily = "InterSemiBold",
        FontSize = 13.5,
        TextColor = PrimaryTextColor,
        VerticalTextAlignment = TextAlignment.Center
    });

    top.Add(left, 0, 0);
    top.Add(right, 1, 0);

    var track = new Border
    {
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 999 },
        BackgroundColor = TrackFillColor
    };

    var fill = new Border
    {
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 999 },
        BackgroundColor = Color.FromArgb(cat.ChartColor),
        HorizontalOptions = LayoutOptions.Start,
        WidthRequest = 0
    };

    var bar = new Grid { HeightRequest = 6 };
    bar.Add(track);
    bar.Add(fill);

    bar.SizeChanged += (_, _) =>
    {
        double width = bar.Width * percent / 100.0;
        fill.WidthRequest = percent > 0 ? Math.Max(width, 6) : 0;
    };

    var content = new VerticalStackLayout { Spacing = 6 };
    content.Add(top);
    content.Add(bar);

    var row = new Border
    {
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 12 },
        BackgroundColor = selected ? SubtleFillColor : Colors.Transparent,
        Padding = new Thickness(selected ? 10 : 2, 10),
        Content = content,
        Margin = new Thickness(0, 0, 0, 2)
    };

    HapticsClick(row, () =>
    {
        _selectedCatIndex = _selectedCatIndex == index ? null : index;
        RenderDonut();
    });

    return row;
}

}

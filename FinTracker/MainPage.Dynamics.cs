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
private static readonly (string Key, string Title, string Sub, string Icon)[] HubModes =
[
("radar", "Радар", "Факт и план", "compass"),
("waffle", "Вафля", "Доли по клеткам", "layout-grid"),
("ranking", "Рейтинг", "Суммы категорий", "target")
 ];

private static string HubTitle(string mode) => HubModes.FirstOrDefault(m => m.Key == mode).Title ?? "Динамика";

private static string HubSub(string mode) => HubModes.FirstOrDefault(m => m.Key == mode).Sub ?? string.Empty;

private void RenderDynamics()
{
    bool flowExpense = _analyticsFlow == "expense";
    var items = CategoryTotals(_analyticsFlow);
    decimal total = items.Sum(i => i.Value);

    DynTotalLabel.Text = Money(total);
    DynTotalLabel.TextColor = flowExpense ? _accentColor : Ui.Success;

    var (start, end) = PeriodRange();
    var today = DateTime.Today.AddDays(1);
    var finish = end < today ? end : today;
    int elapsedDays = Math.Max((int)Math.Ceiling((finish - start).TotalDays), 1);
    decimal avgPerDay = total / elapsedDays;
    DynAvgLabel.Text = Money(avgPerDay);

    // Живой расчёт статуса среднего в день относительно лимита (без заглушек)
    if (_dynAvgStatusLabel is not null)
    {
        if (!flowExpense)
        {
            _dynAvgStatusLabel.Text = total > 0 ? "Стабильный приток" : "Нет поступлений";
            _dynAvgStatusLabel.TextColor = total > 0 ? Ui.Success : SecondaryTextColor;
        }
        else
        {
            decimal factor = _period switch { "week" => 0.25m, "year" => 12m, _ => 1m };
            int totalPeriodDays = Math.Max((int)Math.Round((end - start).TotalDays), 1);
            decimal allowedDaily = (_monthlyLimit * factor) / totalPeriodDays;

            if (avgPerDay <= allowedDaily * 0.9m)
            {
                _dynAvgStatusLabel.Text = "В пределах нормы";
                _dynAvgStatusLabel.TextColor = Ui.Success;
            }
            else if (avgPerDay <= allowedDaily * 1.1m)
            {
                _dynAvgStatusLabel.Text = "На уровне лимита";
                _dynAvgStatusLabel.TextColor = Ui.Warning;
            }
            else
            {
                _dynAvgStatusLabel.Text = "Выше дневной нормы";
                _dynAvgStatusLabel.TextColor = Ui.Danger;
            }
        }
    }

    // Расчёт пикового дня недели и ведущих категорий этого дня
    var weekdays = new decimal[7];
    var weekdayCatTotals = new Dictionary<string, decimal>[7];
    for (int i = 0; i < 7; i++) weekdayCatTotals[i] = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    foreach (var t in _transactions)
    {
        if (t.Date < start || t.Date >= end) continue;
        if (_accountFilter != "all" && !MatchesAccount(t, _accountFilter)) continue;
        if (IsTransfer(t) || IsDebtLoan(t)) continue;
        if (flowExpense ? !IsExpense(t) : !IsIncome(t)) continue;

        int dow = ((int)t.Date.DayOfWeek + 6) % 7;
        weekdays[dow] += t.Amount;

        string catName = string.IsNullOrWhiteSpace(t.Category) ? "Другое" : t.Category!;
        weekdayCatTotals[dow][catName] = weekdayCatTotals[dow].TryGetValue(catName, out decimal cur) ? cur + t.Amount : t.Amount;
    }

    decimal peakValue = weekdays.Max();
    int peakIndex = Array.IndexOf(weekdays, peakValue);

    if (peakValue > 0)
    {
        DynPeakLabel.Text = DayFull[peakIndex];
        var topCatsForPeak = weekdayCatTotals[peakIndex]
        .OrderByDescending(kv => kv.Value)
        .Take(2)
        .Select(kv => kv.Key.Split(' ')[0])
        .ToList();
        DynPeakHint.Text = topCatsForPeak.Count > 0 ? string.Join(" и ", topCatsForPeak) : items[0].Cat.Cat;
    }
    else
    {
        DynPeakLabel.Text = "—";
        DynPeakHint.Text = "Нет данных";
    }
    DynPeakHint.TextColor = _accentColor;

    string chartTitle = HubTitle(_hubMode);
    string chartSub = HubSub(_hubMode);

    if (_hubMode == "bars")
    {
        chartTitle = _period switch
        {
            "week" => "По дням недели",
            "year" => "По месяцам",
            _ => "По дням месяца"
        };
        chartSub = "Сумма";
    }
    else if (_hubMode == "radar" && _analyticsFlow == "income")
    {
        chartTitle = "Доходы";
        chartSub = "Категории";
    }

    HubChartTitle.Text = chartTitle;
    HubChartSub.Text = chartSub + " • " + PeriodRangeLabel();

    RenderHubPills();
    RenderHubChart(items, flowExpense);
    HubChartInsight.Text = BuildHubInsight(items, flowExpense);
    RenderHubLegend(items, flowExpense);
    RenderHeatmap();
}

private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

private void UpdateHubDonutCenter(List<(CategoryDef Cat, decimal Value)> items)
{
    if (HubChartCenter is null) return;

    HubChartCenter.IsVisible = _hubMode == "donut";
    if (_hubMode != "donut") return;

    if (_hubDonutIndex is int index && index >= 0 && index < items.Count)
    {
        HubChartCenterTitle.Text = Trim(items[index].Cat.Cat, 14);
        HubChartCenterValue.Text = Money(items[index].Value);
    }
    else
    {
        HubChartCenterTitle.Text = _analyticsFlow == "expense" ? "Всего расходов" : "Всего доходов";
        HubChartCenterValue.Text = Money(items.Sum(i => i.Value));
    }
}

private void OnHubChartTapped(object? sender, TappedEventArgs e)
{
    if (_hubMode == "waffle")
    {
        var point = e.GetPosition(HubChart);
        if (point is null) return;

        if (HubChart.Drawable is WaffleChartDrawable waffle)
        {
            int index = waffle.HitTest((float)point.Value.X, (float)point.Value.Y);
            _hubWaffleIndex = index >= 0 && _hubWaffleIndex != index ? index : null;
            RenderDynamics();
        }

        return;
    }

    // Остальные графики по тапу открывают круговую диаграмму на «Категориях».
    Haptics.Click();
    SetAnalyticsMode("categories");
    _ = MainScroll.ScrollToAsync(0, 0, true);
}

private string BuildHubInsight(List<(CategoryDef Cat, decimal Value)> items, bool flowExpense)
{
    if (items.Count == 0) return "Нет данных.";

    decimal total = items.Sum(i => i.Value);
    decimal top3 = items.Take(3).Sum(i => i.Value);
    int share = total > 0 ? (int)Math.Round(top3 / total * 100, MidpointRounding.AwayFromZero) : 0;

    if (_hubMode == "radar" && flowExpense)
    {
        var (income, expense) = PeriodTotals();
        if (income > 0)
        {
            int pct = (int)Math.Round(expense / income * 100, MidpointRounding.AwayFromZero);
            return $"Расходы {pct}% дохода";
        }
    }

    return $"Топ-3: {share}% · {Money(total)}";
}

private void RenderHubLegend(List<(CategoryDef Cat, decimal Value)> items, bool flowExpense)
{
    HubChartLegend.Children.Clear();

    // У рейтинга уже есть подписи и суммы прямо на графике.
    if (_hubMode == "ranking") return;

    if (items.Count == 0)
    {
        HubChartLegend.Add(new Label
        {
            Text = "Нет данных.",
            FontFamily = "Inter",
            FontSize = 11.5,
            TextColor = SecondaryTextColor
        });
        return;
    }

    HubChartLegend.Add(new Label
    {
        Text = "Категории",
        FontFamily = "InterMedium",
        FontSize = 11.5,
        TextColor = SecondaryTextColor,
        Margin = new Thickness(0, 0, 0, 8)
    });

    decimal total = items.Sum(i => i.Value);
    foreach (var item in items.Take(6))
    {
        int pct = total > 0 ? (int)Math.Round(item.Value / total * 100, MidpointRounding.AwayFromZero) : 0;

        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };

        row.Add(new Border
        {
            WidthRequest = 9,
            HeightRequest = 9,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            BackgroundColor = Color.FromArgb(item.Cat.ChartColor),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        row.Add(new Label
        {
            Text = Trim(item.Cat.Cat, 18),
            FontFamily = "Inter",
            FontSize = 12,
            TextColor = PrimaryTextColor,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        }, 1, 0);

        row.Add(new Label
        {
            Text = Money(item.Value),
            FontFamily = "InterSemiBold",
            FontSize = 12,
            TextColor = PrimaryTextColor,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        }, 2, 0);

        row.Add(new Label
        {
            Text = pct + "%",
            FontFamily = "Inter",
            FontSize = 11,
            TextColor = SecondaryTextColor,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 40
        }, 3, 0);

        HubChartLegend.Add(row);
    }

    if (items.Count > 6)
    {
        HubChartLegend.Add(new Label
        {
            Text = $"+{items.Count - 6}",
            FontFamily = "Inter",
            FontSize = 11,
            TextColor = SecondaryTextColor,
            Margin = new Thickness(17, 0, 0, 0)
        });
    }
}

private void RenderHubPills()
{
    const int columns = 3;
    int rows = (HubModes.Length + columns - 1) / columns;

    HubChartPillsGrid.Children.Clear();
    HubChartPillsGrid.ColumnDefinitions.Clear();
    HubChartPillsGrid.RowDefinitions.Clear();

    for (int c = 0; c < columns; c++)
        HubChartPillsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
    for (int r = 0; r < rows; r++)
        HubChartPillsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

    for (int i = 0; i < HubModes.Length; i++)
    {
        var mode = HubModes[i];
        bool active = mode.Key == _hubMode;

        var content = new HorizontalStackLayout
        {
            Spacing = 5,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        content.Add(new LucideIcon
        {
            Icon = mode.Icon,
            Size = 13,
            Tint = active ? PrimaryTextColor : ChartTheme.Label,
            VerticalOptions = LayoutOptions.Center
        });
        content.Add(new Label
        {
            Text = mode.Key switch
            {
                "donut" => "Круг",
                "trend" => "Тренд",
                "stack" => "Стек",
                "waffle" => "Вафля",
                "ranking" => "Рейтинг",
                _ => "Радар"
            },
            FontFamily = active ? "InterSemiBold" : "InterMedium",
            FontSize = 11.5,
            TextColor = active ? PrimaryTextColor : ChartTheme.Label,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap
        });

        var pill = new Border
        {
            BackgroundColor = active ? ActiveControlBg : SubtleFillColor,
            Stroke = active ? ActiveControlStroke : HairlineStrokeColor,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(7, 6),
            Content = content,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        string key = mode.Key;
        HapticsClick(pill, () =>
        {
            _hubMode = key;
            RenderDynamics();
        });

        HubChartPillsGrid.Add(pill, i % columns, i / columns);
    }
}

private void RenderHubChart(List<(CategoryDef Cat, decimal Value)> items, bool flowExpense)
{
    if (HubChartCenter is not null) HubChartCenter.IsVisible = false;

    var (start, end) = PeriodRange();
    var today = DateTime.Today.AddDays(1);
    var finish = end < today ? end : today;
    if (finish <= start) finish = start.AddDays(1);

    double spanDays = Math.Max((finish - start).TotalDays, 1);
    Color flowAccent = flowExpense ? _accentColor : Ui.Success;

    decimal SliceSum(DateTime from, DateTime to)
    {
        decimal sum = 0m;
        foreach (var t in _transactions)
        {
            if (t.Date < from || t.Date >= to) continue;
            if (_accountFilter != "all" && !MatchesAccount(t, _accountFilter)) continue;
            if (IsTransfer(t) || IsDebtLoan(t)) continue;
            if (flowExpense ? !IsExpense(t) : !IsIncome(t)) continue;
            sum += t.Amount;
        }
        return sum;
    }

    string SliceLabel(DateTime moment, bool isLast = false)
    {
        if (isLast && _period != "year") return "Сег";
        return _period switch
        {
            "year" => Capitalize(MonthShort[moment.Month - 1]),
            "week" => Capitalize(DayShort[((int)moment.DayOfWeek + 6) % 7]),
            _ => moment.Day.ToString()
        };
    }

    switch (_hubMode)
    {
        case "donut":
            {
                HubChart.HeightRequest = 200;

                if (_hubDonutIndex is int selectedIndex && selectedIndex >= items.Count)
                    _hubDonutIndex = null;

                var segments = items
                .Select(i => (Name: i.Cat.Cat, Value: (float)BynToDisplay(i.Value), Color: Color.FromArgb(i.Cat.ChartColor)))
                .ToList();

                if (segments.Count == 0)
                    segments.Add(("Нет данных", 1f, Color.FromArgb("#6B7280")));

                HubChart.Drawable = new DonutChartDrawable
                {
                    Segments = segments,
                    SelectedIndex = _hubDonutIndex ?? -1
                };

                UpdateHubDonutCenter(items);
                break;
            }
        case "bars":
            {
                HubChart.HeightRequest = 185;
                var bars = new List<(string Label, float Value)>();

                if (_period == "year")
                {
                    for (int m = 0; m < 12; m++)
                    {
                        var from = new DateTime(start.Year, m + 1, 1);
                        var to = from.AddMonths(1);
                        if (from >= finish) break;
                        if (to > finish) to = finish;
                        bars.Add((Capitalize(MonthShort[m]), (float)BynToDisplay(SliceSum(from, to))));
                    }
                }
                else if (_period == "week")
                {
                    for (int d = 0; d < 7; d++)
                    {
                        var from = start.AddDays(d);
                        var to = from.AddDays(1);
                        bars.Add((Capitalize(DayShort[d]), (float)BynToDisplay(SliceSum(from, to))));
                    }
                }
                else
                {
                    int days = Math.Max((finish - start).Days, 1);
                    for (int d = 0; d < days; d++)
                    {
                        var from = start.AddDays(d);
                        var to = from.AddDays(1);
                        bool showLabel = d == 0 || (d + 1) % 5 == 0 || d == days - 1;
                        bars.Add((showLabel ? (d + 1).ToString() : string.Empty, (float)BynToDisplay(SliceSum(from, to))));
                    }
                }

                HubChart.Drawable = new BarsChartDrawable { Bars = bars, Accent = flowAccent };
                break;
            }
        case "trend":
            {
                HubChart.HeightRequest = 185;
                var values = new List<float>();
                var labels = new List<string>();
                decimal acc = 0m;
                int points = 7;

                for (int i = 0; i < points; i++)
                {
                    var from = start.AddDays(spanDays * i / points);
                    var to = i == points - 1 ? finish : start.AddDays(spanDays * (i + 1) / points);
                    acc += SliceSum(from, to);
                    values.Add((float)BynToDisplay(acc));
                    labels.Add(SliceLabel(from, i == points - 1));
                }

                HubChart.Drawable = new AreaChartDrawable { Values = values, Labels = labels, Stroke = flowAccent, FillBaseColor = CardSurfaceColor };
                break;
            }
        case "stack":
            {
                HubChart.HeightRequest = 185;
                var top = items.Take(5).ToList();
                if (top.Count == 0) top.Add((ResolveCategory("Другое"), 0m));

                var columns = new List<(string Label, (float Value, Color Color)[] Parts)>();

                for (int d = 0; d < 7; d++)
                {
                    var parts = new List<(float Value, Color Color)>();
                    foreach (var cat in top)
                    {
                        decimal sum = _transactions
                        .Where(t => t.Date >= start && t.Date < finish)
                        .Where(t => ((int)t.Date.DayOfWeek + 6) % 7 == d)
                        .Where(t => _accountFilter == "all" || MatchesAccount(t, _accountFilter))
                        .Where(t => !IsTransfer(t) && !IsDebtLoan(t))
                        .Where(t => flowExpense ? IsExpense(t) : IsIncome(t))
                        .Where(t => string.Equals(t.Category, cat.Cat.Cat, StringComparison.OrdinalIgnoreCase))
                        .Sum(t => t.Amount);

                        parts.Add(((float)BynToDisplay(sum), Color.FromArgb(cat.Cat.ChartColor)));
                    }

                    columns.Add((Capitalize(DayShort[d]), parts.ToArray()));
                }

                HubChart.Drawable = new StackChartDrawable { Columns = columns };
                break;
            }
        case "sankey":
            {
                HubChart.HeightRequest = 190;

                var (income, expense) = PeriodTotals();
                var incomeCats = CategoryTotals("income");
                var expenseCats = CategoryTotals("expense");

                var rightTargets = expenseCats.Take(3)
                .Select(c => (Trim(c.Cat.Cat, 10), Color.FromArgb(c.Cat.ChartColor)))
                .ToList();

                while (rightTargets.Count < 3)
                {
                    int idx = rightTargets.Count;
                    var fallbackCat = _expenseCategories.ElementAtOrDefault(idx);
                    rightTargets.Add((
                    fallbackCat is not null ? Trim(fallbackCat.Cat, 10) : "Прочее",
                    fallbackCat is not null ? Color.FromArgb(fallbackCat.ChartColor) : _accentColor));
                }

                HubChart.Drawable = new SankeyChartDrawable
                {
                    LeftTop = incomeCats.Count > 0 ? Trim(incomeCats[0].Cat.Cat, 9) : "Зарплата",
                    LeftBottom = incomeCats.Count > 1 ? Trim(incomeCats[1].Cat.Cat, 9) : "Фриланс",
                    BudgetValue = Money(income > 0 ? income : expense),
                    Right = rightTargets.ToArray()
                };
                break;
            }
        case "waterfall":
            {
                HubChart.HeightRequest = 190;

                var (income, expense) = PeriodTotals();
                var walletsAtStart = WalletBalances(start);
                decimal startBalance = Math.Max(ActiveBalance(walletsAtStart), 0m);
                var expenseCats = CategoryTotals("expense");

                decimal firstExp = expenseCats.Count > 0 ? expenseCats[0].Value : expense * 0.6m;
                decimal restExp = Math.Max(expense - firstExp, 0m);
                decimal endBalance = Math.Max(startBalance + income - expense, 0m);

                string firstLabel = "–" + Trim(expenseCats.Count > 0 ? expenseCats[0].Cat.Cat.Split(' ')[0] : "Траты", 7);
                string secondLabel = expenseCats.Count > 1
                ? "–" + Trim(expenseCats[1].Cat.Cat.Split(' ')[0], 7)
                : "–Прочее";

                HubChart.Drawable = new WaterfallChartDrawable
                {
                    Steps =
                [
                ("Старт", Money(startBalance), 108, 32, Color.FromArgb("#6366F1")),
("+Доход", "+" + Money(income), 26, 82, Color.FromArgb("#22C55E")),
(firstLabel, "–" + Money(firstExp), 26, 28, Color.FromArgb("#EF4444")),
(secondLabel, "–" + Money(restExp), 54, 32, Color.FromArgb("#EF4444")),
("Итог", Money(endBalance), 56, 84, _accentColor)
                 ]
                };
                break;
            }
        case "waffle":
            {
                HubChart.HeightRequest = 210;

                var cells = items
                .Select(i => (Name: i.Cat.Cat, Value: (float)BynToDisplay(i.Value), Color: Color.FromArgb(i.Cat.ChartColor)))
                .ToList();

                HubChart.Drawable = new WaffleChartDrawable
                {
                    Items = cells,
                    SelectedIndex = _hubWaffleIndex ?? -1,
                    SelectionColor = _isLightTheme ? Color.FromArgb("#1C1917") : Colors.White
                };
                break;
            }
        case "treemap":
            {
                HubChart.HeightRequest = 200;

                var slices = items
                .Take(7)
                .Select(i => (Name: Trim(i.Cat.Cat, 12), Value: (float)BynToDisplay(i.Value), Color: Color.FromArgb(i.Cat.ChartColor)))
                .ToList();

                decimal rest = items.Skip(7).Sum(i => i.Value);
                if (rest > 0)
                {
                    slices.Add(("Другое", (float)BynToDisplay(rest), Color.FromArgb("#6B7280")));
                }

                HubChart.Drawable = new TreemapChartDrawable
                {
                    Items = slices,
                    SelectedIndex = _hubTreemapIndex ?? -1,
                    SelectionColor = _isLightTheme ? Color.FromArgb("#1C1917") : Colors.White
                };
                break;
            }
        case "ranking":
            {
                HubChart.HeightRequest = 200;

                var rows = items
                .Take(6)
                .Select(i => (Name: Trim(i.Cat.Cat, 15), Value: (float)BynToDisplay(i.Value), Color: Color.FromArgb(i.Cat.ChartColor)))
                .ToList();

                HubChart.Drawable = new RankingBarsDrawable { Items = rows };
                break;
            }
        default:
            {
                HubChart.HeightRequest = 195;

                var axes = new List<string>();
                var factValues = new List<decimal>();

                foreach (var c in items.Take(5))
                {
                    axes.Add(Trim(c.Cat.Cat.Split(' ')[0], 9));
                    factValues.Add(c.Value);
                }

                foreach (var fallback in flowExpense ? _expenseCategories : _incomeCategories)
                {
                    if (axes.Count >= 5) break;
                    string shortName = Trim(fallback.Cat.Split(' ')[0], 9);
                    if (axes.Contains(shortName)) continue;
                    axes.Add(shortName);
                    factValues.Add(0m);
                }

                while (axes.Count < 5)
                {
                    axes.Add("Прочее");
                    factValues.Add(0m);
                }

                decimal factor = _period switch { "week" => 0.25m, "year" => 12m, _ => 1m };
                decimal catPlanAllowance = flowExpense
                ? Math.Max(_monthlyLimit * factor / 4m, 1m)
                : Math.Max(factValues.DefaultIfEmpty(0m).Max(), 1m);
                decimal maxRef = Math.Max(factValues.DefaultIfEmpty(0m).Max(), catPlanAllowance);

                var factRatios = factValues
                .Select(v => (float)Math.Clamp((double)(v / maxRef), 0.12, 0.98))
                .ToArray();

                HubChart.Drawable = new RadarChartDrawable
                {
                    Axes = axes,
                    Plan = [0.82f, 0.85f, 0.70f, 0.65f, 0.75f],
                    Fact = factRatios
                };
                break;
            }
    }

    HubChart.Invalidate();
}

private static string Trim(string text, int max)
{
    if (string.IsNullOrEmpty(text)) return "—";
    return text.Length <= max ? text : text.Substring(0, max).TrimEnd() + "…";
}

}

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
    private void RenderDashboard()
    {
        var wallets = WalletBalances();
        var (income, expense) = PeriodTotals();
        decimal active = ActiveBalance(wallets);

        AnimateBalance(active);

        decimal net = income - expense;
        DeltaBadgeLabel.Text = (net >= 0 ? "▲ +" : "▼ –") + Money(Math.Abs(net));
        DeltaBadgeLabel.TextColor = net >= 0 ? Ui.Success : Ui.Danger;
        DeltaBadge.BackgroundColor = (net >= 0 ? Ui.Success : Ui.Danger).WithAlpha(0.12f);

        IncomeValueLabel.Text = "+" + Money(income);
        ExpenseValueLabel.Text = "–" + Money(expense);

        RenderAccountPills(wallets);
        RenderOverviewChart(wallets);
        RenderLimit(expense);
        RenderTransactions();
    }

    private void AnimateBalance(decimal target)
    {
        string targetText = Money(target);

        if (_privacy || !_loaded || string.IsNullOrEmpty(_lastBalanceText) || _lastBalanceText == targetText)
        {
            HeroBalanceLabel.Text = targetText;
            _lastBalanceText = targetText;
            return;
        }

        decimal from = ParseDisplayed(_lastBalanceText);
        decimal to = target;
        _lastBalanceText = targetText;

        var animation = new Animation(v =>
        {
            decimal shown = CurrencyRates.FromByn((decimal)v, CurrencyIndex);
            HeroBalanceLabel.Text = Grouped(shown) + " " + Symbol;
        }, (double)from, (double)to);

        HeroBalanceLabel.AbortAnimation("balance");
        animation.Commit(HeroBalanceLabel, "balance", 16, 420, Easing.CubicOut);
    }

    private decimal ParseDisplayed(string text)
    {
        string digits = new string(text.Where(c => char.IsDigit(c) || c == '-').ToArray());
        if (!decimal.TryParse(digits, out decimal shown)) return 0m;

        decimal rate = CurrencyRates.RateOf(_currency);
        return rate <= 0 ? shown : shown / rate;
    }

    private void RenderAccountPills((decimal Main, decimal Cash) wallets)
    {
        AccountPillsStack.Children.Clear();
        AccountPillsStack.Children.Add(BuildPill("Все счета", _accountFilter == "all", null, "all"));
        AccountPillsStack.Children.Add(BuildPill("Основной (" + Money(wallets.Main) + ")", _accountFilter == "main", "#EC4899", "main"));
        AccountPillsStack.Children.Add(BuildPill("Наличные (" + Money(wallets.Cash) + ")", _accountFilter == "cash", "#22C55E", "cash"));
    }

    private View BuildPill(string text, bool active, string? dotHex, string key)
    {
        var content = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };

        if (dotHex is not null)
        {
            content.Add(new Border
            {
                WidthRequest = 8,
                HeightRequest = 8,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                BackgroundColor = Color.FromArgb(dotHex),
                VerticalOptions = LayoutOptions.Center
            });
        }

        content.Add(new Label
        {
            Text = text,
            FontFamily = "InterMedium",
            FontSize = 12.5,
            TextColor = active ? _accentColor : SecondaryTextColor,
            VerticalTextAlignment = TextAlignment.Center
        });

        var pill = new Border
        {
            BackgroundColor = active ? _accentColor.WithAlpha(0.15f) : SubtleFillColor,
            Stroke = active ? _accentColor.WithAlpha(0.35f) : HairlineStrokeColor,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(13, 6),
            Content = content,
            Margin = new Thickness(0, 0, 8, 6)
        };

        HapticsClick(pill, () =>
        {
            _accountFilter = key;
            RenderAll();
        });

        return pill;
    }

    private void HapticsClick(View view, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            Haptics.Click();
            action();
        };
        view.GestureRecognizers.Add(tap);
    }

    private void RenderOverviewChart((decimal Main, decimal Cash) wallets)
    {
        var (start, end) = PeriodRange();
        var today = DateTime.Today.AddDays(1);
        var finish = end < today ? end : today;
        var last = finish.AddDays(-1);
        if (last <= start) last = start;

        int points = 7;
        var values = new List<float>(points);
        var labels = new List<string>(points);

        for (int i = 0; i < points; i++)
        {
            double t = i / (double)(points - 1);
            var moment = start.AddDays((last - start).TotalDays * t);
            var balances = WalletBalances(moment.AddDays(1));
            values.Add((float)ActiveBalance(balances));

            if (i == points - 1 && _period != "year")
            {
                labels.Add("Сег");
            }
            else
            {
                labels.Add(_period switch
                {
                    "year" => Capitalize(MonthShort[moment.Month - 1]),
                    "week" => Capitalize(DayShort[((int)moment.DayOfWeek + 6) % 7]),
                    _ => moment.Day.ToString()
                });
            }
        }

        values[^1] = (float)ActiveBalance(wallets);

        OverviewChart.Drawable = new AreaChartDrawable
        {
            Values = values,
            Labels = labels,
            Stroke = _accentColor,
            FillBaseColor = CardSurfaceColor
        };
        OverviewChart.Invalidate();
    }

    private void RenderLimit(decimal expense)
    {
        decimal factor = _period switch { "week" => 0.25m, "year" => 12m, _ => 1m };
        decimal effective = Math.Max(Math.Round(_monthlyLimit * factor), 1m);
        decimal remain = Math.Max(effective - expense, 0m);
        int percent = (int)Math.Round(expense / effective * 100);

        var today = DateTime.Today;
        int daysLeft = _period switch
        {
            "week" => Math.Max(7 - (((int)today.DayOfWeek + 6) % 7), 1),
            "year" => Math.Max((new DateTime(today.Year, 12, 31) - today).Days + 1, 1),
            _ => Math.Max(DateTime.DaysInMonth(today.Year, today.Month) - today.Day + 1, 1)
        };
        int pace = _period switch
        {
            "week" => (int)Math.Round((((int)today.DayOfWeek + 6) % 7 + 1) / 7.0 * 100),
            "year" => (int)Math.Round(today.DayOfYear / (DateTime.IsLeapYear(today.Year) ? 366.0 : 365.0) * 100),
            _ => (int)Math.Round(today.Day / (double)DateTime.DaysInMonth(today.Year, today.Month) * 100)
        };

        LimitTitleLabel.Text = _period switch
        {
            "week" => "Лимит на неделю",
            "year" => "Лимит на год",
            _ => "Лимит на месяц"
        };

        LimitRemainLabel.FormattedText = new FormattedString
        {
            Spans =
{
new Span { Text = "Осталось: ", FontFamily = "Inter", FontSize = 12, TextColor = SecondaryTextColor },
new Span { Text = Money(remain), FontFamily = "InterMedium", FontSize = 12, TextColor = PrimaryTextColor }
}
        };

        AbsoluteLayout.SetLayoutBounds(LimitBarFill, new Rect(0, 0, Math.Clamp(percent / 100.0, 0, 1), 10));
        AbsoluteLayout.SetLayoutFlags(LimitBarFill, AbsoluteLayoutFlags.WidthProportional);

        AbsoluteLayout.SetLayoutBounds(LimitPaceFill, new Rect(0, 0, Math.Clamp(pace / 100.0, 0.01, 0.995), 10));
        AbsoluteLayout.SetLayoutFlags(LimitPaceFill, AbsoluteLayoutFlags.WidthProportional);

        Color barColor = percent >= 100 ? Ui.Danger : percent > 80 ? Ui.Warning : _accentColor;
        LimitBarFill.BackgroundColor = barColor;
        LimitStatusDot.BackgroundColor = barColor;

        AbsoluteLayout.SetLayoutBounds(LimitPaceMarker, new Rect(Math.Clamp(pace / 100.0, 0.02, 0.96), -3, 2, 16));
        AbsoluteLayout.SetLayoutFlags(LimitPaceMarker, AbsoluteLayoutFlags.XProportional);

        LimitSpentLabel.Text = $"Потрачено {Money(expense)} из {Money(effective)} ({percent}%)";
    LimitDailyLabel.Text = "Можно тратить: " + Money(remain / Math.Max(daysLeft, 1)) + "/дн";
    }

    // ================= СПИСОК ОПЕРАЦИЙ И МУЛЬТИ-ВЫДЕЛЕНИЕ =================

}

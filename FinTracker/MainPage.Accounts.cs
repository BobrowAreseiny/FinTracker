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
private void RenderAccounts()
{
    var wallets = WalletBalances();
    var (income, expense) = PeriodTotals();

    AccTotalLabel.Text = Money(wallets.Main + wallets.Cash);
    AccIncomeLabel.Text = "+" + Money(income);
    AccExpenseLabel.Text = "–" + Money(expense);

    WalletsStack.Children.Clear();
    WalletsStack.Add(BuildWalletRow("wallet", AccMain, "Основной счёт", "Безнал и автопуши банков", wallets.Main, "#EC4899"));
    WalletsStack.Add(BuildWalletRow("banknote", AccCash, "Наличные", "В кошельке (ручной учёт)", wallets.Cash, "#22C55E"));

    decimal owedToMe = _debts.Where(d => d.Type == "owed_to_me").Sum(d => d.Amount);
    decimal iOwe = _debts.Where(d => d.Type == "i_owe").Sum(d => d.Amount);
    DebtSummaryLabel.Text = "Мне: " + Money(owedToMe) + " • Я: " + Money(iOwe);

    DebtSeg.Select(_debtFilter == "owed_to_me" ? 0 : 1, raise: false);
    RenderDebts();
}

private View BuildWalletRow(string icon, string accountId, string title, string subtitle, decimal balance, string accentHex)
{
    var color = Color.FromArgb(accentHex);

    var badge = new Border
    {
        WidthRequest = 40,
        HeightRequest = 40,
        StrokeThickness = 1,
        Stroke = color.WithAlpha(0.22f),
        StrokeShape = new RoundRectangle { CornerRadius = 13 },
        BackgroundColor = color.WithAlpha(0.12f),
        Content = new LucideIcon { Icon = icon, Size = 20, Tint = color }
    };

    var texts = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
    texts.Add(new Label { Text = title, FontFamily = "InterMedium", FontSize = 14.5, TextColor = PrimaryTextColor });
    texts.Add(new Label { Text = subtitle, FontFamily = "Inter", FontSize = 12, TextColor = SecondaryTextColor });

    var left = new HorizontalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.Center };
    left.Add(badge);
    left.Add(texts);

    var amount = new Label
    {
        Text = Money(balance),
        FontFamily = "InterSemiBold",
        FontSize = 15,
        TextColor = balance > 0 ? PrimaryTextColor : SecondaryTextColor,
        VerticalTextAlignment = TextAlignment.Center
    };

    var row = new Grid
    {
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
        BackgroundColor = Colors.Transparent
    };
    row.Add(left, 0, 0);
    row.Add(amount, 1, 0);

    var card = new Border
    {
        StrokeThickness = 1,
        Stroke = HairlineStrokeColor,
        StrokeShape = new RoundRectangle { CornerRadius = 18 },
        Padding = 14,
        BackgroundColor = SubtleFillColor,
        Content = row
    };

    string wallet = accountId == AccCash ? WCash : WMain;
    HapticsClick(card, () =>
    {
        _accountFilter = wallet;
        SetActiveTab("main");
        RenderAll();
        ShowToast("Фильтр по выбранному счёту", "wallet");
    });

    return card;
}

private void RenderDebts()
{
    DebtListContainer.Children.Clear();

    var items = _debts.Where(d => d.Type == _debtFilter).ToList();

    if (items.Count == 0)
    {
        DebtListContainer.Add(new Label
        {
            Text = "Активных записей в этой вкладке нет",
            FontFamily = "Inter",
            FontSize = 12.5,
            TextColor = SecondaryTextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 10, 0, 6)
        });
        return;
    }

    foreach (var debt in items)
    {
        bool toMe = debt.Type == "owed_to_me";

        var texts = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
        texts.Add(new Label
        {
            Text = debt.Person,
            FontFamily = "InterMedium",
            FontSize = 13.5,
            TextColor = PrimaryTextColor,
            LineBreakMode = LineBreakMode.TailTruncation
        });

        var sub = new Label { FontFamily = "Inter", FontSize = 11.5, TextColor = SecondaryTextColor };
        sub.FormattedText = new FormattedString
        {
            Spans =
{
new Span { Text = debt.Due + " • ", FontSize = 11.5, FontFamily = "Inter", TextColor = SecondaryTextColor },
new Span { Text = debt.Acc == WCash ? "Наличные" : "Основной", FontSize = 11.5, FontFamily = "Inter", TextColor = PrimaryTextColor.WithAlpha(0.6f) }
}
        };
        texts.Add(sub);

        var amount = new Label
        {
            Text = Money(debt.Amount),
            FontFamily = "InterSemiBold",
            FontSize = 14,
            TextColor = toMe ? Ui.Success : Ui.Danger,
            VerticalTextAlignment = TextAlignment.Center
        };

        // Кнопка «Погасить» в стиле HTML (bg-accent/15 text-accent border-accent/25)
        var settle = new Border
        {
            BackgroundColor = _accentColor.WithAlpha(0.15f),
            Stroke = _accentColor.WithAlpha(0.28f),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(10, 6),
            Content = new Label
            {
                Text = "Погасить",
                FontFamily = "InterSemiBold",
                FontSize = 11.5,
                TextColor = _accentColor
            }
        };

        long id = debt.Id;
        HapticsClick(settle, () => SettleDebt(id));

        var right = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center };
        right.Add(amount);
        right.Add(settle);

        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };
        grid.Add(texts, 0, 0);
        grid.Add(right, 1, 0);

        DebtListContainer.Add(new Border
        {
            BackgroundColor = SubtleFillColor,
            Stroke = HairlineStrokeColor,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = 12,
            Content = grid
        });
    }
}

private void SettleDebt(long id)
{
    var debt = _debts.FirstOrDefault(d => d.Id == id);
    if (debt is null) return;

    _debts.Remove(debt);
    SaveDebts();

    bool isIncome = debt.Type == "owed_to_me";

    _suppressReload = true;
    _databaseService.AddTransaction(new Transaction
    {
        Amount = debt.Amount,
        Currency = "BYN",
        Category = isIncome ? "Возврат долга" : "Погашение долга",
        ShopName = debt.Person,
        Type = isIncome ? "income" : "expense",
        AccountId = debt.Acc == WCash ? AccCash : AccMain,
        Date = DateTime.Now,
        Source = "manual"
    });
    _suppressReload = false;

    _ = LoadAsync();

    string delta = isIncome
    ? "+" + Money(debt.Amount) + " возвращено"
    : "–" + Money(debt.Amount) + " списано";
    ShowToast("Долг погашен: " + delta, "check-circle-2");
}

private void SetDebtFilter(string filter)
{
    _debtFilter = filter;
    DebtSeg.Select(filter == "owed_to_me" ? 0 : 1, raise: false);
    RenderDebts();
}

// ================= НАСТРОЙКИ =================

}

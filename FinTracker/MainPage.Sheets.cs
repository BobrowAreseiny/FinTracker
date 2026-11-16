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
private void OnOpenTransferSheetTapped(object? sender, TappedEventArgs e)
{
    TransferAmountEntry.Text = string.Empty;
    _transferDirection = "main_to_cash";
    TransferDirectionSeg.Select(0, raise: false);
    _ = OpenSheetAsync(TransferOverlay, TransferSheet);
}

private void OnCloseTransferSheetTapped(object? sender, TappedEventArgs e) => CloseTransfer();

private void CloseTransfer()
{
    if (TransferOverlay.IsVisible) _ = CloseSheetAsync(TransferOverlay, TransferSheet);
}

private void OnSubmitTransferTapped(object? sender, TappedEventArgs e)
{
    decimal entered = ParseDecimal(TransferAmountEntry.Text);
    if (entered <= 0)
    {
        ShowToast("Введите сумму перевода", "alert-circle");
        Haptics.Warning();
        return;
    }

    decimal amountByn = Math.Round(InputToByn(entered), 2);
    bool toCash = _transferDirection == "main_to_cash";

    _suppressReload = true;
    _databaseService.AddTransaction(new Transaction
    {
        Amount = amountByn,
        Currency = "BYN",
        Category = "Перевод",
        ShopName = toCash ? "Основной → Наличные" : "Наличные → Основной",
        Type = "transfer",
        AccountId = toCash ? AccCash : AccMain,
        Date = DateTime.Now,
        Source = "manual"
    });
    _suppressReload = false;

    CloseTransfer();
    _ = LoadAsync();

    ShowToast(toCash
    ? "Переведено в наличные: " + Money(amountByn)
    : "Переведено на основной счёт: " + Money(amountByn), "arrow-right-left");
}

// ================= ЛИМИТ =================

private void OnOpenLimitSheetTapped(object? sender, TappedEventArgs e)
{
    decimal shownLimit = Math.Round(BynToDisplay(_monthlyLimit));
    LimitEntry.Text = shownLimit.ToString("0", CultureInfo.InvariantCulture);
    _ = OpenSheetAsync(LimitOverlay, LimitSheet);
}

private void OnCloseLimitSheetTapped(object? sender, TappedEventArgs e) => CloseLimit();

private void CloseLimit()
{
    if (LimitOverlay.IsVisible) _ = CloseSheetAsync(LimitOverlay, LimitSheet);
}

private void OnLimitPreset1500Tapped(object? sender, TappedEventArgs e) => LimitEntry.Text = "1500";
private void OnLimitPreset2000Tapped(object? sender, TappedEventArgs e) => LimitEntry.Text = "2000";
private void OnLimitPreset2500Tapped(object? sender, TappedEventArgs e) => LimitEntry.Text = "2500";
private void OnLimitPreset3000Tapped(object? sender, TappedEventArgs e) => LimitEntry.Text = "3000";

private void OnSaveLimitTapped(object? sender, TappedEventArgs e)
{
    decimal entered = ParseDecimal(LimitEntry.Text);
    if (entered <= 0)
    {
        ShowToast("Введите корректный лимит", "alert-circle");
        return;
    }

    _monthlyLimit = Math.Max(Math.Round(InputToByn(entered)), 1m);
    Preferences.Default.Set("FT_MonthlyLimit", (double)_monthlyLimit);

    CloseLimit();
    RenderAll();
    ShowToast("Лимит установлен: " + Money(_monthlyLimit), "target");
}

// ================= ДОЛГИ =================

private void OnOpenDebtSheetTapped(object? sender, TappedEventArgs e)
{
    DebtPersonEntry.Text = string.Empty;
    DebtAmountEntry.Text = string.Empty;
    DebtDueEntry.Text = string.Empty;

    _modalDebtType = _debtFilter;
    _modalDebtAccount = _accountFilter == WCash ? WCash : WMain;

    DebtTypeSeg.Select(_modalDebtType == "owed_to_me" ? 0 : 1, raise: false);
    DebtAccountSeg.Select(_modalDebtAccount == WCash ? 1 : 0, raise: false);
    UpdateDebtHint();

    _ = OpenSheetAsync(DebtOverlay, DebtSheet);
}

private void OnCloseDebtSheetTapped(object? sender, TappedEventArgs e) => CloseDebt();

private void CloseDebt()
{
    if (DebtOverlay.IsVisible) _ = CloseSheetAsync(DebtOverlay, DebtSheet);
}

private void SetModalDebtType(string type)
{
    _modalDebtType = type;
    DebtTypeSeg.Select(type == "owed_to_me" ? 0 : 1, raise: false);
    UpdateDebtHint();
}

private void SetModalDebtAccount(string account)
{
    _modalDebtAccount = account;
    DebtAccountSeg.Select(account == WCash ? 1 : 0, raise: false);
}

private void UpdateDebtHint()
{
    DebtHintLabel.Text = _modalDebtType == "owed_to_me"
    ? "Вы даёте в долг: сумма сразу спишется со счёта"
    : "Вы берёте в долг: сумма сразу пополнит счёт";
}

private void OnSubmitDebtTapped(object? sender, TappedEventArgs e)
{
    string person = (DebtPersonEntry.Text ?? string.Empty).Trim();
    decimal entered = ParseDecimal(DebtAmountEntry.Text);
    string due = (DebtDueEntry.Text ?? string.Empty).Trim();
    if (due.Length == 0) due = "без срока";

    if (person.Length == 0 || entered <= 0)
    {
        ShowToast("Укажите имя и сумму долга", "alert-circle");
        Haptics.Warning();
        return;
    }

    decimal amountByn = Math.Round(InputToByn(entered), 2);
    bool owedToMe = _modalDebtType == "owed_to_me";

    _debts.Insert(0, new DebtDef
    {
        Id = DateTime.Now.Ticks,
        Person = person,
        Amount = amountByn,
        Due = due,
        Type = _modalDebtType,
        Acc = _modalDebtAccount
    });
    SaveDebts();

    _suppressReload = true;
    _databaseService.AddTransaction(new Transaction
    {
        Amount = amountByn,
        Currency = "BYN",
        Category = owedToMe ? "Дал в долг" : "Взял в долг",
        ShopName = person + " (" + due + ")",
        Type = owedToMe ? "expense" : "income",
        AccountId = _modalDebtAccount == WCash ? AccCash : AccMain,
        Date = DateTime.Now,
        Source = "manual"
    });
    _suppressReload = false;

    _debtFilter = _modalDebtType;
    DebtSeg.Select(_debtFilter == "owed_to_me" ? 0 : 1, raise: false);

    CloseDebt();
    _ = LoadAsync();

    string accLabel = _modalDebtAccount == WCash ? "Наличные" : "Основной";
    ShowToast(owedToMe
    ? "Дал в долг: –" + Money(amountByn) + " (" + accLabel + ")"
    : "Взял в долг: +" + Money(amountByn) + " (" + accLabel + ")",
    owedToMe ? "arrow-up-right" : "arrow-down-left");
}

// ================= ФИЛЬТРЫ ОПЕРАЦИЙ =================

}

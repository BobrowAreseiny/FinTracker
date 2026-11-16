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
private void OnToggleSearchTapped(object? sender, TappedEventArgs e)
{
    SearchWrap.IsVisible = !SearchWrap.IsVisible;

    bool open = SearchWrap.IsVisible;
    TxTitleBlock.IsVisible = !open;
    SearchToggleButton.Stroke = open ? ActiveControlStroke : HairlineStrokeColor;
    SearchToggleIcon.Tint = open ? _accentColor : SecondaryTextColor;

    if (open)
    {
        TxSearchEntry.Focus();
    }
    else
    {
        // Закрываем поиск — убираем фокус и прячем клавиатуру.
        TxSearchEntry.Unfocus();
    }
}

private void OnClearSearchTapped(object? sender, TappedEventArgs e)
{
    TxSearchEntry.Text = string.Empty;
    RenderTransactions();
}

private void OnResetFiltersTapped(object? sender, TappedEventArgs e)
{
    _txTypeFilter = "all";
    _selectionMode = false;
    _selectedIds.Clear();
    TxTypeSeg.Select(0, raise: false);
    TxSearchEntry.Text = string.Empty;
    TxSearchEntry.Unfocus();
    SearchWrap.IsVisible = false;
    TxTitleBlock.IsVisible = true;
    SearchToggleButton.Stroke = HairlineStrokeColor;
    SearchToggleIcon.Tint = SecondaryTextColor;
    RenderTransactions();
    ShowToast("Фильтры сброшены", "rotate-ccw");
}

private void OnTxSearchTextChanged(object? sender, TextChangedEventArgs e)
{
    _searchDebounce?.Cancel();
    var cts = new CancellationTokenSource();
    _searchDebounce = cts;

    _ = Task.Delay(180, cts.Token).ContinueWith(t =>
    {
        if (t.IsCanceled) return;
        MainThread.BeginInvokeOnMainThread(RenderTransactions);
    });
}

// ================= CSV / ДЕМО / ОЧИСТКА =================

private string BuildCsv()
{
    var sb = new StringBuilder();
    sb.AppendLine("ID;Период;Категория;Комментарий;Тип;Счёт;Сумма (BYN)");

    foreach (var t in _transactions)
    {
        string acc = (string.IsNullOrEmpty(t.AccountId) ? AccMain : t.AccountId) == AccCash ? "cash" : "main";
        sb.AppendLine(string.Join(';', t.Id, GroupName(t.Date), t.Category, t.ShopName, t.Type, acc, t.Amount.ToString("0.##", CultureInfo.InvariantCulture)));
    }

    return sb.ToString();
}

private void OnOpenCsvSheetTapped(object? sender, TappedEventArgs e)
{
    CsvPreviewLabel.Text = BuildCsv();
    _ = OpenSheetAsync(CsvOverlay, CsvSheet);
}

private void OnCloseCsvSheetTapped(object? sender, TappedEventArgs e) => CloseCsv();

private void CloseCsv()
{
    if (CsvOverlay.IsVisible) _ = CloseSheetAsync(CsvOverlay, CsvSheet);
}

private async void OnCopyCsvTapped(object? sender, TappedEventArgs e)
{
    try
    {
        await Clipboard.Default.SetTextAsync(CsvPreviewLabel.Text ?? string.Empty);
        ShowToast("CSV скопирован в буфер обмена", "copy");
    }
    catch
    {
        ShowToast("Не удалось скопировать", "alert-circle");
    }

    CloseCsv();
}

// ================= ПОДТВЕРЖДЕНИЕ В СТИЛЕ iOS =================

private TaskCompletionSource<bool>? _confirmTcs;

private async Task<bool> ShowConfirmAsync(string title, string message, string confirmText)
{
    ConfirmTitleLabel.Text = title;
    ConfirmMessageLabel.Text = message;
    ConfirmYesLabel.Text = confirmText;

    _confirmTcs = new TaskCompletionSource<bool>();

    await OpenSheetAsync(ConfirmOverlay, ConfirmSheet);
    bool result = await _confirmTcs.Task;
    await CloseSheetAsync(ConfirmOverlay, ConfirmSheet);

    return result;
}

private void OnConfirmCancelTapped(object? sender, TappedEventArgs e)
{
    _confirmTcs?.TrySetResult(false);
}

private void OnConfirmYesTapped(object? sender, TappedEventArgs e)
{
    _confirmTcs?.TrySetResult(true);
}

private async void OnSeedDemoDataTapped(object? sender, TappedEventArgs e)
{
    bool confirmed = await ShowConfirmAsync(
    "Демо-данные",
    "Восстановить эталонные демонстрационные операции, категории и долги?",
    "Восстановить");
    if (!confirmed) return;

    CategoryCatalog.ResetToDefaults();
    (_expenseCategories, _incomeCategories) = CategoryCatalog.Load();

    _suppressReload = true;
    _databaseService.DeleteAllTransactions();
    _suppressReload = false;

    SeedDemoData();
    ShowToast("Демо-данные восстановлены", "sparkles");
}

/// <summary>
/// Заполняет базу эталонным набором операций и долгов (1 в 1 с INITIAL_TRANSACTIONS и INITIAL_DEBTS из HTML-макета).
/// </summary>
private void SeedDemoData()
{
    _suppressReload = true;

    var demo = new (string Category, string Note, decimal Amount, string Type, int DaysAgo, string Acc)[]
    {
("Кафе и рестораны", "Утренний фильтр и круассан", 42m, "expense", 0, AccMain),
("Продукты", "Фермерский рынок", 185m, "expense", 0, AccMain),
("Фриланс", "Оплата за дизайн-прототип", 320m, "income", 1, AccMain),
("Транспорт", "Такси и заправка АИ-95", 94m, "expense", 1, AccMain),
("Жилье и связь", "Домашний интернет и ЖКХ", 420m, "expense", 1, AccMain),
("Продукты", "Супермаркет Green на неделю", 410m, "expense", 5, AccMain),
("Зарплата", "Основное начисление на карту", 3200m, "income", 7, AccMain)
    };

    int step = 0;
    foreach (var item in demo.Reverse())
    {
        _databaseService.AddTransaction(new Transaction
        {
            Amount = item.Amount,
            Currency = "BYN",
            Category = item.Category,
            ShopName = item.Note,
            Type = item.Type,
            AccountId = item.Acc,
            Date = DateTime.Today.AddDays(-item.DaysAgo).AddHours(9).AddMinutes(step++),
            Source = "demo"
        });
    }

    _suppressReload = false;

    _debts =
    [
    new DebtDef { Id = DateTime.Now.Ticks, Person = "Алексей (коллега)", Amount = 150m, Due = "до 20 нояб", Type = "owed_to_me", Acc = WMain },
new DebtDef { Id = DateTime.Now.Ticks + 1, Person = "Марина (обед)", Amount = 45m, Due = "до 14 нояб", Type = "i_owe", Acc = WMain }
     ];
    SaveDebts();

    _selectionMode = false;
    _selectedIds.Clear();
    _ = LoadAsync();
}

private async void OnClearDataTapped(object? sender, TappedEventArgs e)
{
    bool confirmed = await ShowConfirmAsync(
    "Очистить базу операций?",
    "Все операции и записи о долгах будут удалены. Отменить это действие нельзя.",
    "Очистить");
    if (!confirmed) return;

    _suppressReload = true;
    _databaseService.DeleteAllTransactions();
    _suppressReload = false;

    _debts.Clear();
    SaveDebts();

    _selectionMode = false;
    _selectedIds.Clear();
    _selectedCatIndex = null;
    _ = LoadAsync();
    ShowToast("База операций очищена", "trash-2");
}
}

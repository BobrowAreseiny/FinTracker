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
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_fatalShown) return;

        _isPageVisible = true;
        _dataDirty = false;
        if (Window is not null)
        {
            Window.Stopped -= OnWindowStopped;
            Window.Resumed -= OnWindowResumed;
            Window.Stopped += OnWindowStopped;
            Window.Resumed += OnWindowResumed;
        }

        AttachGestureHost();
        ApplySystemBars();
        UpdateRatesLabel();
        UpdateAutoTrackUi();
        await LoadAsync();

#if DEBUG
        if (Environment.GetEnvironmentVariable("FT_SEED_DEMO") == "1" && _transactions.Count == 0)
        {
            SeedDemoData();
            await LoadAsync();
        }
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isPageVisible = false;

        if (Window is not null)
        {
            Window.Stopped -= OnWindowStopped;
            Window.Resumed -= OnWindowResumed;
        }

        DetachGestureHost();
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        _isPageVisible = false;
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        _isPageVisible = true;
        if (_dataDirty && !_fatalShown)
        {
            _dataDirty = false;
            _ = LoadAsync();
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0 || height <= 0) return;
        if (Math.Abs(_lastAllocatedHeight - height) < 1 && Math.Abs(_lastAllocatedWidth - width) < 1) return;

        _lastAllocatedWidth = width;
        _lastAllocatedHeight = height;

        RootGrid?.InvalidateMeasure();
        AppHost?.InvalidateMeasure();
    }

    private double _lastAllocatedWidth;
    private double _lastAllocatedHeight;

    private async Task LoadAsync()
    {
        try
        {
            var list = await Task.Run(() => _databaseService.GetTransactions());
            _transactions = list
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToList();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "MainPage.LoadAsync");
        }

        _loaded = true;
        RenderAll();
    }

    private void RenderAll()
    {
        if (_fatalShown) return;

        try
        {
            UpdatePlaceholdersForCurrency();
            RenderDashboard();
            RenderAnalytics();
            RenderAccounts();
            RenderSettings();
            RenderCategoryChips();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "MainPage.RenderAll");
        }
    }

    private void UpdatePlaceholdersForCurrency()
    {
        string sym = Symbol;
        if (AddAmountEntry is not null) AddAmountEntry.Placeholder = $"Сумма в {sym} (например, 45)";
        if (TransferAmountEntry is not null) TransferAmountEntry.Placeholder = $"Сумма перевода в {sym} (например, 200)";
        if (LimitEntry is not null) LimitEntry.Placeholder = $"Лимит в {sym}";
        if (DebtAmountEntry is not null) DebtAmountEntry.Placeholder = $"Сумма в {sym} (например, 150)";
    }

    // ================= АППАРАТНАЯ КНОПКА «НАЗАД» =================

    private bool _entryFocused;
    private Entry? _focusedEntry;

    private void AttachEntryFocusTracking()
    {
        Entry?[] entries =
        [
        AddAmountEntry, AddNoteEntry, TxSearchEntry, CatNameEntry,
TransferAmountEntry, LimitEntry, DebtPersonEntry, DebtAmountEntry, DebtDueEntry
         ];

        foreach (var entry in entries)
        {
            if (entry is null) continue;

            entry.Focused += (_, _) =>
            {
                _entryFocused = true;
                _focusedEntry = entry;
            };

            entry.Unfocused += (_, _) =>
            {
                _entryFocused = false;
                _focusedEntry = null;
            };
        }
    }

    public bool HandleBackRequest()
    {
        if (_entryFocused && _focusedEntry is not null)
        {
            _focusedEntry.Unfocus();
            return true;
        }

        if (TryCloseAnySheet()) return true;

        if (TryNavigateBack()) return true;

        MoveToBackground();
        return true;
    }

    /// <summary>
    /// Шаг назад внутри приложения: снять режим выделения или вернуть на предыдущую вкладку.
    /// false — возвращаться уже некуда (мы на главной без истории).
    /// slideFrom != 0 — вкладка въезжает сбоку (когда назад «листают» свайпом).
    /// </summary>
    private bool TryNavigateBack(double slideFrom = 0)
    {
        if (_selectionMode)
        {
            _selectionMode = false;
            _selectedIds.Clear();
            RenderTransactions();
            return true;
        }

        if (_tabHistory.Count > 0)
        {
            string previous = _tabHistory[^1];
            _tabHistory.RemoveAt(_tabHistory.Count - 1);
            SetActiveTab(previous, animate: true, record: false, slideFrom: slideFrom);
            return true;
        }

        if (_activeTab != "main")
        {
            SetActiveTab("main", animate: true, record: false, slideFrom: slideFrom);
            return true;
        }

        return false;
    }

    protected override bool OnBackButtonPressed()
    {
        return HandleBackRequest();
    }

    private void MoveToBackground()
    {
        try
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            activity?.MoveTaskToBack(true);
#endif
        }
        catch { }
    }

    private bool TryCloseAnySheet()
    {
        if (AddTxOverlay.IsVisible) { CloseAddTx(); return true; }
        if (CatMgrOverlay.IsVisible) { CloseCategoryManager(); return true; }
        if (TransferOverlay.IsVisible) { CloseTransfer(); return true; }
        if (LimitOverlay.IsVisible) { CloseLimit(); return true; }
        if (DebtOverlay.IsVisible) { CloseDebt(); return true; }
        if (CsvOverlay.IsVisible) { CloseCsv(); return true; }
        if (ConfirmOverlay.IsVisible) { _confirmTcs?.TrySetResult(false); return true; }
        return false;
    }

    private void ApplySystemBars()
    {
        try
        {
#if ANDROID
            var window = Platform.CurrentActivity?.Window;
            if (window is null) return;

            string statusHex = _isLightTheme ? "#F7F5F0" : "#08080A";
            string navHex = _isLightTheme ? "#FFFFFF" : "#101012";

            window.SetStatusBarColor(Android.Graphics.Color.ParseColor(statusHex));
            window.SetNavigationBarColor(Android.Graphics.Color.ParseColor(navHex));
            window.DecorView.SystemUiVisibility = _isLightTheme
            ? (Android.Views.StatusBarVisibility)Android.Views.SystemUiFlags.LightStatusBar
            : 0;

            window.DecorView.SetBackgroundColor(Android.Graphics.Color.ParseColor(statusHex));
#endif
        }
        catch { }
    }

    // ================= ДЕНЬГИ И ПЕРИОД =================

    private int CurrencyIndex => CurrencyRates.IndexOf(_currency);

    private int CurrencySegIndex => _currency switch { "USD" => 1, "EUR" => 2, _ => 0 };

    private string Symbol => CurrencyRates.All[CurrencyIndex].Symbol;

    private static string Grouped(decimal value)
    {
        var ru = CultureInfo.GetCultureInfo("ru-RU");
        string s = Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", ru);
        return s.Replace('\u00A0', ' ').Replace(',', ' ');
    }

    private string Money(decimal byn, bool forceVisible = false)
    {
        if (_privacy && !forceVisible) return "••• " + Symbol;
        decimal shown = CurrencyRates.FromByn(byn, CurrencyIndex);
        return Grouped(shown) + " " + Symbol;
    }

    /// <summary>
    /// Переводит введённую пользователем сумму из текущей выбранной валюты интерфейса в базовую (BYN).
    /// </summary>
    private decimal InputToByn(decimal enteredAmount)
    {
        if (_currency == "BYN") return enteredAmount;
        decimal rate = CurrencyRates.RateOf(_currency);
        return rate <= 0 ? enteredAmount : enteredAmount / rate;
    }

    private decimal BynToDisplay(decimal bynAmount)
    {
        return CurrencyRates.FromByn(bynAmount, CurrencyIndex);
    }

    private (DateTime Start, DateTime End) PeriodRange()
    {
        var today = DateTime.Today;
        return _period switch
        {
            // Календарная неделя: понедельник–воскресенье, чтобы лимит и графики
            // совпадали с недельным периодом и сбрасывались в понедельник.
            "week" => WeekRange(today),
            "year" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year + 1, 1, 1)),
            _ => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1))
        };
    }

    private static (DateTime Start, DateTime End) WeekRange(DateTime today)
    {
        int offset = ((int)today.DayOfWeek + 6) % 7;
        var start = today.Date.AddDays(-offset);
        return (start, start.AddDays(7));
    }

    private string PeriodRangeLabel()
    {
        var (start, end) = PeriodRange();
        var last = end.AddDays(-1);
        return _period switch
        {
            "week" => $"{start:dd.MM} — {last:dd.MM}",
            "year" => $"Янв — {Capitalize(MonthShort[last.Month - 1])} {last.Year}",
            _ => $"{start:dd.MM} — {last:dd.MM}"
        };
    }

    private static bool IsTransfer(Transaction t) =>
    t.Type == "transfer" || AccountBalance.IsTransferType(t.Type ?? string.Empty);

    private bool IsIncome(Transaction t) => !IsTransfer(t) && t.Type is "income" or "loan_borrowed";

    private bool IsExpense(Transaction t) => !IsTransfer(t) && (string.IsNullOrEmpty(t.Type) || t.Type is "expense" or "loan_lent");

    private static bool IsDebtLoan(Transaction t) =>
    t.Type is "loan_lent" or "loan_borrowed"
    || t.Category is "Дал в долг" or "Взял в долг" or "Возврат долга" or "Погашение долга";

    private bool MatchesAccount(Transaction t, string wallet)
    {
        if (wallet == "all") return true;
        if (t.Type == "transfer") return true;

        string acc = string.IsNullOrEmpty(t.AccountId) ? AccMain : t.AccountId;
        bool isCash = acc == AccCash;
        return wallet switch
        {
            WCash => isCash,
            WMain => !isCash,
            _ => true
        };
    }

    private (decimal Main, decimal Cash) WalletBalances(DateTime? upTo = null)
    {
        decimal main = 0m, cash = 0m;

        foreach (var t in _transactions)
        {
            if (upTo.HasValue && t.Date >= upTo.Value) continue;

            string type = t.Type ?? "expense";

            if (type == "transfer")
            {
                if (t.AccountId == AccCash) { cash += t.Amount; main -= t.Amount; }
                else { main += t.Amount; cash -= t.Amount; }
                continue;
            }

            if (AccountBalance.IsTransferType(type)) continue;

            bool plus = IsIncome(t);
            bool minus = IsExpense(t);
            if (!plus && !minus) continue;

            decimal delta = plus ? t.Amount : -t.Amount;
            if (MatchesAccount(t, WCash)) cash += delta;
            else main += delta;
        }

        return (main, cash);
    }

    private decimal ActiveBalance((decimal Main, decimal Cash) wallets)
    {
        return _accountFilter switch
        {
            "main" => wallets.Main,
            "cash" => wallets.Cash,
            _ => wallets.Main + wallets.Cash
        };
    }

    private (decimal Income, decimal Expense) PeriodTotals()
    {
        var (start, end) = PeriodRange();
        decimal income = 0m, expense = 0m;

        foreach (var t in _transactions)
        {
            if (t.Date < start || t.Date >= end) continue;
            if (_accountFilter != "all" && !MatchesAccount(t, _accountFilter)) continue;
            if (IsTransfer(t) || IsDebtLoan(t)) continue;

            if (IsIncome(t)) income += t.Amount;
            else if (IsExpense(t)) expense += t.Amount;
        }

        return (income, expense);
    }

    // ================= ОБЩИЕ РЕНДЕРЫ ГЛАВНОЙ =================

}

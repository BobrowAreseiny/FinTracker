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
private void SetPeriod(int index)
{
    _period = index switch { 0 => "week", 2 => "year", _ => "month" };
    _hubDonutIndex = null;
    _hubTreemapIndex = null;
    _hubWaffleIndex = null;
    Preferences.Default.Set("FT_Period", _period);

    PeriodSegMain.Select(index, raise: false);
    PeriodSegAnalytics.Select(index, raise: false);
    PeriodSegAccounts.Select(index, raise: false);

    RenderAll();
}

private int PeriodIndex() => _period switch { "week" => 0, "year" => 2, _ => 1 };

private void SetAnalyticsMode(string mode)
{
    _analyticsMode = mode;
    bool categories = mode == "categories";

    AnalyticsCategoriesSection.IsVisible = categories;
    AnalyticsDynamicsSection.IsVisible = !categories;
    AnalyticsModeSeg.Select(categories ? 0 : 1, raise: false);

    RenderAnalytics();
}

private void SetAnalyticsFlow(string flow)
{
    _analyticsFlow = flow;
    _selectedCatIndex = null;
    _hubDonutIndex = null;
    _hubTreemapIndex = null;
    _hubWaffleIndex = null;
    AnalyticsFlowSeg.Select(flow == "expense" ? 0 : 1, raise: false);
    RenderAnalytics();
}

private void OnNavMainTapped(object? sender, TappedEventArgs e) => SetActiveTab("main");
private void OnNavAnalyticsTapped(object? sender, TappedEventArgs e) => SetActiveTab("analytics");
private void OnNavAccountsTapped(object? sender, TappedEventArgs e) => SetActiveTab("accounts");
private void OnNavSettingsTapped(object? sender, TappedEventArgs e) => SetActiveTab("settings");

private void SetActiveTab(string tab, bool animate = true, bool record = true, double slideFrom = 0)
{
    if (record && _activeTab != tab)
    {
        _tabHistory.Add(_activeTab);
        if (_tabHistory.Count > 12) _tabHistory.RemoveAt(0);
    }

    _activeTab = tab;
    Preferences.Default.Set("FT_ActiveTab", tab);
    Haptics.Click();

    TabMain.IsVisible = tab == "main";
    TabAnalytics.IsVisible = tab == "analytics";
    TabAccounts.IsVisible = tab == "accounts";
    TabSettings.IsVisible = tab == "settings";

    UpdateNavBarColors();

    if (animate)
    {
        var content = tab switch
        {
            "analytics" => TabAnalytics,
            "accounts" => TabAccounts,
            "settings" => TabSettings,
            _ => TabMain
        };

        // slideFrom != 0 — вкладка «въезжает» сбоку: так свайп вбок ощущается как листание.
        bool slide = Math.Abs(slideFrom) > 0.1;

        content.Opacity = slide ? 0.55 : 0;
        content.TranslationX = slide ? slideFrom : 0;
        content.TranslationY = slide ? 0 : 10;
        content.Scale = slide ? 1 : 0.985;

        _ = Task.WhenAll(
        content.FadeTo(1, slide ? 200u : 260u, Easing.CubicOut),
        content.TranslateTo(0, 0, slide ? 300u : 340u, Easing.CubicOut),
        content.ScaleTo(1, slide ? 300u : 340u, Easing.CubicOut));
    }

    _ = MainScroll.ScrollToAsync(0, 0, false);
}

private void UpdateNavItem(VisualElement item, LucideIcon icon, Label label, bool active)
{
    Color color = active ? _accentColor : SecondaryTextColor;
    icon.Tint = color;
    label.TextColor = color;
    item.Scale = active ? 1.04 : 1;
}

// ================= TOAST (Dynamic Island) =================

private async void ShowToast(string text, string icon = "sparkles")
{
    try
    {
        ToastIcon.Icon = icon;
        ToastIcon.Tint = _accentColor;
        ToastLabel.Text = text;

        _toastCts?.Cancel();
        var cts = new CancellationTokenSource();
        _toastCts = cts;

        ToastBorder.IsVisible = true;
        ToastBorder.Opacity = 0;
        ToastBorder.TranslationY = -70;
        ToastBorder.Scale = 0.68;

        await Task.WhenAll(
        ToastBorder.FadeTo(1, 160, Easing.CubicOut),
        ToastBorder.TranslateTo(0, 0, 460, Easing.SpringOut),
        ToastBorder.ScaleTo(1, 460, Easing.SpringOut));

        try
        {
            await Task.Delay(2350, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await Task.WhenAll(
        ToastBorder.FadeTo(0, 200, Easing.CubicIn),
        ToastBorder.TranslateTo(0, -60, 260, Easing.CubicIn));

        if (!cts.IsCancellationRequested) ToastBorder.IsVisible = false;
    }
    catch (Exception ex)
    {
        CrashLog.Write(ex, "MainPage.ShowToast");
    }
}

// ================= ШТОРКИ: АНИМАЦИЯ =================

/// <summary>Поглощает тап внутри шторки, чтобы он не закрывал её через оверлей.</summary>
private void OnSheetTapIgnore(object? sender, TappedEventArgs e)
{
}

private async Task OpenSheetAsync(Grid overlay, Border sheet)
{
    overlay.IsVisible = true;
    overlay.Opacity = 0;
    sheet.TranslationY = Math.Max(sheet.Height, 520);

    await Task.Yield();

    await Task.WhenAll(
    overlay.FadeTo(1, 200, Easing.CubicOut),
    sheet.TranslateTo(0, 0, 380, Easing.CubicOut),
    AppHost.ScaleTo(0.945, 380, Easing.CubicOut),
    AppHost.TranslateTo(0, 8, 380, Easing.CubicOut));
}

private async Task CloseSheetAsync(Grid overlay, Border sheet)
{
    await Task.WhenAll(
    overlay.FadeTo(0, 180, Easing.CubicIn),
    sheet.TranslateTo(0, Math.Max(sheet.Height, 520), 300, Easing.CubicIn),
    AppHost.ScaleTo(1, 300, Easing.CubicOut),
    AppHost.TranslateTo(0, 0, 300, Easing.CubicOut));

    overlay.IsVisible = false;
    overlay.Opacity = 1;
}

// ================= СЛУЖЕБНЫЕ =================

private void UpdateRatesLabel()
{
    if (FxRateLabel is null) return;

    decimal usd = CurrencyRates.RateOf("USD");
    decimal eur = CurrencyRates.RateOf("EUR");

    string usdPerByn = usd <= 0 ? "3,25" : (1m / usd).ToString("0.00", _ru);
    string eurPerByn = eur <= 0 ? "3,33" : (1m / eur).ToString("0.00", _ru);

    var updated = CurrencyRates.LastUpdated;
    string stamp = updated.HasValue ? $"Обновлено в {updated.Value:HH:mm}" : "Обновлено сегодня";
    FxRateLabel.Text = $"1 $ = {usdPerByn} Br • 1 € = {eurPerByn} Br ({stamp})";

}

private List<DebtDef> LoadDebts()
{
    try
    {
        string json = Preferences.Default.Get("FT_Debts_v1", string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<DebtDef>>(json) ?? [];
    }
    catch (Exception ex)
    {
        CrashLog.Write(ex, "MainPage.LoadDebts");
        return [];
    }
}

private void SaveDebts()
{
    try
    {
        Preferences.Default.Set("FT_Debts_v1", JsonSerializer.Serialize(_debts));
    }
    catch (Exception ex)
    {
        CrashLog.Write(ex, "MainPage.SaveDebts");
    }
}

// ================= АНАЛИТИКА =================

private bool _suppressReload;

}

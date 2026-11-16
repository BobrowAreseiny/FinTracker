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
private void RenderSettings()
{
    SettingsCatSub.Text = $"Активно: {_expenseCategories.Count} кат. расходов и {_incomeCategories.Count} кат. доходов. Нажмите для настройки.";
CurrencySeg.Select(CurrencySegIndex, raise: false);
    ThemeModeSeg?.Select(_isLightTheme ? 1 : 0, raise: false);
    RenderGlowColorPicker();
    UpdateAutoTrackUi();
}

private void UpdateAutoTrackUi()
{
    bool permEnabled = !NotificationAccess.SupportsAutoCapture || NotificationAccess.IsEnabled();
    bool prefEnabled = Preferences.Default.Get("FT_AutoTrack", true);
    bool active = permEnabled && prefEnabled;

    AutoTrackLabel.Text = active
    ? "Автоучёт активен"
    : (!permEnabled ? "Нужен доступ" : "Пауза");
    AutoTrackLabel.TextColor = active ? Ui.Success : SecondaryTextColor;
    AutoTrackIcon.Icon = active ? "check-circle-2" : "bot";
    AutoTrackIcon.Tint = active ? Ui.Success : SecondaryTextColor;
    AutoTrackButton.BackgroundColor = active ? Ui.Success.WithAlpha(0.10f) : SubtleFillColor;
    AutoTrackButton.Stroke = active ? Ui.Success.WithAlpha(0.22f) : HairlineStrokeColor;
    AutoTrackButton.StrokeThickness = 1;
    AutoTrackButton.Opacity = active ? 1.0 : 0.75;

    HeaderCurrencyLabel.Text = _currency switch
    {
        "USD" => "USD • $",
        "EUR" => "EUR • €",
        _ => "BYN • Br"
    };

    PrivacyIcon.Icon = _privacy ? "eye" : "eye-off";
}

private void SetCurrencyIndex(int index)
{
    _currency = index switch { 1 => "USD", 2 => "EUR", _ => "BYN" };
    Preferences.Default.Set("FT_CurrencyCode", _currency);
    CurrencySeg.Select(CurrencySegIndex, raise: false);
    _lastBalanceText = string.Empty;
    RenderAll();
}

private void OnCycleCurrencyTapped(object? sender, TappedEventArgs e)
{
    int next = (CurrencySegIndex + 1) % 3;
    SetCurrencyIndex(next);
    ShowToast($"Валюта отображения: {_currency} ({Symbol})", "coins");
}

private void OnTogglePrivacyTapped(object? sender, TappedEventArgs e)
{
    _privacy = !_privacy;
    Preferences.Default.Set("FT_Privacy", _privacy);
    _lastBalanceText = string.Empty;
    RenderAll();
    ShowToast(_privacy ? "Суммы скрыты" : "Суммы показаны", _privacy ? "eye-off" : "eye");
}

private void OnToggleAutoTrackTapped(object? sender, TappedEventArgs e)
{
    if (NotificationAccess.SupportsAutoCapture && !NotificationAccess.IsEnabled())
    {
        NotificationAccess.OpenSettings();
        ShowToast("Разрешите доступ к уведомлениям", "shield-alert");
        return;
    }

    bool nextState = !Preferences.Default.Get("FT_AutoTrack", true);
    Preferences.Default.Set("FT_AutoTrack", nextState);
    UpdateAutoTrackUi();
    ShowToast(nextState ? "Автоучёт включен" : "Автоучёт приостановлен", "bot");
}

private async void OnRefreshRatesTapped(object? sender, TappedEventArgs e)
{
    ShowToast("Обновляем курсы…", "refresh-cw");
    bool ok = await CurrencyRates.RefreshAsync();
    UpdateRatesLabel();
    RenderAll();
    ShowToast(ok ? "Курсы валют обновлены" : "Курсы валют актуализированы", "refresh-cw");
}

// ================= ШТОРКА ОПЕРАЦИИ =================

}

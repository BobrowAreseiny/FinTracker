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
private List<CategoryDef> ModalCategories => _modalType == "income" ? _incomeCategories : _expenseCategories;

private void OnAddTransactionTapped(object? sender, TappedEventArgs e)
{
    _editingTxId = null;
    AddTxTitleLabel.Text = "Новая операция";
    AddTxSubmitLabel.Text = "Сохранить операцию";
    AddTxDeleteButton.IsVisible = false;
    AddAmountEntry.Text = string.Empty;
    AddNoteEntry.Text = string.Empty;
    AddTxTypeSeg.Select(0, raise: false);
    AddTxAccountSeg.Select(_accountFilter == WCash ? 1 : 0, raise: false);
    _modalType = "expense";
    _modalAccount = _accountFilter == WCash ? WCash : WMain;
    _modalCatIndex = 0;

    RenderCategoryChips();
    _ = OpenSheetAsync(AddTxOverlay, AddTxSheet);
}

private void OpenEditTransaction(Transaction tx)
{
    _editingTxId = tx.Id;
    AddTxTitleLabel.Text = "Изменить операцию";
    AddTxSubmitLabel.Text = "Сохранить изменения";
    AddTxDeleteButton.IsVisible = true;

    bool income = IsIncome(tx);
    _modalType = income ? "income" : "expense";
    _modalAccount = (string.IsNullOrEmpty(tx.AccountId) ? AccMain : tx.AccountId) == AccCash ? WCash : WMain;

    int index = ModalCategories.FindIndex(c => string.Equals(c.Cat, tx.Category, StringComparison.OrdinalIgnoreCase));
    _modalCatIndex = index >= 0 ? index : 0;

    AddTxTypeSeg.Select(income ? 1 : 0, raise: false);
    AddTxAccountSeg.Select(_modalAccount == WCash ? 1 : 0, raise: false);

    decimal shownAmount = Math.Round(BynToDisplay(tx.Amount), 2);
    AddAmountEntry.Text = shownAmount.ToString("0.##", CultureInfo.InvariantCulture);
    AddNoteEntry.Text = tx.ShopName ?? string.Empty;

    RenderCategoryChips();
    _ = OpenSheetAsync(AddTxOverlay, AddTxSheet);
}

private void SetModalType(string type)
{
    _modalType = type;
    _modalCatIndex = 0;
    AddTxTypeSeg.Select(type == "income" ? 1 : 0, raise: false);
    RenderCategoryChips();
}

private void SetModalAccount(string account)
{
    _modalAccount = account;
    AddTxAccountSeg.Select(account == WCash ? 1 : 0, raise: false);
}

    private void RenderCategoryChips()
    {
        if (ModalCatChipsGrid is null) return;

        ModalCatChipsGrid.Children.Clear();
        ModalCatChipsGrid.RowDefinitions.Clear();
        ModalCatChipsGrid.ColumnDefinitions.Clear();
        ModalCatChipsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ModalCatChipsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var list = ModalCategories;
        if (_modalCatIndex >= list.Count) _modalCatIndex = 0;

        int cells = list.Count + 1;
        int rows = (int)Math.Ceiling(cells / 2.0);
        for (int r = 0; r < rows; r++) ModalCatChipsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < list.Count; i++)
        {
            var cat = list[i];
            bool active = i == _modalCatIndex;

            var badge = BuildThemedBadge(cat, 28, 14, 8);

            var content = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            content.Add(badge);
            content.Add(new Label
            {
                Text = cat.Cat,
                FontFamily = "InterMedium",
                FontSize = 12.5,
                TextColor = PrimaryTextColor,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            });

            var chip = new Border
            {
                BackgroundColor = active ? ActiveControlBg : SubtleFillColor,
                Stroke = active ? ActiveControlStroke : HairlineStrokeColor,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(11, 9),
                Content = content
            };

            int captured = i;
            HapticsClick(chip, () =>
            {
                _modalCatIndex = captured;
                RenderCategoryChips();
            });

            Grid.SetColumn(chip, i % 2);
            Grid.SetRow(chip, i / 2);
            ModalCatChipsGrid.Children.Add(chip);
        }

        Color customCategoryBg = _accentColor.WithAlpha(_isLightTheme ? 0.12f : 0.18f);
        Color customCategoryStroke = _accentColor.WithAlpha(_isLightTheme ? 0.30f : 0.38f);

        var addBadge = new Border
        {
            WidthRequest = 28,
            HeightRequest = 28,
            StrokeThickness = 1,
            Stroke = customCategoryStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            BackgroundColor = customCategoryBg,
            Padding = 0,
            Content = new LucideIcon
            {
                Icon = "plus",
                Size = 14,
                Tint = _accentColor,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var addContent = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center
        };
        addContent.Add(addBadge);
        addContent.Add(new Label
        {
            Text = "Своя категория",
            FontFamily = "InterMedium",
            FontSize = 12.5,
            TextColor = _accentColor,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        });

        var addChip = new Border
        {
            Stroke = customCategoryStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = customCategoryBg,
            Padding = new Thickness(11, 9),
            Content = addContent
        };

        HapticsClick(addChip, () => OpenCategoryManager(_modalType));

        Grid.SetColumn(addChip, list.Count % 2);
        Grid.SetRow(addChip, list.Count / 2);
        ModalCatChipsGrid.Children.Add(addChip);
    }

    private void OnQuickAmount10Tapped(object? sender, TappedEventArgs e) => AddQuickAmount(10m);
private void OnQuickAmount50Tapped(object? sender, TappedEventArgs e) => AddQuickAmount(50m);
private void OnQuickAmount100Tapped(object? sender, TappedEventArgs e) => AddQuickAmount(100m);
private void OnQuickAmount500Tapped(object? sender, TappedEventArgs e) => AddQuickAmount(500m);

private void AddQuickAmount(decimal delta)
{
    decimal current = ParseDecimal(AddAmountEntry.Text);
    AddAmountEntry.Text = (current + delta).ToString("0.##", CultureInfo.InvariantCulture);
}

private static decimal ParseDecimal(string? text)
{
    if (string.IsNullOrWhiteSpace(text)) return 0m;
    text = text.Replace(" ", string.Empty).Replace(',', '.').Trim();
    return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value) ? value : 0m;
}

private void OnSubmitTransactionTapped(object? sender, TappedEventArgs e)
{
    decimal entered = ParseDecimal(AddAmountEntry.Text);
    if (entered <= 0)
    {
        ShowToast("Введите корректную сумму", "alert-circle");
        Haptics.Warning();
        return;
    }

    decimal amountByn = Math.Round(InputToByn(entered), 2);
    var list = ModalCategories;
    var preset = list.Count > 0 ? list[Math.Clamp(_modalCatIndex, 0, list.Count - 1)] : ResolveCategory("Другое");
    string note = (AddNoteEntry.Text ?? string.Empty).Trim();
    if (note.Length == 0) note = preset.Cat;

    _suppressReload = true;

    if (_editingTxId is int id)
    {
        var tx = _transactions.FirstOrDefault(t => t.Id == id);
        if (tx is not null)
        {
            tx.Category = preset.Cat;
            tx.ShopName = note;
            tx.Amount = amountByn;
            tx.Type = _modalType;
            tx.AccountId = _modalAccount == WCash ? AccCash : AccMain;
            _databaseService.UpdateTransaction(tx);
        }

        _suppressReload = false;
        CloseAddTx();
        _ = LoadAsync();
        ShowToast("Операция обновлена", "check-circle-2");
        return;
    }

    _databaseService.AddTransaction(new Transaction
    {
        Amount = amountByn,
        Currency = "BYN",
        Category = preset.Cat,
        ShopName = note,
        Type = _modalType,
        AccountId = _modalAccount == WCash ? AccCash : AccMain,
        Date = DateTime.Now,
        Source = "manual"
    });

    _suppressReload = false;
    CloseAddTx();
    _ = LoadAsync();
    ShowToast("Операция добавлена", "sparkles");
}

private void OnDeleteTransactionTapped(object? sender, TappedEventArgs e)
{
    if (_editingTxId is not int id) return;

    var tx = _transactions.FirstOrDefault(t => t.Id == id);
    if (tx is null) return;

    _suppressReload = true;
    _databaseService.DeleteTransaction(tx);
    _suppressReload = false;

    _selectedIds.Remove(id);
    CloseAddTx();
    _ = LoadAsync();
    ShowToast("Операция удалена", "trash-2");
}

private void OnCloseAddTxTapped(object? sender, TappedEventArgs e) => CloseAddTx();

private void CloseAddTx()
{
    if (!AddTxOverlay.IsVisible) return;

    if (_returnToTxSheet)
    {
        _returnToTxSheet = false;
        _ = AnimateCloseOnly(CatMgrOverlay, CatMgrSheet);
        return;
    }

    _ = CloseSheetAsync(AddTxOverlay, AddTxSheet);
}

private async Task AnimateCloseOnly(Grid overlay, Border sheet)
{
    if (!overlay.IsVisible) return;
    await CloseSheetAsync(overlay, sheet);
    await OpenSheetAsync(AddTxOverlay, AddTxSheet);
}

// ================= МЕНЕДЖЕР КАТЕГОРИЙ (iOS CRUD + Jiggle) =================

}

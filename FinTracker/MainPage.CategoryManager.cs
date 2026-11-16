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
private void OnOpenCategoryManagerTapped(object? sender, TappedEventArgs e) => OpenCategoryManager(_modalType);
private void OnOpenExpenseCategoriesTapped(object? sender, TappedEventArgs e) => OpenCategoryManager("expense");
private void OnOpenIncomeCategoriesTapped(object? sender, TappedEventArgs e) => OpenCategoryManager("income");
private void OnManageCategoriesFromAnalyticsTapped(object? sender, TappedEventArgs e) => OpenCategoryManager(_analyticsFlow);

    private async void OpenCategoryManager(string type)
    {
        if (AddTxOverlay.IsVisible)
        {
            _returnToTxSheet = true;
            // Сначала плавно скрываем форму транзакции
            await CloseSheetAsync(AddTxOverlay, AddTxSheet);
        }

        _catMgrType = type;
        _jiggle = false;
        _editingCatId = null;
        CatMgrTypeSeg.Select(type == "expense" ? 0 : 1, raise: false);
        UpdateJiggleUi();
        ResetCategoryEditor();

        // И только затем выводим менеджер категорий
        await OpenSheetAsync(CatMgrOverlay, CatMgrSheet);
    }

    private void OnCloseCategoryManagerTapped(object? sender, TappedEventArgs e) => CloseCategoryManager();

    private async void CloseCategoryManager()
    {
        if (!CatMgrOverlay.IsVisible) return;

        _jiggle = false;
        StopJiggle();
        UpdateJiggleUi();

        // Плавно закрываем менеджер категорий
        await CloseSheetAsync(CatMgrOverlay, CatMgrSheet);

        RenderCategoryChips();
        RenderAll();

        if (_returnToTxSheet)
        {
            _returnToTxSheet = false;
            // Плавно возвращаем форму добавления транзакции
            await OpenSheetAsync(AddTxOverlay, AddTxSheet);
        }
    }

    private void SetCatMgrType(string type)
{
    _catMgrType = type;
    _editingCatId = null;
    CatMgrTypeSeg.Select(type == "expense" ? 0 : 1, raise: false);
    ResetCategoryEditor();
}

private void OnToggleJiggleTapped(object? sender, TappedEventArgs e)
{
    _jiggle = !_jiggle;
    UpdateJiggleUi();
    RenderCategoryGrid();

    if (_jiggle) ShowToast("Режим удаления: нажмите «−» на категории", "scissors");
}

private void UpdateJiggleUi()
{
    JiggleToggleLabel.Text = _jiggle ? "Готово" : "Изм.";
    JiggleToggleLabel.TextColor = _jiggle ? Colors.White : _accentColor;
    JiggleToggleButton.BackgroundColor = _jiggle ? _accentColor : SubtleFillColor;
    JiggleToggleButton.Stroke = _jiggle ? _accentColor : HairlineStrokeColor;
}

private void RenderCategoryGrid()
{
    CatMgrGrid.Children.Clear();
    CatMgrGrid.RowDefinitions.Clear();

    var list = _catMgrType == "expense" ? _expenseCategories : _incomeCategories;
    int rows = (int)Math.Ceiling(list.Count / 2.0);
    for (int r = 0; r < rows; r++) CatMgrGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

    for (int i = 0; i < list.Count; i++)
    {
        var cat = list[i];
        bool editing = _editingCatId == cat.Id;

        var badge = BuildThemedBadge(cat, 28, 14, 8);

        var left = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        left.Add(badge);
        left.Add(new Label
        {
            Text = cat.Cat,
            FontFamily = "InterMedium",
            FontSize = 12.5,
            TextColor = PrimaryTextColor,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        });

        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };
        grid.Add(left, 0, 0);
        grid.Add(new LucideIcon { Icon = "pen-line", Size = 12, Tint = SecondaryTextColor.WithAlpha(0.7f) }, 1, 0);

        var chip = new Border
        {
            BackgroundColor = editing ? ActiveControlBg : SubtleFillColor,
            Stroke = editing ? ActiveControlStroke : HairlineStrokeColor,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(11, 9),
            Content = grid
        };

        string catId = cat.Id;
        HapticsClick(chip, () => SelectCategoryForEdit(catId));

        View cell = chip;

        if (_jiggle)
        {
            var minus = new Border
            {
                WidthRequest = 20,
                HeightRequest = 20,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                BackgroundColor = Ui.Danger,
                Content = new LucideIcon { Icon = "minus", Size = 12, Tint = Colors.White },
                TranslationX = -5,
                TranslationY = -5,
                ZIndex = 2,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start
            };

            var host = new Grid();
            host.Add(chip);
            host.Add(minus);
            cell = host;

            HapticsClick(minus, () => _ = DeleteCategory(catId, host));

            var anim = new Animation();
            anim.Add(0, 0.5, new Animation(v => chip.Rotation = v, -1.1, 1.1));
            anim.Add(0.5, 1, new Animation(v => chip.Rotation = v, 1.1, -1.1));
            anim.Commit(chip, "jiggle", 16, 280, Easing.Linear, null, () => _jiggle);
        }

        Grid.SetColumn(cell, i % 2);
        Grid.SetRow(cell, i / 2);
        CatMgrGrid.Children.Add(cell);
    }
}

private void StopJiggle()
{
    foreach (var child in CatMgrGrid.Children)
    {
        if (child is Border border)
        {
            border.AbortAnimation("jiggle");
            border.Rotation = 0;
        }
        else if (child is Grid g && g.Children.FirstOrDefault() is Border innerBorder)
        {
            innerBorder.AbortAnimation("jiggle");
            innerBorder.Rotation = 0;
        }
    }
}

private void ResetCategoryEditor()
{
    _editingCatId = null;
    _catEditorIcon = _catMgrType == "expense" ? "coffee" : "briefcase";
    _catEditorColorIndex = _catMgrType == "expense" ? 0 : 4;

    CatEditorTitle.Text = "Новая категория";
    CatSubmitLabel.Text = "+ Добавить категорию";
    CatCancelEditButton.IsVisible = false;
    CatDeleteButton.IsVisible = false;
    CatNameEntry.Text = string.Empty;

    RenderCategoryEditor();
}

private void OnResetCategoryEditorTapped(object? sender, TappedEventArgs e) => ResetCategoryEditor();

private void RenderCategoryEditor()
{
    RenderColorSwatches();
    RenderIconPicker();
    UpdateCategoryPreview();
    RenderCategoryGrid();
}

private void RenderColorSwatches()
{
    CatColorRow.Children.Clear();

    for (int i = 0; i < CategoryCatalog.Presets.Length; i++)
    {
        var preset = CategoryCatalog.Presets[i];
        bool active = i == _catEditorColorIndex;
        Color c = Color.FromArgb(preset.ChartColor);

        var swatch = new Border
        {
            WidthRequest = active ? 32 : 28,
            HeightRequest = active ? 32 : 28,
            StrokeThickness = active ? 2 : 0,
            Stroke = active ? PrimaryTextColor : Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            BackgroundColor = c,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Shadow = active
        ? new Shadow { Brush = new SolidColorBrush(c), Offset = new Point(0, 0), Radius = 12, Opacity = 0.35f }
        : null
        };

        int index = i;
        HapticsClick(swatch, () =>
        {
            _catEditorColorIndex = index;
            RenderCategoryEditor();
        });

        Grid.SetColumn(swatch, i);
        CatColorRow.Children.Add(swatch);
    }
}

private void RenderIconPicker()
{
    CatIconGrid.Children.Clear();
    CatIconGrid.RowDefinitions.Clear();

    var icons = CategoryCatalog.IconLibrary;
    int rows = (int)Math.Ceiling(icons.Length / 5.0);
    for (int r = 0; r < rows; r++) CatIconGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

    for (int i = 0; i < icons.Length; i++)
    {
        bool active = icons[i] == _catEditorIcon;

        var cell = new Border
        {
            HeightRequest = 36,
            StrokeThickness = 1,
            Stroke = active ? _accentColor : HairlineStrokeColor,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            BackgroundColor = active ? ActiveControlBg : SubtleFillColor,
            Scale = active ? 1.05 : 1.0,
            Content = new LucideIcon
            {
                Icon = icons[i],
                Size = 16,
                Tint = active ? PrimaryTextColor : SecondaryTextColor
            }
        };

        string icon = icons[i];
        HapticsClick(cell, () =>
        {
            _catEditorIcon = icon;
            RenderCategoryEditor();
        });

        Grid.SetColumn(cell, i % 5);
        Grid.SetRow(cell, i / 5);
        CatIconGrid.Children.Add(cell);
    }
}

private void UpdateCategoryPreview()
{
    var preset = CategoryCatalog.Presets[Math.Clamp(_catEditorColorIndex, 0, CategoryCatalog.Presets.Length - 1)];
    Color chartColor = SafeParseColor(preset.ChartColor, _accentColor);

    if (_isLightTheme)
    {
        CatPreviewBox.BackgroundColor = chartColor.WithAlpha(0.12f);
        CatPreviewBox.Stroke = chartColor.WithAlpha(0.28f);
        CatPreviewIcon.Tint = chartColor;
    }
    else
    {
        CatPreviewBox.BackgroundColor = Color.FromArgb(preset.BoxBg);
        CatPreviewBox.Stroke = Color.FromArgb(preset.BoxBorder);
        CatPreviewIcon.Tint = Color.FromArgb(preset.IconColor);
    }

    CatPreviewIcon.Icon = _catEditorIcon;
}

private void SelectCategoryForEdit(string catId)
{
    var list = _catMgrType == "expense" ? _expenseCategories : _incomeCategories;
    var target = list.FirstOrDefault(c => c.Id == catId);
    if (target is null) return;

    _editingCatId = target.Id;
    CatEditorTitle.Text = "Редактирование категории";
    CatSubmitLabel.Text = "Сохранить изменения";
    CatCancelEditButton.IsVisible = true;
    CatDeleteButton.IsVisible = true;
    CatNameEntry.Text = target.Cat;

    _catEditorIcon = CategoryCatalog.IconLibrary.Contains(target.Icon) ? target.Icon : CategoryCatalog.IconLibrary[0];

    int presetIndex = Array.FindIndex(CategoryCatalog.Presets,
    p => p.ChartColor.Equals(target.ChartColor, StringComparison.OrdinalIgnoreCase));
    _catEditorColorIndex = presetIndex >= 0 ? presetIndex : 0;

    RenderCategoryEditor();
}

    private void OnSaveCategoryTapped(object? sender, TappedEventArgs e)
    {
        string name = (CatNameEntry.Text ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            ShowToast("Введите название категории", "alert-circle");
            Haptics.Warning();
            return;
        }

        var preset = CategoryCatalog.Presets[Math.Clamp(_catEditorColorIndex, 0, CategoryCatalog.Presets.Length - 1)];
        var list = _catMgrType == "expense" ? _expenseCategories : _incomeCategories;

        if (_editingCatId is string editingId)
        {
            var target = list.FirstOrDefault(c => c.Id == editingId);
            if (target is null) return;

            string oldName = target.Cat;
            target.Cat = name;
            target.Icon = _catEditorIcon;
            target.BoxBg = preset.BoxBg;
            target.BoxBorder = preset.BoxBorder;
            target.IconColor = preset.IconColor;
            target.ChartColor = preset.ChartColor;

            if (!string.Equals(oldName, name, StringComparison.Ordinal))
            {
                RenameCategoryInTransactions(oldName, name);
            }

            PersistCategories();

            // Сбрасываем флаг возврата, чтобы выйти сразу на главную
            _returnToTxSheet = false;
            CloseCategoryManager();

            ShowToast("Категория «" + name + "» обновлена", "check-circle-2");
            return;
        }

        if (list.Any(c => c.Cat.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowToast("Такая категория уже есть", "alert-circle");
            return;
        }

        list.Add(new CategoryDef
        {
            Id = _catMgrType + "-" + DateTime.Now.Ticks,
            Cat = name,
            Icon = _catEditorIcon,
            BoxBg = preset.BoxBg,
            BoxBorder = preset.BoxBorder,
            IconColor = preset.IconColor,
            ChartColor = preset.ChartColor
        });

        PersistCategories();
        if (_modalType == _catMgrType)
        {
            _modalCatIndex = list.Count - 1;
        }

        // Сбрасываем флаг возврата, чтобы выйти сразу на главную
        _returnToTxSheet = false;
        CloseCategoryManager();

        ShowToast("Категория «" + name + "» создана", "sparkles");
    }

    private void RenameCategoryInTransactions(string oldName, string newName)
{
    var affected = _transactions
    .Where(t => !IsTransfer(t) && string.Equals(t.Category, oldName, StringComparison.Ordinal))
    .ToList();

    if (affected.Count == 0) return;

    _suppressReload = true;
    foreach (var tx in affected)
    {
        tx.Category = newName;
        _databaseService.UpdateTransaction(tx);
    }
    _suppressReload = false;
}

private void PersistCategories() => CategoryCatalog.Save(_expenseCategories, _incomeCategories);

private void OnDeleteEditingCategoryTapped(object? sender, TappedEventArgs e)
{
    if (_editingCatId is string id) _ = DeleteCategory(id);
}

private async Task DeleteCategory(string id, VisualElement? visualItem = null)
{
    var list = _catMgrType == "expense" ? _expenseCategories : _incomeCategories;

    if (list.Count <= 1)
    {
        ShowToast("Оставьте хотя бы 1 категорию", "shield-alert");
        return;
    }

    var target = list.FirstOrDefault(c => c.Id == id);
    if (target is null) return;

    if (visualItem is not null)
    {
        await Task.WhenAll(
        visualItem.ScaleTo(0.4, 200, Easing.CubicIn),
        visualItem.FadeTo(0, 200, Easing.CubicIn));
    }

    list.Remove(target);
    PersistCategories();

    if (_editingCatId == id) ResetCategoryEditor();
    else RenderCategoryEditor();

    RenderCategoryChips();
    RenderAll();
    ShowToast("Категория «" + target.Cat + "» удалена", "trash-2");
}

// ================= ПЕРЕВОД МЕЖДУ СЧЕТАМИ =================

}

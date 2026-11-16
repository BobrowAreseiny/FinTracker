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
    private string GroupName(DateTime date)
    {
        var today = DateTime.Today;
        if (date.Date == today) return "Сегодня";
        if (date.Date == today.AddDays(-1)) return "Вчера";
        if (date.Year == today.Year && date.Month == today.Month) return "Ранее в этом месяце";
        return date.Year == today.Year
        ? Capitalize(MonthShort[date.Month - 1])
        : $"{Capitalize(MonthShort[date.Month - 1])} {date.Year}";
    }

    private CategoryDef ResolveCategory(string name)
    {
        if (string.Equals(name, "Перевод", StringComparison.OrdinalIgnoreCase))
        {
            return new CategoryDef
            {
                Cat = "Перевод",
                Icon = "arrow-right-left",
                BoxBg = "#1E1B36",
                BoxBorder = "#2D2852",
                IconColor = "#C4B5FD",
                ChartColor = _accentHex
            };
        }

        var found = _expenseCategories.FirstOrDefault(c => c.Cat.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? _incomeCategories.FirstOrDefault(c => c.Cat.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (found is not null) return found;

        var special = CategoryCatalog.Special(name);
        if (special is not null) return special;

        return new CategoryDef
        {
            Cat = name,
            Icon = "circle-off",
            BoxBg = "#17171A",
            BoxBorder = "#2E2E32",
            IconColor = "#9E9E9E",
            ChartColor = "#64748B"
        };
    }

    private List<Transaction> GetFilteredTransactions()
    {
        string query = (TxSearchEntry?.Text ?? string.Empty).Trim().ToLowerInvariant();

        return _transactions.Where(t =>
        {
            if (_accountFilter != "all" && !MatchesAccount(t, _accountFilter)) return false;
            if (_txTypeFilter != "all")
            {
                if (IsTransfer(t)) return false;
                bool wantIncome = _txTypeFilter == "income";
                if (wantIncome != IsIncome(t)) return false;
            }
            if (query.Length > 0)
            {
                string hay = (t.Category ?? string.Empty) + " " + (t.ShopName ?? string.Empty);
                if (!hay.ToLowerInvariant().Contains(query, StringComparison.Ordinal)) return false;
            }
            return true;
        }).ToList();
    }

    private void RenderTransactions()
    {
        TxListContainer.Children.Clear();
        var filtered = GetFilteredTransactions();

        // Очищаем выбранные ID, которых больше нет в базе
        _selectedIds.RemoveWhere(id => _transactions.All(t => t.Id != id));
        if (_selectedIds.Count > 0 && !_selectionMode)
        {
            _selectionMode = true;
        }

        UpdateSelectionUi(filtered.Count);

        // Панель выбора живёт в шапке карточки, а не в списке: контент не сдвигается.
        TxSelectionActionsHost.Children.Clear();
        TxSelectionActionsHost.IsVisible = _selectionMode;
        TxTypeSeg.IsVisible = !_selectionMode;

        if (_selectionMode)
        {
            TxSelectionActionsHost.Add(BuildSelectionBar(filtered));
        }

        if (filtered.Count == 0)
        {
            TxListContainer.Add(new Label
            {
                Text = "Операции не найдены",
                FontFamily = "Inter",
                FontSize = 13,
                TextColor = SecondaryTextColor,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 8)
            });
            return;
        }

        foreach (var group in filtered.GroupBy(t => GroupName(t.Date)))
        {
            var items = group.ToList();
            decimal net = items
            .Where(t => !IsTransfer(t))
            .Sum(t => IsIncome(t) ? t.Amount : -t.Amount);

            var header = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                Margin = new Thickness(0, 0, 0, 8)
            };

            header.Add(new Label
            {
                Text = group.Key.ToUpperInvariant(),
                FontFamily = "InterSemiBold",
                FontSize = 10.5,
                CharacterSpacing = 1.2,
                TextColor = SecondaryTextColor,
                VerticalTextAlignment = TextAlignment.Center
            }, 0, 0);

            header.Add(new Label
            {
                Text = (net > 0 ? "+" : net < 0 ? "–" : string.Empty) + Money(Math.Abs(net)),
                FontFamily = "InterSemiBold",
                FontSize = 11,
                TextColor = net > 0 ? Ui.Success : SecondaryTextColor,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalTextAlignment = TextAlignment.End
            }, 1, 0);

            TxListContainer.Add(header);

            var rows = new VerticalStackLayout { Spacing = 10 };
            foreach (var tx in items) rows.Add(BuildTransactionRow(tx));
            TxListContainer.Add(rows);
        }
    }

    private View BuildTransactionRow(Transaction tx)
    {
        var cat = ResolveCategory(tx.Category ?? string.Empty);
        bool transfer = IsTransfer(tx);
        bool income = IsIncome(tx);
        bool selected = _selectedIds.Contains(tx.Id);

        string accBadge = transfer
        ? (tx.AccountId == AccCash ? "Основной → Наличные" : "Наличные → Основной")
        : ((string.IsNullOrEmpty(tx.AccountId) ? AccMain : tx.AccountId) == AccCash ? "Наличные" : "Основной");

        var badge = BuildThemedBadge(cat, 40, 16, 13);
        var title = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill
        };
        title.Add(new Label
        {
            Text = tx.Category ?? "Без категории",
            FontFamily = "InterMedium",
            FontSize = 14.5,
            TextColor = PrimaryTextColor,
            LineBreakMode = LineBreakMode.TailTruncation
        });

        string note = string.IsNullOrWhiteSpace(tx.ShopName)
        ? (transfer ? "Перевод между счетами" : income ? "Доход" : "Расход")
        : tx.ShopName;

        var noteLabel = new Label
        {
            FontFamily = "Inter",
            FontSize = 12,
            TextColor = SecondaryTextColor,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        noteLabel.FormattedText = new FormattedString
        {
            Spans =
{
new Span { Text = note + " • ", FontSize = 12, FontFamily = "Inter", TextColor = SecondaryTextColor },
new Span { Text = accBadge, FontSize = 12, FontFamily = "Inter", TextColor = PrimaryTextColor.WithAlpha(0.6f) }
}
        };
        title.Add(noteLabel);

        var row = new Grid
        {
            ColumnDefinitions =
        [
        new ColumnDefinition(GridLength.Auto),
new ColumnDefinition(GridLength.Star),
new ColumnDefinition(GridLength.Auto)
         ],
            ColumnSpacing = 12,
            Padding = new Thickness(8, 6),
            BackgroundColor = Colors.Transparent
        };

        var left = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center };

        // Чекбокс показываем только в режиме выделения — в обычном списке он не нужен.
        if (_selectionMode)
        {
            var checkDot = BuildSelectionDot(tx.Id);
            HapticsClick(checkDot, () => ToggleSelected(tx.Id));
            left.Add(checkDot);
        }

        left.Add(badge);

        string prefix = transfer ? "⇄ " : income ? "+" : "–";
        Color amountColor = transfer ? _accentColor : income ? Ui.Success : PrimaryTextColor;

        var amount = new Label
        {
            Text = prefix + Money(tx.Amount),
            FontFamily = "InterSemiBold",
            FontSize = 14.5,
            TextColor = amountColor,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.End,
            LineBreakMode = LineBreakMode.NoWrap
        };

        var right = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        right.Add(amount);
        right.Add(new LucideIcon
        {
            Icon = "chevron-right",
            Size = 14,
            Tint = SecondaryTextColor.WithAlpha(0.6f),
            IsVisible = !_selectionMode
        });

        row.Add(left, 0, 0);
        row.Add(title, 1, 0);
        row.Add(right, 2, 0);

        var container = new Border
        {
            StrokeThickness = selected ? 1 : 0,
            Stroke = selected ? _accentColor.WithAlpha(0.45f) : Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = selected ? _accentColor.WithAlpha(_isLightTheme ? 0.10f : 0.12f) : Colors.Transparent,
            Content = row,
            Padding = 0
        };

        HapticsClick(container, () =>
        {
            if (_selectionMode)
            {
                ToggleSelected(tx.Id);
                return;
            }

            OpenEditTransaction(tx);
        });

        return container;
    }

    // ================= УПРАВЛЕНИЕ МУЛЬТИ-ВЫДЕЛЕНИЕМ =================

    private void UpdateSelectionUi(int visibleCount)
    {
        if (SelectModeButton is not null)
        {
            SelectModeButton.IsVisible = true;
            SelectModeButton.BackgroundColor = _selectionMode ? ActiveControlBg : SubtleFillColor;
            SelectModeButton.Stroke = _selectionMode ? ActiveControlStroke : HairlineStrokeColor;
            SelectModeIcon.Tint = _selectionMode ? _accentColor : SecondaryTextColor;

            if (SelectModeLabel is not null)
            {
                SelectModeLabel.Text = _selectionMode ? "Готово" : "Выбрать";
                SelectModeLabel.TextColor = _selectionMode ? _accentColor : SecondaryTextColor;
            }
        }

        if (TxHintLabel is not null)
        {
            TxHintLabel.IsVisible = _selectionMode;
            if (_selectionMode)
            {
                TxHintLabel.Text = $"Выбрано: {_selectedIds.Count}";
                TxHintLabel.TextColor = _accentColor;
                TxHintLabel.FontFamily = "InterSemiBold";
            }
        }
    }

    private View BuildSelectionDot(int txId)
    {
        bool selected = _selectedIds.Contains(txId);

        return new Border
        {
            WidthRequest = 22,
            HeightRequest = 22,
            StrokeThickness = 1.5,
            Stroke = selected ? _accentColor : (_isLightTheme ? Color.FromArgb("#401C1917") : Color.FromArgb("#40FFFFFF")),
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            BackgroundColor = selected ? _accentColor : Colors.Transparent,
            Padding = 0,
            Content = selected
        ? new LucideIcon { Icon = "check", Size = 12, Tint = Colors.White, Stroke = 2.6 }
        : null,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private View BuildSelectionBar(List<Transaction> filtered)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 8,
            Margin = new Thickness(0)
        };

        bool allSelected = filtered.Count > 0 && filtered.All(t => _selectedIds.Contains(t.Id));

        var selectAllBtn = BuildSelectionBarButton(
            allSelected ? "Снять" : "Все",
            SubtleFillColor,
            HairlineStrokeColor,
            PrimaryTextColor,
            ToggleSelectAllFiltered);

        var deleteBtn = BuildSelectionBarButton(
            _selectedIds.Count > 0 ? $"Удалить ({_selectedIds.Count})" : "Удалить",
            Ui.Danger.WithAlpha(0.16f),
            Ui.Danger.WithAlpha(0.35f),
            Ui.Danger,
            () => OnDeleteSelectedTapped(null, null));
        deleteBtn.Opacity = _selectedIds.Count > 0 ? 1.0 : 0.45;

        grid.Add(selectAllBtn, 0, 0);
        grid.Add(deleteBtn, 1, 0);

        return grid;
    }

    /// <summary>Кнопка панели выбора: равная ширина, одна строка, центрированный текст.</summary>
    private Border BuildSelectionBarButton(string text, Color bg, Color stroke, Color textColor, Action action)
    {
        var border = new Border
        {
            BackgroundColor = bg,
            Stroke = stroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(10, 6),
            HorizontalOptions = LayoutOptions.Fill,
            Content = new Label
            {
                Text = text,
                FontFamily = "InterSemiBold",
                FontSize = 12,
                TextColor = textColor,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.NoWrap
            }
        };

        HapticsClick(border, action);
        return border;
    }


private void ToggleSelectAllFiltered()
{
    var filtered = GetFilteredTransactions();
    if (filtered.Count == 0) return;

    bool allSelected = filtered.All(t => _selectedIds.Contains(t.Id));
    if (allSelected)
    {
        foreach (var t in filtered) _selectedIds.Remove(t.Id);
    }
    else
    {
        foreach (var t in filtered) _selectedIds.Add(t.Id);
    }

    RenderTransactions();
}

private void ToggleSelected(int txId)
{
    if (!_selectedIds.Add(txId)) _selectedIds.Remove(txId);
    RenderTransactions();
}

private void OnToggleSelectionModeTapped(object? sender, TappedEventArgs e)
{
    _selectionMode = !_selectionMode;
    if (!_selectionMode) _selectedIds.Clear();

    RenderTransactions();

    if (_selectionMode) ShowToast("Выберите операции", "list-checks");
}

private async void OnDeleteSelectedTapped(object? sender, TappedEventArgs? e)
{
    if (_selectedIds.Count == 0)
    {
        ShowToast("Выберите операции", "check");
        return;
    }

    int count = _selectedIds.Count;
    bool confirmed = await ShowConfirmAsync(
    "Удалить выбранные операции?",
    count == 1
    ? "Выбранная операция будет удалена без возможности отмены."
    : $"Будет удалено операций: {count}. Отменить это действие нельзя.",
"Удалить");

    if (!confirmed) return;

    var toDelete = _transactions.Where(t => _selectedIds.Contains(t.Id)).ToList();

    _suppressReload = true;
    foreach (var tx in toDelete) _databaseService.DeleteTransaction(tx);
    _suppressReload = false;

    _selectedIds.Clear();
    _selectionMode = false;

    await LoadAsync();
    ShowToast(count == 1 ? "Операция удалена" : $"Удалено операций: {count}", "trash-2");
}

// ================= ВКЛАДКИ И НАВИГАЦИЯ =================

}

using FinTracker.Services;
using Microsoft.Maui.Devices;

namespace FinTracker;

/// <summary>
/// Телефонные жесты страницы: свайп вбок по экрану (вправо — назад, влево — на следующую
/// вкладку) и смахивание нижней шторки вниз. Сами касания распознаёт платформенный слой
/// (Platforms/Android/GestureRouter) и зовёт методы этого партиала через IGestureHost.
/// </summary>
public partial class MainPage : IGestureHost
{
    /// <summary>Порядок вкладок в нижнем меню — по нему «листаем» свайпом влево.</summary>
    private static readonly string[] TabsOrder = ["main", "analytics", "accounts", "settings"];

    private bool _sheetDragging;
    private double _sheetDragHeight = 480;
    private double _sheetDragBaseOpacity = 1;

    private void AttachGestureHost()
    {
        GestureBridge.Host = this;
    }

    private void DetachGestureHost()
    {
        if (ReferenceEquals(GestureBridge.Host, this)) GestureBridge.Host = null;
    }

    // ================= СВАЙП ВБОК =================

    public bool OnHorizontalSwipe(int direction, double startX, double startY)
    {
        if (_fatalShown) return false;

        // Открыта шторка: свайп вправо работает как «назад» — закрывает её.
        if (VisibleSheet() is { } sheet)
        {
            if (direction < 0) return false;

            Haptics.Click();
            CloseSheetProgrammatically(sheet);
            return true;
        }

        if (IsSwipeBlocked(startX, startY)) return false;

        // В режиме выделения листаем только назад — вперёд не уводим пользователя.
        if (direction < 0 && _selectionMode) return false;

        return direction > 0 ? SwipeBack() : SwipeForward();
    }

    /// <summary>Свайп вправо — как «назад»: из выделения, на предыдущую вкладку или на главную.</summary>
    private bool SwipeBack()
    {
        if (!TryNavigateBack(slideFrom: -70)) return false;

        Haptics.Click();
        return true;
    }

    /// <summary>Свайп влево — на следующую вкладку нижнего меню.</summary>
    private bool SwipeForward()
    {
        int index = Array.IndexOf(TabsOrder, _activeTab);
        if (index < 0 || index >= TabsOrder.Length - 1) return false;

        Haptics.Click();
        SetActiveTab(TabsOrder[index + 1], animate: true, record: true, slideFrom: 70);
        return true;
    }

    /// <summary>
    /// Зоны, где свайп вбок делает что-то другое: слайдеры, нижнее меню,
    /// диаграммы и горизонтальные прокрутки (пилюли счетов и типов графика).
    /// </summary>
    private bool IsSwipeBlocked(double x, double y)
    {
        VisualElement?[] controls =
        [
            CustomHueSlider,
            GlowOpacitySlider,
            NavBarBorder
        ];

        foreach (var control in controls)
        {
            if (control is null || !IsEffectivelyVisible(control)) continue;

            if (TryGetViewRect(control, out double left, out double top, out double right, out double bottom)
                && x >= left && x <= right && y >= top && y <= bottom)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Проверяет видимость с учётом скрытых вкладок-родителей: скрытый контент
    /// сохраняет координаты, но не должен перехватывать свайпы.
    /// </summary>
    private static bool IsEffectivelyVisible(VisualElement element)
    {
        Element? current = element;
        while (current is not null)
        {
            if (current is VisualElement visual && !visual.IsVisible) return false;
            current = current.Parent;
        }

        return true;
    }

    // ================= ШТОРКИ: СМАХИВАНИЕ ВНИЗ =================

    public bool IsSheetOpen => VisibleSheet() is not null;

    public bool IsPointOnSheet(double x, double y)
    {
        if (VisibleSheet() is not { } sheet) return false;

        return TryGetViewRect(sheet.Sheet, out double left, out double top, out double right, out double bottom)
            && x >= left && x <= right && y >= top && y <= bottom;
    }

    public bool CanDragSheet()
    {
        if (VisibleSheet() is not { } sheet) return false;

        // Пока хоть одна прокрутка внутри шторки не в самом начале — жест отдаём ей.
        foreach (var scroll in FindScrollViews(sheet.Sheet))
        {
            if (scroll.ScrollY > 0.5) return false;
        }

        return true;
    }

    public void OnSheetDragStart()
    {
        if (VisibleSheet() is not { } sheet) return;

        _sheetDragging = true;
        _sheetDragHeight = Math.Max(sheet.Sheet.Height, 240);
        _sheetDragBaseOpacity = sheet.Overlay.Opacity <= 0 ? 1 : sheet.Overlay.Opacity;

        sheet.Sheet.CancelAnimations();
        sheet.Overlay.CancelAnimations();
        AppHost.CancelAnimations();
    }

    public void OnSheetDrag(double dy)
    {
        if (!_sheetDragging) return;

        if (VisibleSheet() is not { } sheet)
        {
            _sheetDragging = false;
            return;
        }

        double offset = Math.Max(0, dy);
        double progress = Math.Clamp(offset / (_sheetDragHeight * 0.6), 0, 1);

        sheet.Sheet.TranslationY = offset;
        sheet.Overlay.Opacity = _sheetDragBaseOpacity * (1 - progress);
        AppHost.Scale = 0.945 + 0.055 * progress;
        AppHost.TranslationY = 8 - 8 * progress;
    }

    public void OnSheetDragEnd(double dy, double velocity, bool canceled)
    {
        if (!_sheetDragging) return;
        _sheetDragging = false;

        if (VisibleSheet() is not { } sheet) return;

        double offset = Math.Max(0, dy);
        bool dismiss = !canceled
            && (offset > 96 || (velocity > 520 && offset > 36));

        if (dismiss)
        {
            Haptics.Click();
            CloseSheetProgrammatically(sheet);
            return;
        }

        // Не дотянули — шторка мягко возвращается на место.
        _ = Task.WhenAll(
            sheet.Sheet.TranslateTo(0, 0, 220, Easing.CubicOut),
            sheet.Overlay.FadeTo(1, 220, Easing.CubicOut),
            AppHost.ScaleTo(0.945, 220, Easing.CubicOut),
            AppHost.TranslateTo(0, 8, 220, Easing.CubicOut));
    }

    public void OnSheetSwipeDown()
    {
        if (VisibleSheet() is not { } sheet) return;

        Haptics.Click();
        CloseSheetProgrammatically(sheet);
    }

    private void CloseSheetProgrammatically((Grid Overlay, Border Sheet) sheet)
    {
        if (sheet.Overlay == AddTxOverlay) CloseAddTx();
        else if (sheet.Overlay == CatMgrOverlay) CloseCategoryManager();
        else if (sheet.Overlay == TransferOverlay) CloseTransfer();
        else if (sheet.Overlay == LimitOverlay) CloseLimit();
        else if (sheet.Overlay == DebtOverlay) CloseDebt();
        else if (sheet.Overlay == CsvOverlay) CloseCsv();
        else if (sheet.Overlay == ConfirmOverlay) _confirmTcs?.TrySetResult(false);
    }

    // ================= ПОИСК ЭЛЕМЕНТОВ =================

    private (Grid Overlay, Border Sheet)? VisibleSheet()
    {
        if (_fatalShown) return null;

        if (AddTxOverlay.IsVisible) return (AddTxOverlay, AddTxSheet);
        if (CatMgrOverlay.IsVisible) return (CatMgrOverlay, CatMgrSheet);
        if (TransferOverlay.IsVisible) return (TransferOverlay, TransferSheet);
        if (LimitOverlay.IsVisible) return (LimitOverlay, LimitSheet);
        if (DebtOverlay.IsVisible) return (DebtOverlay, DebtSheet);
        if (CsvOverlay.IsVisible) return (CsvOverlay, CsvSheet);
        if (ConfirmOverlay.IsVisible) return (ConfirmOverlay, ConfirmSheet);

        return null;
    }

    private static IEnumerable<ScrollView> FindScrollViews(IVisualTreeElement element)
    {
        if (element is ScrollView scrollView) yield return scrollView;

        foreach (var child in element.GetVisualChildren())
        {
            foreach (var nested in FindScrollViews(child)) yield return nested;
        }
    }

    /// <summary>Положение элемента на экране в DIP (те же координаты, что у жестов).</summary>
    private static bool TryGetViewRect(VisualElement element, out double left, out double top, out double right, out double bottom)
    {
        left = 0;
        top = 0;
        right = 0;
        bottom = 0;

#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View view && view.Width > 0 && view.Height > 0)
        {
            var location = new int[2];
            view.GetLocationInWindow(location);

            double density = DeviceDisplay.MainDisplayInfo.Density;
            if (density <= 0) density = 1;

            left = location[0] / density;
            top = location[1] / density;
            right = left + view.Width / density;
            bottom = top + view.Height / density;
            return true;
        }
#else
        _ = element;
#endif

        return false;
    }
}

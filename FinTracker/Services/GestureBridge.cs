namespace FinTracker.Services;

/// <summary>
/// Что страница умеет делать с «телефонными» жестами.
/// Все координаты и расстояния — в DIP (как Margin/TranslationY в MAUI).
/// </summary>
public interface IGestureHost
{
    /// <summary>Открыта ли хоть одна нижняя шторка.</summary>
    bool IsSheetOpen { get; }

    /// <summary>Лежит ли точка на самой шторке (а не на затемнении вокруг неё).</summary>
    bool IsPointOnSheet(double x, double y);

    /// <summary>Можно ли утянуть шторку вниз: её контент прокручен в самое начало.</summary>
    bool CanDragSheet();

    /// <summary>
    /// Свайп вбок: +1 — вправо (значит «назад»), −1 — влево («вперёд»).
    /// Возвращает true, если жест что-то сделал.
    /// </summary>
    bool OnHorizontalSwipe(int direction, double startX, double startY);

    /// <summary>Шторку начали тянуть вниз.</summary>
    void OnSheetDragStart();

    /// <summary>Шторку тянут: dy — смещение вниз от точки захвата.</summary>
    void OnSheetDrag(double dy);

    /// <summary>Палец отпущен: dy — итоговое смещение, velocity — скорость вниз (DIP/с).</summary>
    void OnSheetDragEnd(double dy, double velocity, bool canceled);

    /// <summary>Быстрый свайп вниз по затемнению — просто закрыть шторку.</summary>
    void OnSheetSwipeDown();
}

/// <summary>
/// Мост между платформенным распознаванием жестов и страницей.
/// Платформенный слой только «подслушивает» касания и никогда их не перехватывает,
/// поэтому прокрутка, тапы, ввод текста и слайдеры продолжают работать как обычно.
/// </summary>
public static class GestureBridge
{
    public static IGestureHost? Host { get; set; }
}

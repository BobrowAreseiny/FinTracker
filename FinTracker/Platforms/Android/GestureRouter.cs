using Android.Views;

namespace FinTracker.Services;

/// <summary>
/// Распознавание «телефонных» жестов на уровне окна: свайп вбок по экрану
/// (назад/вперёд по вкладкам) и смахивание нижней шторки вниз.
///
/// Работает как наблюдатель: активити получает каждый кадр касания в
/// DispatchTouchEvent и просто передаёт его сюда, а событие идёт дальше по дереву.
/// Поэтому обычная прокрутка, тапы и ввод продолжают работать без изменений.
/// </summary>
internal static class GestureRouter
{
    private const double AxisRatio = 1.45;        // насколько жест должен быть «вбок», а не «вниз»
    private const double HorizontalMin = 56;      // DIP: минимальная длина свайпа вбок
    private const double AxisSlop = 18;           // DIP: после этого определяем направление жеста
    private const double DragSlop = 10;           // DIP: порог начала перетаскивания шторки
    private const double SheetDismissDistance = 96;   // DIP: хватит, чтобы закрыть шторку
    private const double SheetDismissVelocity = 520;  // DIP/с: «бросок» вниз закрывает шторку
    private const double OverlaySwipeDistance = 72;   // DIP: свайп вниз по затемнению

    private enum Mode
    {
        None,
        Horizontal,
        DragSheet,
        SwipeOnOverlay
    }

    private static Mode _mode;
    private static bool _tracking;
    private static double _downX;
    private static double _downY;
    private static double _lastY;
    private static long _lastTime;
    private static double _velocityY;
    private static double _dragFromY;
    private static double _dragDy;

    private static double Density
    {
        get
        {
            try
            {
                double density = Android.App.Application.Context?.Resources?.DisplayMetrics?.Density ?? 0;
                return density > 0 ? density : 1;
            }
            catch
            {
                return 1;
            }
        }
    }

    /// <summary>Вызывается из MainActivity.DispatchTouchEvent для каждого касания.</summary>
    public static void Feed(MotionEvent e)
    {
        IGestureHost? host = GestureBridge.Host;
        if (host is null)
        {
            Reset();
            return;
        }

        // Мультитач (щипок, второе касание) не наш случай — жест отменяем.
        if (e.ActionMasked != MotionEventActions.Down && e.PointerCount > 1)
        {
            Finish(host, canceled: true);
            return;
        }

        double density = Density;
        double x = e.GetX() / density;
        double y = e.GetY() / density;
        long now = e.EventTime;

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _tracking = true;
                _mode = Mode.None;
                _downX = x;
                _downY = y;
                _lastY = y;
                _lastTime = now;
                _velocityY = 0;
                _dragDy = 0;
                break;

            case MotionEventActions.Move:
                if (!_tracking) break;
                TrackMove(host, x, y, now);
                break;

            case MotionEventActions.Up:
                if (_tracking) Finish(host, canceled: false, x, y);
                Reset();
                break;

            case MotionEventActions.Cancel:
                Finish(host, canceled: true);
                Reset();
                break;
        }
    }

    private static void TrackMove(IGestureHost host, double x, double y, long now)
    {
        long dtMs = now - _lastTime;
        if (dtMs > 0) _velocityY = (y - _lastY) / (dtMs / 1000.0);

        _lastY = y;
        _lastTime = now;

        double dx = x - _downX;
        double dy = y - _downY;

        if (_mode == Mode.None)
        {
            bool verticalDown = dy > DragSlop && dy > Math.Abs(dx) * 1.15;

            if (verticalDown && host.IsSheetOpen)
            {
                if (host.IsPointOnSheet(_downX, _downY))
                {
                    // Внутри шторки, но её контент прокручен — это обычная прокрутка.
                    if (host.CanDragSheet())
                    {
                        _mode = Mode.DragSheet;
                        _dragFromY = y;
                        _dragDy = 0;
                        host.OnSheetDragStart();
                    }
                }
                else
                {
                    // Свайп по затемнению вокруг шторки: решение принимаем при отпускании.
                    _mode = Mode.SwipeOnOverlay;
                }
            }
            else if (Math.Abs(dx) > AxisSlop && Math.Abs(dx) > Math.Abs(dy) * AxisRatio)
            {
                _mode = Mode.Horizontal;
            }
        }
        else if (_mode == Mode.DragSheet)
        {
            _dragDy = y - _dragFromY;
            host.OnSheetDrag(_dragDy);
        }
    }

    private static void Finish(IGestureHost host, bool canceled, double x = 0, double y = 0)
    {
        Mode mode = _mode;
        double dx = x - _downX;
        double dy = y - _downY;

        switch (mode)
        {
            case Mode.Horizontal:
                if (!canceled && Math.Abs(dx) >= HorizontalMin && Math.Abs(dx) > Math.Abs(dy) * AxisRatio)
                {
                    host.OnHorizontalSwipe(dx > 0 ? 1 : -1, _downX, _downY);
                }
                break;

            case Mode.DragSheet:
                host.OnSheetDragEnd(_dragDy, canceled ? 0 : _velocityY, canceled);
                break;

            case Mode.SwipeOnOverlay:
                if (!canceled && dy > OverlaySwipeDistance && dy > Math.Abs(dx))
                {
                    host.OnSheetSwipeDown();
                }
                break;
        }
    }

    private static void Reset()
    {
        _tracking = false;
        _mode = Mode.None;
        _velocityY = 0;
        _dragDy = 0;
    }
}

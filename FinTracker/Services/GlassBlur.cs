namespace FinTracker.Services;

/// <summary>
/// Снимок текущего экрана с размытием — фон для «стеклянных» панелей.
/// Реализовано для Android; на остальных платформах возвращает null
/// (там панели просто остаются полупрозрачными).
/// </summary>
public static class GlassBlur
{
    /// <summary>
    /// Делает снимок страницы и отдаёт его как размытую картинку.
    /// </summary>
    /// <param name="page">Страница, которую снимаем (должна быть без открытых оверлеев).</param>
    /// <param name="blurPercent">Сила размытия в процентах (1–100).</param>
    public static ImageSource? Capture(Page page, int blurPercent = 40)
    {
#if ANDROID
        try
        {
            if (page?.Handler?.PlatformView is not Android.Views.View view) return null;

            var width = view.Width;
            var height = view.Height;
            if (width <= 0 || height <= 0) return null;

            var percent = Math.Clamp(blurPercent, 1, 100);

            // Чем больше процент, тем сильнее сжимаем снимок: при растягивании обратно
            // картинка превращается в мягкое размытие нужной силы.
            var downscale = 2 + percent / 8;
            var w = Math.Max(1, width / downscale);
            var h = Math.Max(1, height / downscale);

            using var small = Android.Graphics.Bitmap.CreateBitmap(w, h, Android.Graphics.Bitmap.Config.Argb8888!);
            using (var canvas = new Android.Graphics.Canvas(small))
            {
                canvas.Scale(1f / downscale, 1f / downscale);
                view.Draw(canvas);
            }

            using var stream = new MemoryStream();
            small.Compress(Android.Graphics.Bitmap.CompressFormat.Png, 100, stream);
            var bytes = stream.ToArray();

            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
#else
        _ = page;
        _ = blurPercent;
        return null;
#endif
    }
}

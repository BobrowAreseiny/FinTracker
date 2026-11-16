using Microsoft.Maui.Graphics;
using GFont = Microsoft.Maui.Graphics.Font;

namespace FinTracker.Charts;

/// <summary>Цвета подписей графиков, общие для тёмной и светлой темы.</summary>
public static class ChartTheme
{
    public static Color Label { get; set; } = Color.FromArgb("#9E9E9E");
    public static Color Guide { get; set; } = Color.FromArgb("#12FFFFFF");
    public static Color Surface { get; set; } = Color.FromArgb("#17171A");
    public static Color InactiveBar { get; set; } = Color.FromArgb("#1FFFFFFF");
    public static Color RankingText { get; set; } = Color.FromArgb("#D8D8D8");
    public static Color RankingValue { get; set; } = Color.FromArgb("#F0F0F0");
    public static Color RankingTrack { get; set; } = Color.FromArgb("#1AFFFFFF");
}

/// <summary>Плавная кривая с градиентной заливкой — как SVG Bezier Area в эталоне.</summary>
public sealed class AreaChartDrawable : IDrawable
{
    public IReadOnlyList<float> Values { get; set; } = [];
    public IReadOnlyList<string> Labels { get; set; } = [];
    public Color Stroke { get; set; } = Color.FromArgb("#8B5CF6");

    /// <summary>Цвет подложки карточки — для расчёта затухания заливки без полупрозрачных полос.</summary>
    public Color FillBaseColor { get; set; } = Color.FromArgb("#1E1E20");
    public bool ShowLabels { get; set; } = true;

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Values.Count < 2 || rect.Width <= 0 || rect.Height <= 0) return;

        canvas.Antialias = true;

        float padL = 10, padR = 12, padT = 14;
        float padB = ShowLabels ? 26 : 8;

        float maxV = Math.Max(Values.Max(), 10f);
        float minV = Math.Min(Values.Min(), 0f);
        float range = Math.Max(maxV - minV, 1f);

        float plotW = rect.Width - padL - padR;
        float plotH = rect.Height - padT - padB;

        var pts = new PointF[Values.Count];
        bool flat = Values.Max() - Values.Min() < 0.01f;
        for (int i = 0; i < Values.Count; i++)
        {
            float x = padL + (i / (float)(Values.Count - 1)) * plotW;
            float y = flat
                ? padT + plotH * 0.45f
                : padT + (1f - (Values[i] - minV) / range) * plotH;
            pts[i] = new PointF(x, y);
        }

        var line = BuildSmooth(pts);
        var area = BuildSmooth(pts);
        area.LineTo(pts[^1].X, rect.Height - padB);
        area.LineTo(pts[0].X, rect.Height - padB);
        area.Close();

        // Градиентная заливка: непрозрачные цвета, заранее смешанные с фоном карточки.
        // Так избегаем полос от наложения полупрозрачных прямоугольников.
        canvas.SaveState();
        canvas.ClipPath(area);
        float gradTop = padT;
        float gradBottom = rect.Height - padB;
        const int bands = 128;
        float prevY = gradTop;
        for (int i = 0; i < bands; i++)
        {
            float t = (i + 1) / (float)bands;
            float nextY = gradTop + (gradBottom - gradTop) * t;
            float alpha = 0.42f * (1f - t) * (1f - t);
            canvas.FillColor = BlendWithBase(Stroke, alpha);
            canvas.FillRectangle(0, prevY, rect.Width, nextY - prevY + 0.75f);
            prevY = nextY;
        }
        canvas.RestoreState();

        canvas.StrokeColor = Stroke;
        canvas.StrokeSize = 2.5f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(line);

        if (!ShowLabels || Labels.Count == 0) return;

        canvas.Font = GFont.Default;
        canvas.FontSize = 10;
        canvas.FontColor = ChartTheme.Label;

        for (int i = 0; i < Labels.Count; i++)
        {
            float x = padL + (i / (float)Math.Max(Labels.Count - 1, 1)) * plotW;
            var align = i == 0 ? HorizontalAlignment.Left
                      : i == Labels.Count - 1 ? HorizontalAlignment.Right
                      : HorizontalAlignment.Center;

            float boxW = 42;
            float bx = align == HorizontalAlignment.Left ? x
                     : align == HorizontalAlignment.Right ? x - boxW
                     : x - boxW / 2;

            canvas.DrawString(Labels[i], bx, rect.Height - padB + 4, boxW, 14, align, VerticalAlignment.Center);
        }
    }

    private Color BlendWithBase(Color foreground, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        float r = FillBaseColor.Red + (foreground.Red - FillBaseColor.Red) * alpha;
        float g = FillBaseColor.Green + (foreground.Green - FillBaseColor.Green) * alpha;
        float b = FillBaseColor.Blue + (foreground.Blue - FillBaseColor.Blue) * alpha;
        return Color.FromRgb(r, g, b);
    }

    private static PathF BuildSmooth(PointF[] pts)
    {
        var path = new PathF();
        path.MoveTo(pts[0]);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            float cx = (pts[i].X + pts[i + 1].X) / 2f;
            path.CurveTo(cx, pts[i].Y, cx, pts[i + 1].Y, pts[i + 1].X, pts[i + 1].Y);
        }
        return path;
    }
}

/// <summary>Кольцевая диаграмма с выделением выбранного сектора.</summary>
public sealed class DonutChartDrawable : IDrawable
{
    public IReadOnlyList<(string Name, float Value, Color Color)> Segments { get; set; } = [];
    public int SelectedIndex { get; set; } = -1;

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Segments.Count == 0) return;

        canvas.Antialias = true;

        float size = Math.Min(rect.Width, rect.Height);
        if (size < 24) return;
        float thickness = size * 0.09f;
        float rOut = size / 2f;
        float rIn = rOut - thickness;
        float cx = rect.Center.X;
        float cy = rect.Center.Y;

        float total = Math.Max(Segments.Sum(s => s.Value), 1f);
        float angle = -90f;

        for (int i = 0; i < Segments.Count; i++)
        {
            float frac = Segments[i].Value / total;
            float sweep = frac * 360f;
            float gap = Segments.Count > 1 ? Math.Min(3f, sweep * 0.12f) : 0f;

            float a0 = angle + gap / 2;
            float a1 = angle + sweep - gap / 2;

            float outer = i == SelectedIndex ? rOut : rOut - thickness * 0.15f;
            float inner = i == SelectedIndex ? rIn - thickness * 0.15f : rIn;

            var ring = BuildRing(cx, cy, inner, outer, a0, a1);
            canvas.FillColor = Segments[i].Color;
            canvas.FillPath(ring);

            // Тонкая разделительная линия цветом подложки — видна и в тёмной, и в светлой теме.
            canvas.StrokeColor = ChartTheme.Surface;
            canvas.StrokeSize = 2f;
            canvas.DrawPath(ring);

            angle += sweep;
        }
    }

    /// <summary>
    /// Кольцевой сектор. Дуги строятся ломаной с шагом 2° — так доли и цвета секторов
    /// всегда совпадают, независимо от трактовки углов в бэкенде рисования.
    /// </summary>
    private static PathF BuildRing(float cx, float cy, float rIn, float rOut, float a0, float a1)
    {
        var path = new PathF();

        double sweep = a1 - a0;
        int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / 2.0));

        for (int k = 0; k <= steps; k++)
        {
            double angle = (a0 + sweep * k / steps) * Math.PI / 180.0;
            float x = cx + rOut * (float)Math.Cos(angle);
            float y = cy + rOut * (float)Math.Sin(angle);

            if (k == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        for (int k = steps; k >= 0; k--)
        {
            double angle = (a0 + sweep * k / steps) * Math.PI / 180.0;
            path.LineTo(cx + rIn * (float)Math.Cos(angle), cy + rIn * (float)Math.Sin(angle));
        }

        path.Close();
        return path;
    }
}

/// <summary>Столбчатая диаграмма по дням.</summary>
public sealed class BarsChartDrawable : IDrawable
{
    public IReadOnlyList<(string Label, float Value)> Bars { get; set; } = [];
    public Color Accent { get; set; } = Color.FromArgb("#8B5CF6");

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Bars.Count == 0) return;

        canvas.Antialias = true;
        float padB = 22, padT = 16;
        float maxV = Math.Max(Bars.Max(b => b.Value), 100f);

        float slot = rect.Width / Bars.Count;
        float barW = Math.Min(30f, slot * 0.45f);
        float plotH = rect.Height - padT - padB;

        for (int i = 0; i < Bars.Count; i++)
        {
            bool active = i == Bars.Count - 1;
            float h = Math.Max(Bars[i].Value / maxV * plotH, 8f);
            float x = slot * i + (slot - barW) / 2f;
            float y = rect.Height - padB - h;

            canvas.FillColor = active ? Accent : ChartTheme.InactiveBar;
            canvas.FillRoundedRectangle(x, y, barW, h, Math.Min(7, barW / 2));

            canvas.Font = GFont.Default;
            if (Bars.Count <= 12 || active)
            {
                canvas.FontSize = 9.5f;
                canvas.FontColor = ChartTheme.Label;
                canvas.DrawString(Compact(Bars[i].Value), x - 10, y - 15, barW + 20, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            if (!string.IsNullOrEmpty(Bars[i].Label))
            {
                canvas.FontSize = 10.5f;
                canvas.FontColor = ChartTheme.Label;
                canvas.DrawString(Bars[i].Label, slot * i, rect.Height - padB + 3, slot, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }

    public static string Compact(float v) => v >= 1000 ? (v / 1000f).ToString("0.#") + "k" : ((int)Math.Round(v)).ToString();
}

/// <summary>Стек по дням недели — вклад категорий.</summary>
public sealed class StackChartDrawable : IDrawable
{
    public IReadOnlyList<(string Label, (float Value, Color Color)[] Parts)> Columns { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Columns.Count == 0) return;

        canvas.Antialias = true;
        float padB = 22, padT = 12;
        float slot = rect.Width / Columns.Count;
        float colW = Math.Min(26f, slot * 0.55f);
        float plotH = rect.Height - padT - padB;
        float maxSum = Math.Max(Columns.Max(c => c.Parts.Sum(p => p.Value)), 1f);

        for (int i = 0; i < Columns.Count; i++)
        {
            float x = slot * i + (slot - colW) / 2f;
            float y = rect.Height - padB;

            foreach (var part in Columns[i].Parts)
            {
                float h = part.Value / maxSum * plotH;
                y -= h;
                canvas.FillColor = part.Color;
                canvas.FillRoundedRectangle(x, y, colW, Math.Max(h, 2f), 4);

                canvas.StrokeColor = ChartTheme.Surface;
                canvas.StrokeSize = 1f;
                canvas.DrawRoundedRectangle(x, y, colW, Math.Max(h, 2f), 4);
            }

            canvas.Font = GFont.Default;
            canvas.FontSize = 10.5f;
            canvas.FontColor = ChartTheme.Label;
            canvas.DrawString(Columns[i].Label, slot * i, rect.Height - padB + 3, slot, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}

/// <summary>Вафельная диаграмма: 100 клеток, каждая = 1% категории.</summary>
public sealed class WaffleChartDrawable : IDrawable
{
    public IReadOnlyList<(string Name, float Value, Color Color)> Items { get; set; } = [];
    public int SelectedIndex { get; set; } = -1;
    public Color SelectionColor { get; set; } = Colors.White;

    private const int Columns = 10;
    private const int Rows = 10;
    private const float Gap = 3f;

    private readonly List<int> _cellCategories = [];
    private readonly List<RectF> _cellRects = [];

    public int HitTest(float x, float y)
    {
        for (int i = 0; i < _cellRects.Count; i++)
        {
            if (_cellRects[i].Contains(x, y) && i < _cellCategories.Count)
                return _cellCategories[i];
        }

        return -1;
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.Antialias = true;
        _cellCategories.Clear();
        _cellRects.Clear();

        if (Items.Count == 0)
        {
            DrawEmpty(canvas, rect);
            return;
        }

        double total = Items.Sum(i => (double)Math.Max(0f, i.Value));
        if (total <= 0)
        {
            DrawEmpty(canvas, rect);
            return;
        }

        // Распределяем ровно 100 клеток методом наибольших остатков.
        var exact = Items.Select(i => Math.Max(0f, i.Value) / total * 100.0).ToArray();
        var counts = exact.Select(v => (int)Math.Floor(v)).ToArray();
        int remaining = 100 - counts.Sum();
        var order = Enumerable.Range(0, exact.Length)
            .OrderByDescending(i => exact[i] - counts[i])
            .ToList();

        for (int i = 0; i < remaining; i++)
            counts[order[i % order.Count]]++;

        for (int i = 0; i < counts.Length; i++)
            for (int n = 0; n < counts[i]; n++)
                _cellCategories.Add(i);

        float cellW = (rect.Width - Gap * (Columns - 1)) / Columns;
        float cellH = (rect.Height - Gap * (Rows - 1)) / Rows;
        float cell = Math.Max(1f, Math.Min(cellW, cellH));

        float gridW = cell * Columns + Gap * (Columns - 1);
        float gridH = cell * Rows + Gap * (Rows - 1);
        float ox = rect.X + (rect.Width - gridW) / 2f;
        float oy = rect.Y + (rect.Height - gridH) / 2f;

        for (int idx = 0; idx < Columns * Rows; idx++)
        {
            float x = ox + (idx % Columns) * (cell + Gap);
            float y = oy + (idx / Columns) * (cell + Gap);
            var cellRect = new RectF(x, y, cell, cell);
            _cellRects.Add(cellRect);

            int category = idx < _cellCategories.Count ? _cellCategories[idx] : -1;

            if (category < 0)
            {
                canvas.FillColor = ChartTheme.InactiveBar;
                canvas.FillRoundedRectangle(x, y, cell, cell, 2.5f);
                continue;
            }

            canvas.FillColor = Items[category].Color;
            canvas.FillRoundedRectangle(x, y, cell, cell, 2.5f);

            if (category == SelectedIndex)
            {
                canvas.StrokeColor = SelectionColor;
                canvas.StrokeSize = 2f;
                canvas.DrawRoundedRectangle(x + 1, y + 1, Math.Max(cell - 2, 1), Math.Max(cell - 2, 1), 2.5f);
            }
        }
    }

    private static void DrawEmpty(ICanvas canvas, RectF rect)
    {
        canvas.Font = GFont.Default;
        canvas.FontSize = 12;
        canvas.FontColor = ChartTheme.Label;
        canvas.DrawString("Нет данных за период", rect.X, rect.Y, rect.Width, rect.Height,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}

/// <summary>Карта трат: площадь плитки пропорциональна сумме категории.</summary>
public sealed class TreemapChartDrawable : IDrawable
{
    public IReadOnlyList<(string Name, float Value, Color Color)> Items { get; set; } = [];
    public int SelectedIndex { get; set; } = -1;
    public Color SelectionColor { get; set; } = Colors.White;

    private readonly List<(int Index, RectF Rect)> _tiles = [];
    private int _tileIndex;

    public int HitTest(float x, float y)
    {
        foreach (var tile in _tiles)
        {
            if (tile.Rect.Contains(x, y)) return tile.Index;
        }

        return -1;
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.Antialias = true;
        _tiles.Clear();
        _tileIndex = 0;

        var items = Items
            .Where(i => i.Value > 0)
            .OrderByDescending(i => i.Value)
            .ToList();

        if (items.Count == 0)
        {
            DrawEmpty(canvas, rect);
            return;
        }

        Layout(canvas, items, new RectF(rect.X + 2, rect.Y + 2, Math.Max(rect.Width - 4, 1), Math.Max(rect.Height - 4, 1)));
    }

    private void Layout(ICanvas canvas, List<(string Name, float Value, Color Color)> items, RectF rect)
    {
        if (items.Count == 0 || rect.Width < 2 || rect.Height < 2) return;

        if (items.Count == 1)
        {
            DrawTile(canvas, items[0], rect);
            return;
        }

        float total = items.Sum(i => i.Value);
        if (total <= 0) return;

        float half = total / 2f;
        float acc = 0;
        int split = 1;
        for (int i = 0; i < items.Count; i++)
        {
            acc += items[i].Value;
            if (acc >= half)
            {
                split = i + 1;
                break;
            }
        }
        split = Math.Clamp(split, 1, items.Count - 1);

        var first = items.Take(split).ToList();
        var second = items.Skip(split).ToList();
        float ratio = Math.Clamp(first.Sum(i => i.Value) / total, 0.06f, 0.94f);
        const float gap = 2f;

        if (rect.Width >= rect.Height)
        {
            float w = rect.Width * ratio;
            Layout(canvas, first, new RectF(rect.X, rect.Y, Math.Max(w - gap, 1), rect.Height));
            Layout(canvas, second, new RectF(rect.X + w + gap / 2, rect.Y, Math.Max(rect.Width - w - gap, 1), rect.Height));
        }
        else
        {
            float h = rect.Height * ratio;
            Layout(canvas, first, new RectF(rect.X, rect.Y, rect.Width, Math.Max(h - gap, 1)));
            Layout(canvas, second, new RectF(rect.X, rect.Y + h + gap / 2, rect.Width, Math.Max(rect.Height - h - gap, 1)));
        }
    }

    private void DrawTile(ICanvas canvas, (string Name, float Value, Color Color) item, RectF rect)
    {
        if (rect.Width < 2 || rect.Height < 2) return;

        int index = _tileIndex++;
        _tiles.Add((index, rect));

        canvas.FillColor = item.Color;
        canvas.FillRoundedRectangle(rect.X, rect.Y, rect.Width, rect.Height, 5);

        if (index == SelectedIndex)
        {
            canvas.StrokeColor = SelectionColor;
            canvas.StrokeSize = 2.5f;
            canvas.DrawRoundedRectangle(rect.X + 1, rect.Y + 1, Math.Max(rect.Width - 2, 1), Math.Max(rect.Height - 2, 1), 5);
        }
    }

    private static void DrawEmpty(ICanvas canvas, RectF rect)
    {
        canvas.Font = GFont.Default;
        canvas.FontSize = 12;
        canvas.FontColor = ChartTheme.Label;
        canvas.DrawString("Нет данных за период", rect.X, rect.Y, rect.Width, rect.Height,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

}

/// <summary>Рейтинг категорий: горизонтальные полосы от большей суммы к меньшей.</summary>
public sealed class RankingBarsDrawable : IDrawable
{
    public IReadOnlyList<(string Name, float Value, Color Color)> Items { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.Antialias = true;

        if (Items.Count == 0)
        {
            canvas.Font = GFont.Default;
            canvas.FontSize = 12;
            canvas.FontColor = ChartTheme.Label;
            canvas.DrawString("Нет данных за период", rect.X, rect.Y, rect.Width, rect.Height,
                HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        float max = Math.Max(Items.Max(i => i.Value), 1f);
        float rowH = rect.Height / Items.Count;
        float labelW = Math.Min(rect.Width * 0.36f, 120f);
        float valueW = 62f;
        float barX = rect.X + labelW + 8;
        float barW = Math.Max(rect.Width - labelW - valueW - 14, 20);

        for (int i = 0; i < Items.Count; i++)
        {
            float y = rect.Y + i * rowH;
            float centerY = y + rowH / 2f;

            canvas.Font = GFont.Default;
            canvas.FontSize = 10.5f;
            canvas.FontColor = ChartTheme.RankingText;
            canvas.DrawString(Trim(Items[i].Name, 15), rect.X, y, labelW, rowH,
                HorizontalAlignment.Left, VerticalAlignment.Center);

            canvas.FillColor = ChartTheme.RankingTrack;
            canvas.FillRoundedRectangle(barX, centerY - 6, barW, 12, 6);

            float w = Math.Max(Items[i].Value / max * barW, 4);
            canvas.FillColor = Items[i].Color;
            canvas.FillRoundedRectangle(barX, centerY - 6, w, 12, 6);

            canvas.FontSize = 10.5f;
            canvas.FontColor = ChartTheme.RankingValue;
            canvas.DrawString(BarsChartDrawable.Compact(Items[i].Value), rect.X + rect.Width - valueW, y, valueW, rowH,
                HorizontalAlignment.Right, VerticalAlignment.Center);
        }
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

/// <summary>Диаграмма потоков Источники → Бюджет → Категории.</summary>
public sealed class SankeyChartDrawable : IDrawable
{
    public string LeftTop { get; set; } = "Зарплата";
    public string LeftBottom { get; set; } = "Фриланс";
    public string BudgetValue { get; set; } = "0";
    public (string Name, Color Color)[] Right { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.Antialias = true;
        float s = Math.Min(rect.Width / 340f, rect.Height / 180f);
        float ox = (rect.Width - 340f * s) / 2f;
        float oy = (rect.Height - 180f * s) / 2f;

        void Flow(float x1, float y1, float x2, float y2, Color color, float width)
        {
            var path = new PathF();
            path.MoveTo(ox + x1 * s, oy + y1 * s);
            path.CurveTo(ox + (x1 + (x2 - x1) * 0.55f) * s, oy + y1 * s,
                         ox + (x1 + (x2 - x1) * 0.45f) * s, oy + y2 * s,
                         ox + x2 * s, oy + y2 * s);
            canvas.StrokeColor = color.WithAlpha(0.30f);
            canvas.StrokeSize = width * s;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
        }

        Flow(80, 40, 145, 82, Color.FromArgb("#4ADE80"), 22);
        Flow(80, 130, 145, 102, Color.FromArgb("#34D399"), 10);

        var right = Right.Length >= 3 ? Right : [("Продукты", Color.FromArgb("#EC4899")), ("Жилье", Color.FromArgb("#8B5CF6")), ("Транспорт", Color.FromArgb("#06B6D4"))];
        Flow(195, 74, 260, 30, right[0].Color, 16);
        Flow(195, 90, 260, 90, right[1].Color, 14);
        Flow(195, 106, 260, 148, right[2].Color, 10);

        void Node(float x, float y, float w, float h, Color fill, string text, Color textColor, float fontSize, float radius)
        {
            canvas.FillColor = fill;
            canvas.FillRoundedRectangle(ox + x * s, oy + y * s, w * s, h * s, radius * s);
            canvas.Font = GFont.Default;
            canvas.FontSize = fontSize;
            canvas.FontColor = textColor;
            canvas.DrawString(text, ox + x * s, oy + y * s, w * s, h * s, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        Node(8, 22, 72, 34, Color.FromArgb("#4ADE80"), LeftTop, Color.FromArgb("#081C15"), 10.5f, 8);
        Node(8, 114, 72, 30, Color.FromArgb("#10B981"), LeftBottom, Color.FromArgb("#081C15"), 10.5f, 8);
        Node(145, 60, 50, 60, Color.FromArgb("#8B5CF6"), "Бюджет", Colors.White, 10f, 10);

        canvas.Font = GFont.Default;
        canvas.FontSize = 9;
        canvas.FontColor = Color.FromArgb("#E0E7FF");
        canvas.DrawString(BudgetValue, ox + 145 * s, oy + 96 * s, 50 * s, 18 * s, HorizontalAlignment.Center, VerticalAlignment.Center);

        Node(260, 14, 72, 30, right[0].Color, right[0].Name, Colors.White, 10f, 8);
        Node(260, 75, 72, 30, right[1].Color, right[1].Name, Colors.White, 10f, 8);
        Node(260, 134, 72, 30, right[2].Color, right[2].Name, Colors.White, 10f, 8);
    }
}

/// <summary>Каскад: старт → доходы → траты → итог.</summary>
public sealed class WaterfallChartDrawable : IDrawable
{
    public (string Label, string Value, float Top, float Height, Color Color)[] Steps { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Steps.Length == 0) return;

        canvas.Antialias = true;
        float s = Math.Min(rect.Width / 340f, rect.Height / 175f);
        float ox = (rect.Width - 340f * s) / 2f;
        float oy = (rect.Height - 175f * s) / 2f;

        for (int i = 0; i < Steps.Length; i++)
        {
            float x = 16 + i * 65;
            var step = Steps[i];

            canvas.FillColor = step.Color;
            canvas.FillRoundedRectangle(ox + x * s, oy + step.Top * s, 40 * s, Math.Max(step.Height, 3) * s, 6 * s);

            canvas.Font = GFont.Default;
            canvas.FontSize = 9.5f;
            canvas.FontColor = ChartTheme.RankingValue;
            canvas.DrawString(step.Value, ox + (x - 8) * s, oy + (step.Top - 16) * s, 56 * s, 14 * s, HorizontalAlignment.Center, VerticalAlignment.Center);

            canvas.Font = GFont.Default;
            canvas.FontSize = 10.5f;
            canvas.FontColor = ChartTheme.Label;
            canvas.DrawString(step.Label, ox + x * s, oy + 156 * s, 40 * s, 16 * s, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}

/// <summary>Радар факта против плана по топ-категориям.</summary>
public sealed class RadarChartDrawable : IDrawable
{
    public IReadOnlyList<string> Axes { get; set; } = [];
    public float[] Plan { get; set; } = [];
    public float[] Fact { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        if (Axes.Count < 3) return;

        canvas.Antialias = true;
        float s = Math.Min(rect.Width / 340f, rect.Height / 190f);
        float ox = (rect.Width - 340f * s) / 2f;
        float oy = (rect.Height - 190f * s) / 2f;

        float cx = 170, cy = 96, rMax = 66;

        PointF P(float ratio, int i)
        {
            double angle = -Math.PI / 2 + i * 2 * Math.PI / Axes.Count;
            return new PointF(
                ox + (float)(cx + Math.Cos(angle) * rMax * ratio) * s,
                oy + (float)(cy + Math.Sin(angle) * rMax * ratio) * s);
        }

        foreach (float level in new[] { 0.4f, 0.7f, 1f })
        {
            var web = new PathF();
            for (int i = 0; i < Axes.Count; i++)
            {
                var p = P(level, i);
                if (i == 0) web.MoveTo(p); else web.LineTo(p);
            }
            web.Close();
            canvas.StrokeColor = ChartTheme.Guide;
            canvas.StrokeSize = 1;
            canvas.DrawPath(web);
        }

        void Shape(float[] ratios, Color fill, Color stroke, float width)
        {
            var path = new PathF();
            for (int i = 0; i < Axes.Count; i++)
            {
                var p = P(Math.Clamp(ratios.Length > i ? ratios[i] : 0.5f, 0.05f, 1f), i);
                if (i == 0) path.MoveTo(p); else path.LineTo(p);
            }
            path.Close();
            canvas.FillColor = fill;
            canvas.FillPath(path);
            canvas.StrokeColor = stroke;
            canvas.StrokeSize = width;
            canvas.DrawPath(path);
        }

        if (Plan.Length >= Axes.Count) Shape(Plan, Color.FromArgb("#293B82F6"), Color.FromArgb("#3B82F6"), 1.8f);
        if (Fact.Length >= Axes.Count) Shape(Fact, Color.FromArgb("#528B5CF6"), Color.FromArgb("#8B5CF6"), 2.2f);

        canvas.Font = GFont.Default;
        canvas.FontSize = 10;
        canvas.FontColor = ChartTheme.Label;

        for (int i = 0; i < Axes.Count; i++)
        {
            double angle = -Math.PI / 2 + i * 2 * Math.PI / Axes.Count;
            float tx = ox + (float)(cx + Math.Cos(angle) * (rMax + 26)) * s;
            float ty = oy + (float)(cy + Math.Sin(angle) * (rMax + 16)) * s;
            canvas.DrawString(Axes[i], tx - 34, ty - 8, 68, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}

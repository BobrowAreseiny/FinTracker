using FinTracker.Models;

namespace FinTracker.Services;

/// <summary>
/// Каталог категорий и цветовых пресетов Soft Matte Pro.
/// Категории пользователя сохраняются в настройках (JSON), потому что таблица операций
/// хранит только имя категории — так правка стиля не затрагивает финансовые записи.
/// </summary>
public static class CategoryCatalog
{
    private const string PrefCategories = "FT_Categories_v2";

    public static readonly ColorPreset[] Presets =
    [
        new() { Id = "violet", Name = "Фиолетовый", BoxBg = "#1E1B36", BoxBorder = "#2D2852", IconColor = "#C4B5FD", ChartColor = "#8b5cf6" },
        new() { Id = "pink", Name = "Розовый", BoxBg = "#25173B", BoxBorder = "#3B1F5E", IconColor = "#E1BEE7", ChartColor = "#ec4899" },
        new() { Id = "amber", Name = "Янтарный", BoxBg = "#2E1E1A", BoxBorder = "#3E2723", IconColor = "#FDE68A", ChartColor = "#f59e0b" },
        new() { Id = "cyan", Name = "Голубой", BoxBg = "#142834", BoxBorder = "#1E3A4C", IconColor = "#A5F3FC", ChartColor = "#06b6d4" },
        new() { Id = "green", Name = "Изумрудный", BoxBg = "#102B26", BoxBorder = "#173F38", IconColor = "#A7F3D0", ChartColor = "#4ade80" },
        new() { Id = "blue", Name = "Синий", BoxBg = "#1A2636", BoxBorder = "#233852", IconColor = "#93C5FD", ChartColor = "#3b82f6" },
        new() { Id = "coral", Name = "Коралловый", BoxBg = "#361717", BoxBorder = "#4F2121", IconColor = "#FECACA", ChartColor = "#f87171" },
        new() { Id = "fuchsia", Name = "Пурпурный", BoxBg = "#2D192B", BoxBorder = "#472343", IconColor = "#F5D0FE", ChartColor = "#d946ef" }
    ];

    public static readonly string[] IconLibrary =
    [
        "coffee", "shopping-bag", "car", "home", "film",
        "shirt", "briefcase", "zap", "gift", "percent",
        "trending-up", "heart", "plane", "smartphone", "gamepad-2",
        "music", "book-open", "dumbbell", "paw-print", "utensils"
    ];

    public static List<CategoryDef> DefaultExpense() =>
    [
        new() { Id = "exp_1", Cat = "Кафе и рестораны", Icon = "coffee", BoxBg = "#2E1E1A", BoxBorder = "#3E2723", IconColor = "#D7CCC8", ChartColor = "#f59e0b" },
        new() { Id = "exp_2", Cat = "Продукты", Icon = "shopping-bag", BoxBg = "#25173B", BoxBorder = "#3B1F5E", IconColor = "#E1BEE7", ChartColor = "#ec4899" },
        new() { Id = "exp_3", Cat = "Транспорт", Icon = "car", BoxBg = "#361717", BoxBorder = "#4F2121", IconColor = "#FFCDD2", ChartColor = "#06b6d4" },
        new() { Id = "exp_4", Cat = "Жилье и связь", Icon = "home", BoxBg = "#1E1B36", BoxBorder = "#2D2852", IconColor = "#C4B5FD", ChartColor = "#8b5cf6" },
        new() { Id = "exp_5", Cat = "Досуг", Icon = "film", BoxBg = "#1A2636", BoxBorder = "#233852", IconColor = "#93C5FD", ChartColor = "#3b82f6" },
        new() { Id = "exp_6", Cat = "Покупки", Icon = "shirt", BoxBg = "#2D192B", BoxBorder = "#472343", IconColor = "#F5D0FE", ChartColor = "#d946ef" }
    ];

    public static List<CategoryDef> DefaultIncome() =>
    [
        new() { Id = "inc_1", Cat = "Зарплата", Icon = "briefcase", BoxBg = "#102B26", BoxBorder = "#173F38", IconColor = "#B2DFDB", ChartColor = "#3b82f6" },
        new() { Id = "inc_2", Cat = "Фриланс", Icon = "zap", BoxBg = "#102B26", BoxBorder = "#173F38", IconColor = "#B2DFDB", ChartColor = "#4ade80" },
        new() { Id = "inc_3", Cat = "Подарок", Icon = "gift", BoxBg = "#2A2415", BoxBorder = "#42371E", IconColor = "#FDE68A", ChartColor = "#fbbf24" },
        new() { Id = "inc_4", Cat = "Кэшбэк", Icon = "percent", BoxBg = "#1E1B36", BoxBorder = "#2D2852", IconColor = "#C4B5FD", ChartColor = "#8b5cf6" },
        new() { Id = "inc_5", Cat = "Инвестиции", Icon = "trending-up", BoxBg = "#102B26", BoxBorder = "#173F38", IconColor = "#B2DFDB", ChartColor = "#10b981" },
        new() { Id = "inc_6", Cat = "Возврат", Icon = "rotate-ccw", BoxBg = "#1A2636", BoxBorder = "#233852", IconColor = "#93C5FD", ChartColor = "#06b6d4" }
    ];

    /// <summary>Стиль для служебных категорий долгов и переводов.</summary>
    public static CategoryDef? Special(string name)
    {
        return name switch
        {
            "Дал в долг" => new CategoryDef { Cat = name, Icon = "arrow-up-right", BoxBg = "#361717", BoxBorder = "#4F2121", IconColor = "#FECACA", ChartColor = "#f87171" },
            "Взял в долг" => new CategoryDef { Cat = name, Icon = "arrow-down-left", BoxBg = "#102B26", BoxBorder = "#173F38", IconColor = "#A7F3D0", ChartColor = "#4ade80" },
            "Возврат долга" => new CategoryDef { Cat = name, Icon = "check-circle-2", BoxBg = "#102B26", BoxBorder = "#173F38", IconColor = "#B2DFDB", ChartColor = "#4ade80" },
            "Погашение долга" => new CategoryDef { Cat = name, Icon = "check-circle-2", BoxBg = "#361717", BoxBorder = "#4F2121", IconColor = "#FFCDD2", ChartColor = "#f87171" },
            _ => null
        };
    }

    /// <summary>Загружает категории пользователя (или значения по умолчанию).</summary>
    public static (List<CategoryDef> Expense, List<CategoryDef> Income) Load()
    {
        try
        {
            string json = Preferences.Default.Get(PrefCategories, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var saved = System.Text.Json.JsonSerializer.Deserialize<SavedCategories>(json);
                if (saved?.Expense is { Count: > 0 } && saved.Income is { Count: > 0 })
                    return (saved.Expense, saved.Income);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "CategoryCatalog.Load");
        }

        return (DefaultExpense(), DefaultIncome());
    }

    public static void Save(IEnumerable<CategoryDef> expense, IEnumerable<CategoryDef> income)
    {
        try
        {
            var payload = new SavedCategories { Expense = expense.ToList(), Income = income.ToList() };
            Preferences.Default.Set(PrefCategories, System.Text.Json.JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "CategoryCatalog.Save");
        }
    }

    public static void ResetToDefaults()
    {
        try
        {
            Preferences.Default.Remove(PrefCategories);
        }
        catch { }
    }

    private sealed class SavedCategories
    {
        public List<CategoryDef> Expense { get; set; } = [];
        public List<CategoryDef> Income { get; set; } = [];
    }
}

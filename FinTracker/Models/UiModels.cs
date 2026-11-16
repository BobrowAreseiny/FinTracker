namespace FinTracker.Models;

/// <summary>Категория с визуальным оформлением, как в эталонном макете.</summary>
public sealed class CategoryDef
{
    public string Id { get; set; } = string.Empty;
    public string Cat { get; set; } = string.Empty;
    public string Icon { get; set; } = "circle-off";
    public string BoxBg { get; set; } = "#1E1B36";
    public string BoxBorder { get; set; } = "#2D2852";
    public string IconColor { get; set; } = "#C4B5FD";
    public string ChartColor { get; set; } = "#8b5cf6";

    public CategoryDef Clone() => (CategoryDef)MemberwiseClone();
}

/// <summary>Цветовой пресет конструктора категорий.</summary>
public sealed class ColorPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BoxBg { get; set; } = string.Empty;
    public string BoxBorder { get; set; } = string.Empty;
    public string IconColor { get; set; } = string.Empty;
    public string ChartColor { get; set; } = string.Empty;
}

/// <summary>Долг или займ. Хранится отдельно от операций, но влияет на баланс кошелька.</summary>
public sealed class DebtDef
{
    public long Id { get; set; }
    public string Person { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Due { get; set; } = "без срока";
    public string Type { get; set; } = "owed_to_me"; // owed_to_me | i_owe
    public string Acc { get; set; } = "main";        // main | cash
}

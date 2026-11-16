using SQLite;

namespace FinTracker.Models;

public class Transaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BYN";
    public string ShopName { get; set; } = string.Empty;
    public string Category { get; set; } = "Супермаркеты";
    public string Type { get; set; } = "expense"; // "expense" | "income" | "transfer"
    public string AccountId { get; set; } = "acc-1";
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>
    /// Источник операции: "manual" (введено вручную), "push" (снято из уведомления банка),
    /// "demo" (демонстрационные данные), "goal" (пополнение копилки).
    /// Используется для защиты от дублей: дедупликация применяется только к "push".
    /// </summary>
    public string Source { get; set; } = "manual";

    /// <summary>
    /// Идентификатор цели, если операция является пополнением копилки.
    /// Позволяет пересчитывать прогресс цели по базе — удаление операции сразу уменьшает накопленное.
    /// </summary>
    public string? GoalId { get; set; }
}

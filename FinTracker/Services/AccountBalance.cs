using FinTracker.Models;

namespace FinTracker.Services;

/// <summary>
/// Расчёт остатков по счетам и целям. Вынесен отдельно от страницы,
/// чтобы правила можно было проверить независимо от UI.
/// </summary>
public static class AccountBalance
{
    /// <summary>Перевод между своими счетами (в резерв / в цель) — не доход и не расход.</summary>
    public static bool IsTransferType(string? type)
    {
        return type is "reserve_transfer" or "goal_deposit";
    }

    /// <summary>
    /// Остаток по счёту с учётом всех операций.
    /// Переводы в резерв уменьшают «Основной счёт» и увеличивают «Резерв».
    /// </summary>
    public static decimal Calculate(string accountId, IEnumerable<Transaction> all)
    {
        decimal bal = 0m;
        foreach (var tx in all)
        {
            string txAcc = string.IsNullOrEmpty(tx.AccountId) ? "acc-1" : tx.AccountId;
            string type = string.IsNullOrEmpty(tx.Type) ? "expense" : tx.Type;

            if (IsTransferType(type))
            {
                if (accountId == "acc-1") bal -= tx.Amount;
                else if (accountId == "acc-2") bal += tx.Amount;
                continue;
            }

            if (txAcc != accountId) continue;

            if (type == "income" || type == "loan_borrowed") bal += tx.Amount;
            else if (type == "expense" || type == "loan_lent") bal -= tx.Amount;
        }
        return bal;
    }

    /// <summary>Суммарный остаток по нескольким счетам.</summary>
    public static decimal CalculateTotal(IEnumerable<string> accountIds, IEnumerable<Transaction> all)
    {
        var list = all as IList<Transaction> ?? all.ToList();
        return accountIds.Sum(id => Calculate(id, list));
    }

    /// <summary>Доходы за период (тип income).</summary>
    public static decimal Income(IEnumerable<Transaction> all)
    {
        return all.Where(t => t.Type == "income").Sum(t => t.Amount);
    }

    /// <summary>Расходы за период (тип expense, в т.ч. у записей без явного типа).</summary>
    public static decimal Expense(IEnumerable<Transaction> all)
    {
        return all.Where(t => string.IsNullOrEmpty(t.Type) || t.Type == "expense").Sum(t => t.Amount);
    }

    /// <summary>Накопленная сумма по цели — считается по операциям пополнения копилки.</summary>
    public static decimal GoalSaved(string goalId, IEnumerable<Transaction> all)
    {
        return all.Where(t => t.Type == "goal_deposit" && t.GoalId == goalId).Sum(t => t.Amount);
    }
}

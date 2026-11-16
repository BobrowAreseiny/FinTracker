using Microsoft.Maui.Devices;

namespace FinTracker.Services;

/// <summary>
/// Обёртка над HapticFeedback из MAUI. Даёт короткую тактильную отдачу при нажатии
/// на кнопки и при вводе суммы с цифровой клавиатуры.
/// На платформах без поддержки вибрации вызов ничего не делает и не падает.
/// </summary>
public static class Haptics
{
    /// <summary>Короткая вибрация "клик" — основной отклик на нажатие.</summary>
    public static void Click() => Perform(HapticFeedbackType.Click);

    /// <summary>Более заметная вибрация для важных действий (сохранение, подтверждение).</summary>
    public static void Success() => Perform(HapticFeedbackType.LongPress);

    /// <summary>Вибрация-предупреждение (например, когда сумма не введена).</summary>
    public static void Warning() => Perform(HapticFeedbackType.LongPress);

    private static void Perform(HapticFeedbackType type)
    {
        try
        {
            HapticFeedback.Default?.Perform(type);
        }
        catch
        {
            // Вибрация — второстепенный эффект, никогда не роняем из-за неё приложение.
        }
    }
}

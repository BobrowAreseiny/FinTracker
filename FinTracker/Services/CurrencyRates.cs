using System.Text.Json;

namespace FinTracker.Services;

/// <summary>
/// Курсы валют с обновлением из API Национального банка РБ.
/// Внутри приложение всегда работает в BYN, — здесь только «сколько единиц валюты в 1 BYN».
/// Если интернета нет — используются последние сохранённые, а в крайнем случае — зашитые.
/// </summary>
public static class CurrencyRates
{
    /// <summary>Единый источник правды для UI и парсера. Массив меняется на месте при обновлении.</summary>
    public static readonly (string Code, string Symbol, decimal Rate)[] All =
    [
        ("BYN", "Br", 1.0m),
        ("RUB", "₽", 28.5m),
        ("USD", "$", 0.31m),
        ("EUR", "€", 0.28m)
    ];

    private const string PrefRates = "FT_Rates_v1";
    private const string PrefUpdated = "FT_Rates_Updated";

    private const string NbrbUrl = "https://api.nbrb.by/exrates/rates?periodicity=0";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static DateTime? LastUpdated
    {
        get
        {
            long ticks = Preferences.Default.Get(PrefUpdated, 0L);
            return ticks <= 0 ? null : new DateTime(ticks, DateTimeKind.Local);
        }
    }

    public static int IndexOf(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return 0;

        string c = code.Trim().ToUpperInvariant();
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Code == c) return i;
        }
        return 0;
    }

    public static decimal RateOf(string? code) => All[IndexOf(code)].Rate;

    /// <summary>Переводит сумму в валюте уведомления в базовую (BYN).</summary>
    public static decimal ToByn(decimal amount, string? code)
    {
        decimal rate = RateOf(code);
        if (rate <= 0m) return amount;

        return Math.Round(amount / rate, 2);
    }

    /// <summary>Переводит сумму из базовой (BYN) в выбранную валюту.</summary>
    public static decimal FromByn(decimal baseByn, int currencyIndex)
    {
        decimal rate = All[Math.Clamp(currencyIndex, 0, All.Length - 1)].Rate;
        return Math.Round(baseByn * rate, 2);
    }

    /// <summary>Поднимает сохранённые курсы без обращения к сети. Вызывать при старте.</summary>
    public static void LoadCached()
    {
        try
        {
            string json = Preferences.Default.Get(PrefRates, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;

            var saved = JsonSerializer.Deserialize<List<SavedRate>>(json);
            if (saved is null) return;

            ApplyRates(saved.Select(s => (s.Code, s.Rate)));
        }
        catch { }
    }

    /// <summary>
    /// Обновляет курсы с сервера Нацбанка (нужен интернет).
    /// Возвращает true, если курсы действительно обновились.
    /// </summary>
    public static async Task<bool> RefreshAsync()
    {
        try
        {
            string json = await Http.GetStringAsync(NbrbUrl).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;

            var parsed = new List<(string Code, decimal Rate)>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("Cur_Abbreviation", out var codeEl)) continue;
                if (!item.TryGetProperty("Cur_OfficialRate", out var rateEl)) continue;

                string code = codeEl.GetString() ?? string.Empty;
                if (!decimal.TryParse(rateEl.ToString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal official)) continue;
                if (official <= 0) continue;

                int scale = item.TryGetProperty("Cur_Scale", out var scaleEl) &&
                            int.TryParse(scaleEl.ToString(), out int s) && s > 0
                    ? s
                    : 1;

                // НБРБ даёт «сколько BYN за Cur_Scale единиц», нам нужно обратное
                decimal perByn = Math.Round(scale / official, 4);
                parsed.Add((code, perByn));
            }

            if (parsed.Count == 0) return false;

            ApplyRates(parsed);

            var toSave = All.Select(a => new SavedRate { Code = a.Code, Rate = a.Rate }).ToList();
            Preferences.Default.Set(PrefRates, JsonSerializer.Serialize(toSave));
            Preferences.Default.Set(PrefUpdated, DateTime.Now.Ticks);

            return true;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "CurrencyRates.RefreshAsync");
            return false;
        }
    }

    private static void ApplyRates(IEnumerable<(string Code, decimal Rate)> rates)
    {
        var list = rates.ToList();

        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Code == "BYN") continue;

            var found = list.FirstOrDefault(r =>
                string.Equals(r.Code, All[i].Code, StringComparison.OrdinalIgnoreCase));

            if (found.Rate > 0)
                All[i] = (All[i].Code, All[i].Symbol, found.Rate);
        }
    }

    private sealed class SavedRate
    {
        public string Code { get; set; } = string.Empty;
        public decimal Rate { get; set; }
    }
}

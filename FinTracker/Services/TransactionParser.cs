using System.Globalization;
using System.Text.RegularExpressions;
using FinTracker.Models;

namespace FinTracker.Services;

/// <summary>
/// Разбор текста уведомлений Samsung Pay / Wallet, СМС от банков и сервисов оплаты
/// (в т.ч. транслитом) в операцию.
///
/// Поддерживаемые форматы (по реальным примерам):
///  1. Samsung Wallet:        SHOP "SOSEDI" 10,18 BYN
///                            GIPERMARKET "GREEN" | 89,64 BYN | 02.11.2026 15:45:38
///  2. bePaid (СМС):          Summa: 47,44 BYN ... Adres: ... Podrobno: https://pzz.by/...
///  3. СМС банка с кодом:     Vash kod 788278 dlya operacii: karta 4*8758, summa 140.00 BYN
///  4. Обычные банковские:    Оплата 12,50 BYN, место: EVROOPT; Доступно: 500 BYN
/// </summary>
public static class TransactionParser
{
    // ---------------------------------------------------------------------
    // Сумма и валюта
    // ---------------------------------------------------------------------

    // "1 250.00", "1250,50", "12", "0.99" — с пробелами-разделителями тысяч
    private const string NumberPattern =
        @"(?:\d{1,3}(?:[ \u00A0\u202F]\d{3})+(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)";

    private const string CurrencyPattern =
        @"(?:BYN|BYR|USD|EUR|RUB|GBP|PLN|UAH|Br|руб|р)(?!\w)";

    private static readonly Regex AmountCurrencyRegex = new(
        $@"(?<amount>{NumberPattern})\s*(?<currency>{CurrencyPattern})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Метка, после которой идёт именно сумма операции ("Summa:", "Сумма:", "amount").</summary>
    private static readonly Regex SumLabelRegex = new(
        @"(?i)(summa|сумма|amount|itogo|итого|k\s*oplate|к\s*оплате|prinyat|принят|spisano|списано|oplata|оплата)\s*[:\-–—=]*\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BalanceKeywordRegex = new(
        @"(?i)(доступн|остат|баланс|available|balance|ostatok|dostupno)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ---------------------------------------------------------------------
    // Направление операции
    // ---------------------------------------------------------------------

    private static readonly Regex ExpenseKeywordRegex = new(
        @"(?i)(оплат|оплачен|списан|спиш|покупк|платеж|платёж|сняти|purchase|payment|paid|дебет|debit|oplat|spisan|pokupk|prinyat|принят)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IncomeKeywordRegex = new(
        @"(?i)(зачислен|зачисл|пополнен|пополн|поступлен|поступл|возврат|refund|начислен|получен|credit|zachislen|postuplen|popoln|vozvrat)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IncomingTransferRegex = new(
        @"(?i)(перевод[^.|]{0,40}?\bот\b|perevod[^.|]{0,40}?\bot\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ---------------------------------------------------------------------
    // Магазин
    // ---------------------------------------------------------------------

    /// <summary>Samsung Wallet/Pay: SHOP "SOSEDI" или SHOP EVROOPT.</summary>
    private static readonly Regex ShopKeywordRegex = new(
        @"(?i)\bSHOP\b\s*[«""']?(?<shop>[^«»""'\|]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Префикс + бренд в кавычках: GIPERMARKET "GREEN", RBO "BURGER KING".</summary>
    private static readonly Regex QuotedBrandRegex = new(
        @"(?i)(?<prefix>[A-Za-zА-Яа-яЁё0-9\.\-]{2,20})\s*[«""'](?<brand>[^«»""']{2,40})[»""']",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Явные метки магазина.</summary>
    private static readonly Regex MerchantLabelRegex = new(
        @"(?i)\b(merchant|magazin|магазин|mesto|место|place|torgovaya\s*tochka|торговая\s*точка)\b\s*[:\-]?\s*(?<value>[^\|]{2,60})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Адрес (bePaid и некоторые банки).</summary>
    private static readonly Regex AddressLabelRegex = new(
        @"(?i)\b(adres|адрес)\b\s*[:\-]?\s*(?<value>[^\|]{2,60})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Маска карты: karta 4*8758, карта *1234, картой 4*8758.</summary>
    private static readonly Regex CardMaskRegex = new(
        @"(?i)\b(?:kart[aoы]?|карт[аоы]?)\s*(?<mask>\d{1,4}\s*\*+\s*\d{2,6}|\*+\d{2,6}|\d{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UrlRegex = new(
        @"(?i)https?://(?<host>[^/\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Домены, которые не являются названием магазина
    private static readonly string[] BankLikeHosts =
    [
        "bank", "erip", "bepaid", "be-paid", "platby", "pay.by", "priorbank",
        "alfabank", "mtbank", "vtb", "sber", "belinvest", "dabrabyt", "bss"
    ];

    // Слова, которые не могут быть названием магазина
    private static readonly string[] ShopNoiseWords =
    [
        "оплата", "оплачено", "покупка", "платеж", "платёж", "списание", "операция", "операции",
        "карта", "картой", "карту", "счет", "счёт", "баланс", "доступно", "остаток",
        "payment", "purchase", "paid", "card", "summa", "сумма", "amount", "prinyat", "принят",
        "vash", "ваш", "kod", "код", "nikomu", "никому", "dlya", "для", "operacii", "bePaid".ToLowerInvariant()
    ];

    // ---------------------------------------------------------------------
    // Публичный API
    // ---------------------------------------------------------------------

    /// <summary>Разбирает текст уведомления/СМС. Возвращает null, если это не операция.</summary>
    public static Transaction? ParseNotification(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string raw = Normalize(text);
        if (raw.Length < 3) return null;

        var amountMatches = AmountCurrencyRegex.Matches(raw);
        if (amountMatches.Count == 0) return null;

        Match? chosen = ChooseAmount(amountMatches, raw);
        if (chosen is null) return null;

        if (!TryParseAmount(chosen.Groups["amount"].Value, out decimal amount) || amount <= 0)
            return null;

        // Мусорные суммы (остаток на счёте без признаков операции)
        bool hasTxnKeyword = ExpenseKeywordRegex.IsMatch(raw) || IncomeKeywordRegex.IsMatch(raw);
        if (!hasTxnKeyword && IsBalanceOnly(raw, chosen, amountMatches))
            return null;

        string currencyCode = chosen.Groups["currency"].Value;
        decimal baseByn = CurrencyRates.ToByn(amount, currencyCode);
        if (baseByn <= 0) return null;

        bool isIncome = DetectIncome(raw);
        string shop = ExtractShop(raw, chosen);

        return new Transaction
        {
            Amount = baseByn,
            Currency = "BYN",
            ShopName = string.IsNullOrWhiteSpace(shop)
                ? (isIncome ? "Пополнение" : "Покупка")
                : shop,
            Category = isIncome ? DetermineIncomeCategory(raw, shop) : DetermineCategory(shop),
            Type = isIncome ? "income" : "expense",
            AccountId = "acc-1",
            Source = "push",
            Date = DateTime.Now
        };
    }

    // ---------------------------------------------------------------------
    // Нормализация
    // ---------------------------------------------------------------------

    /// <summary>
    /// Переносы строк превращаем в разделитель " | ": так сохраняется структура
    /// сообщения (Samsung Wallet пишет магазин одной строкой, а сумму — следующей).
    /// </summary>
    private static string Normalize(string text)
    {
        string s = text
            .Replace("\r\n", " | ")
            .Replace("\r", " | ")
            .Replace("\n", " | ")
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ');

        s = Regex.Replace(s, @"(\s*\|\s*)+", " | ");
        s = Regex.Replace(s, @"[ \t]{2,}", " ");
        return s.Trim(' ', '|');
    }

    // ---------------------------------------------------------------------
    // Выбор суммы
    // ---------------------------------------------------------------------

    /// <summary>
    /// Выбирает сумму операции, а не остаток по счёту:
    /// 1) сумма сразу после метки "Summa:/Сумма:/amount";
    /// 2) иначе первая сумма, перед которой нет слов «доступно/баланс/остаток»;
    /// 3) иначе самая первая.
    /// </summary>
    private static Match? ChooseAmount(MatchCollection matches, string raw)
    {
        Match? first = null;
        Match? firstNonBalance = null;
        int segmentStart = 0;

        foreach (Match m in matches)
        {
            first ??= m;

            int labelFrom = Math.Max(0, m.Index - 40);
            string justBefore = raw[labelFrom..m.Index];

            // Явная метка суммы имеет высший приоритет
            if (SumLabelRegex.IsMatch(justBefore))
                return m;

            int from = Math.Clamp(segmentStart, 0, m.Index);
            string segment = raw[from..m.Index];

            if (firstNonBalance is null && !BalanceKeywordRegex.IsMatch(segment))
                firstNonBalance = m;

            segmentStart = m.Index + m.Length;
        }

        return firstNonBalance ?? first;
    }

    private static bool TryParseAmount(string rawAmount, out decimal result)
    {
        string normalized = rawAmount
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Replace("\u202F", string.Empty)
            .Replace(',', '.');

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
               && result > 0;
    }

    private static bool IsBalanceOnly(string raw, Match chosen, MatchCollection all)
    {
        int from = 0;
        foreach (Match m in all)
        {
            if (m.Index >= chosen.Index) break;
            from = m.Index + m.Length;
        }

        from = Math.Clamp(from, 0, chosen.Index);
        return BalanceKeywordRegex.IsMatch(raw[from..chosen.Index]);
    }

    // ---------------------------------------------------------------------
    // Направление
    // ---------------------------------------------------------------------

    private static bool DetectIncome(string raw)
    {
        if (IncomingTransferRegex.IsMatch(raw)) return true;

        var expense = ExpenseKeywordRegex.Match(raw);
        var income = IncomeKeywordRegex.Match(raw);

        if (income.Success && !expense.Success) return true;
        if (!income.Success) return false;

        // Есть оба слова — побеждает то, что встречается раньше
        return income.Index < expense.Index;
    }

    // ---------------------------------------------------------------------
    // Магазин
    // ---------------------------------------------------------------------

    private static string ExtractShop(string raw, Match amount)
    {
        // 1. SHOP "SOSEDI"
        var shopKw = ShopKeywordRegex.Match(raw);
        if (shopKw.Success)
        {
            string candidate = CleanShopName(shopKw.Groups["shop"].Value);
            if (IsUsableShop(candidate)) return candidate;
        }

        // 2. GIPERMARKET "GREEN" / RBO "BURGER KING"
        var quoted = QuotedBrandRegex.Match(raw);
        if (quoted.Success)
        {
            string candidate = CleanShopName(
                quoted.Groups["prefix"].Value + " " + quoted.Groups["brand"].Value);
            if (IsUsableShop(candidate)) return candidate;
        }

        // 3. Явная метка магазина
        var merchant = MerchantLabelRegex.Match(raw);
        if (merchant.Success)
        {
            string candidate = CleanShopName(merchant.Groups["value"].Value);
            if (IsUsableShop(candidate)) return candidate;
        }

        // 4. Маска карты — единственное, что есть в СМС с кодом подтверждения
        var card = CardMaskRegex.Match(raw);
        if (card.Success)
        {
            string mask = Regex.Replace(card.Groups["mask"].Value, @"\s+", string.Empty);
            return $"Карта {mask}";
        }

        // 5. Домен из ссылки (bePaid: https://pzz.by/...)
        var url = UrlRegex.Match(raw);
        if (url.Success)
        {
            string host = url.Groups["host"].Value.ToLowerInvariant();
            if (!BankLikeHosts.Any(h => host.Contains(h, StringComparison.Ordinal)))
                return host;
        }

        // 6. Адрес (если ничего лучше не нашли)
        var address = AddressLabelRegex.Match(raw);
        if (address.Success)
        {
            string candidate = CleanShopName(address.Groups["value"].Value);
            if (IsUsableShop(candidate)) return candidate;
        }

        // 7. Текст до/после суммы в том же сегменте
        int segStart = raw.LastIndexOf('|', Math.Max(0, amount.Index - 1)) + 1;
        int segEnd = raw.IndexOf('|', amount.Index + amount.Length);
        if (segEnd < 0) segEnd = raw.Length;

        string before = raw[segStart..amount.Index];
        string after = raw[(amount.Index + amount.Length)..segEnd];

        foreach (string part in new[] { before, after })
        {
            string candidate = CleanShopName(part);
            if (IsUsableShop(candidate)) return candidate;
        }

        // 8. Предыдущий сегмент целиком: Samsung Wallet пишет название строкой выше
        if (segStart > 0)
        {
            int prevEnd = segStart - 1;
            int prevStart = raw.LastIndexOf('|', Math.Max(0, prevEnd - 1)) + 1;
            string prev = raw[prevStart..prevEnd];

            var prevShopKw = ShopKeywordRegex.Match(prev);
            if (prevShopKw.Success)
            {
                string candidate = CleanShopName(prevShopKw.Groups["shop"].Value);
                if (IsUsableShop(candidate)) return candidate;
            }

            var prevQuoted = QuotedBrandRegex.Match(prev);
            if (prevQuoted.Success)
            {
                string candidate = CleanShopName(
                    prevQuoted.Groups["prefix"].Value + " " + prevQuoted.Groups["brand"].Value);
                if (IsUsableShop(candidate)) return candidate;
            }

            string prevClean = CleanShopName(prev);
            if (IsUsableShop(prevClean)) return prevClean;
        }

        return string.Empty;
    }

    private static bool IsUsableShop(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length >= 2 && !IsNoiseShop(value);
    }

    private static string CleanShopName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string s = Normalize(raw);

        // Хвосты вида "Доступно: 120.50 BYN" / "Баланс: ..."
        foreach (string kw in new[] { "доступно", "остаток", "баланс", "available", "balance", "ostatok", "dostupno" })
        {
            int i = s.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (i > 0) s = s[..i];
        }

        s = s.Trim(' ', '.', ',', ';', '-', ':', '|', '•', '*', '(', ')', '!', '"', '\'', '«', '»');

        // Типовые префиксы в уведомлениях
        foreach (string prefix in new[]
                 {
                     "место:", "место ", "merchant:", "магазин:", "магазин ", "adres:", "адрес:",
                     "в ", "at ", "от ", "shop "
                 })
        {
            if (s.Length > prefix.Length && s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                s = s[prefix.Length..].Trim();
                break;
            }
        }

        // Слишком длинный «магазин» — это скорее мусор
        if (s.Length > 60) s = s[..60].Trim();

        return s.Trim();
    }

    /// <summary>Похоже ли значение на служебный текст, а не на магазин.</summary>
    private static bool IsNoiseShop(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        string s = value.ToLowerInvariant();

        foreach (string noise in ShopNoiseWords)
            s = s.Replace(noise, " ", StringComparison.Ordinal);

        s = s.Replace("*", " ").Trim(' ', '-', '.', ',', ':', '(', ')');

        // Если остались только цифры и знаки — это не магазин
        foreach (char c in s)
        {
            if (char.IsLetter(c)) return false;
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Категории
    // ---------------------------------------------------------------------

    private static string DetermineIncomeCategory(string raw, string shop)
    {
        string t = (raw + " " + shop).ToLowerInvariant();

        if (ContainsAny(t, "зарплат", "аванс", "преми", "zarplat", "avans", "premi"))
            return "Зарплата";

        if (ContainsAny(t, "фриланс", "проект", "гонорар", "frilans", "proekt", "gonorar"))
            return "Проект / Фриланс";

        return "Подарки и возвраты";
    }

    private static string DetermineCategory(string shop)
    {
        // Категория определяется ТОЛЬКО по названию магазина: если подмешать весь текст
        // уведомления, то служебное слово «SHOP» из Samsung Wallet превращает
        // любую покупку в «Развлечения».
        string s = shop.ToLowerInvariant();

        // Продукты и супермаркеты (в т.ч. из ваших чеков Samsung Wallet)
        if (ContainsAny(s, "evroopt", "евроопт", "sosedi", "соседи", "green", "грин",
                           "korona", "корона", "gippo", "гиппо", "santa", "санта",
                           "hit!", "хит!", "magnit", "магнит", "almi", "алми",
                           "prostore", "простор", "spar", "спар", "gipermarket", "гипермаркет",
                           "gipemarket", "pyyatnits", "pyatnits", "пятниц", "produkt", "продукт",
                           "mart", "маркет", "supermarket", "супермаркет"))
            return "Продукты";

        // Кафе, рестораны, фастфуд, доставка еды
        if (ContainsAny(s, "domino", "burger", "kfc", "pizza", "пицц", "pzz",
                           "мак", "mak.by", "суши", "sushi", "coffee", "кофе",
                           "dodo", "додо", "cafe", "кафе", "restaurant", "ресторан",
                           "doner", "дюнер", "шаурм", "starbucks", "кофейн"))
            return "Кафе и рестораны";

        // Транспорт
        if (ContainsAny(s, "yandex", "яндекс", "uber", "метро", "azs", "азс",
                           "belorusneft", "белоруснефть", "lukoil", "лукойл",
                           "taxi", "такси", "parking", "парковк", "fuel", "заправк"))
            return "Транспорт";

        // Жильё и связь
        if (ContainsAny(s, "erip", "ерип", "жкх", "коммунал", "mts", "мтс",
                           "a1", "life", "beltelecom", "белтелеком", "internet", "интернет",
                           "аренда", "rent", "utility"))
            return "Жилье и связь";

        // Шопинг, развлечения
        if (ContainsAny(s, "mooon", "silver screen", "afisha", "steam", "epic games",
                           "wildberries", "ozon", "21vek", "кино", "cinema", "игр",
                           "spotify", "netflix", "befree", "karat", "карат",
                           "шопинг", "одежда", "обувь"))
            return "Развлечения";

        // Здоровье
        if (ContainsAny(s, "apteka", "аптека", "adel", "inlek", "gym", "фитнес",
                           "clinic", "клиник", "med", "мед", "vitamin", "витамин"))
            return "Здоровье";

        return "Другое";
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (string n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

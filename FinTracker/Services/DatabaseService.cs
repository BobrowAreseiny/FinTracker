using System.Security.Cryptography;
using FinTracker.Models;
using SQLite;

namespace FinTracker.Services;

/// <summary>
/// Обёртка над SQLite с шифрованием (SQLCipher).
/// Класс зарегистрирован как Singleton, поэтому все операции сериализованы блокировкой:
/// слушатель уведомлений пишет в базу из фонового потока, а UI — из основного,
/// и SQLite-соединение не потокобезопасно.
/// </summary>
public class DatabaseService
{
    private const string PrefDbKey = "FT_DbKey";

    private readonly SQLiteConnection _db;
    private readonly object _gate = new();

    public DatabaseService()
        : this(DefaultDatabasePath())
    {
    }

    /// <summary>
    /// Путь по умолчанию. Переменная окружения FT_DB_PATH позволяет запустить
    /// приложение с отдельной базой (например, для визуальной проверки на десктопе).
    /// </summary>
    private static string DefaultDatabasePath()
    {
        string? custom = Environment.GetEnvironmentVariable("FT_DB_PATH");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "finance.db3");
    }

    /// <param name="dbPath">Путь к файлу БД (для тестов можно передать временный).</param>
    public DatabaseService(string dbPath)
    {
        string key = GetOrCreateKey();

        try
        {
            _db = Open(dbPath, key);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "DatabaseService.OpenEncrypted");

            // Возможно, осталась незашифрованная база от прошлой версии — переносим данные
            if (TryMigratePlainToEncrypted(dbPath, key, out var migrated))
            {
                _db = migrated;
                return;
            }

            // Битый файл или потерянный ключ: не роняем приложение, пересоздаём базу
            try
            {
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
            catch { }

            _db = Open(dbPath, key);
        }
    }

    // ================= КЛЮЧ ШИФРОВАНИЯ =================

    /// <summary>
    /// Ключ создаётся один раз на устройстве случайным и хранится в приватных настройках
    /// приложения (в APK его нет). Резервная копия приложения отключена,
    /// поэтому вытащить ключ вместе с базой нельзя.
    /// </summary>
    private static string GetOrCreateKey()
    {
        try
        {
            string? existing = Preferences.Default.Get<string?>(PrefDbKey, null);
            if (!string.IsNullOrWhiteSpace(existing)) return existing;

            string generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            Preferences.Default.Set(PrefDbKey, generated);
            return generated;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "DatabaseService.GetOrCreateKey");

            // Крайний случай: хоть какой-то стабильный ключ, чтобы не терять базу
            return "FinTracker-fallback-key-2026";
        }
    }

    // ================= ОТКРЫТИЕ =================

    private static SQLiteConnection Open(string dbPath, string key)
    {
        try
        {
            // Провайдер SQLCipher инициализируется идемпотентно
            SQLitePCL.Batteries_V2.Init();
        }
        catch { }

        // По умолчанию — зашифрованная база (SQLCipher).
        var options = new SQLiteConnectionString(dbPath, storeDateTimeAsTicks: true, key: key);

        var connection = new SQLiteConnection(options);
        connection.CreateTable<Transaction>();

        return connection;
    }

    /// <summary>
    /// Переносит данные из незашифрованной базы (версия до шифрования) в зашифрованную.
    /// Так обновление не приводит к потере операций.
    /// </summary>
    private static bool TryMigratePlainToEncrypted(string dbPath, string key, out SQLiteConnection encrypted)
    {
        encrypted = null!;

        if (!File.Exists(dbPath)) return false;

        try
        {
            // Пробуем открыть как обычную SQLite
            using (var plain = new SQLiteConnection(new SQLiteConnectionString(dbPath, storeDateTimeAsTicks: true)))
            {
                var rows = plain.Table<Transaction>().ToList();
                plain.Close();

                string tempPath = dbPath + ".enc";
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }

                using (var enc = new SQLiteConnection(new SQLiteConnectionString(tempPath, storeDateTimeAsTicks: true, key: key)))
                {
                    enc.CreateTable<Transaction>();
                    foreach (var row in rows)
                    {
                        row.Id = 0;          // новые идентификаторы выдаст SQLite
                        enc.Insert(row);
                    }
                    enc.Close();
                }

                File.Delete(dbPath);
                File.Move(tempPath, dbPath);

                CrashLog.Write(null, $"DatabaseService: в зашифрованную базу перенесено операций — {rows.Count}");
            }

            encrypted = new SQLiteConnection(new SQLiteConnectionString(dbPath, storeDateTimeAsTicks: true, key: key));
            return true;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "DatabaseService.TryMigratePlainToEncrypted");
            return false;
        }
    }

    // ================= СОБЫТИЯ =================

    /// <summary>Событие: в базу добавлена операция (используется автопарсером пушей).</summary>
    public event Action? OnTransactionAdded;

    /// <summary>Событие: любые изменения данных (добавление, изменение, удаление).</summary>
    public event Action? OnDataChanged;

    // ================= ЗАПИСЬ =================

    /// <summary>
    /// Добавляет операцию. Защита от дублей работает только для АВТО-операций
    /// (Source == "push" из уведомлений и "sms" из сообщений):
    /// одна и та же оплата может прийти сразу двумя каналами — Samsung Wallet пришлёт
    /// пуш, а банк — СМС. Две РУЧНЫЕ операции на одну сумму — это две разные операции,
    /// их обе нужно сохранить.
    /// </summary>
    public void AddTransaction(Transaction newTransaction)
    {
        bool inserted = false;

        lock (_gate)
        {
            if (IsDuplicateAuto(newTransaction))
                return;

            _db.Insert(newTransaction);
            inserted = true;
        }

        if (!inserted) return;

        OnTransactionAdded?.Invoke();
        OnDataChanged?.Invoke();
    }

    /// <summary>Источник, для которого включена защита от дублей.</summary>
    private static bool IsAutoSource(string? source)
    {
        return string.Equals(source, "push", StringComparison.OrdinalIgnoreCase)
               || string.Equals(source, "sms", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Дубликат авто-операции: тот же тип и та же сумма в пределах 3 минут.
    /// Название магазина намеренно НЕ сравниваем: Samsung пишет «SHOP "SOSEDI"»,
    /// а СМС — «SOSEDI» или вообще только адрес, но операция одна и та же.
    /// </summary>
    private bool IsDuplicateAuto(Transaction t)
    {
        if (!IsAutoSource(t.Source))
            return false;

        var since = t.Date.AddSeconds(-WindowSeconds);
        var until = t.Date.AddSeconds(WindowSeconds);

        var candidates = _db.Table<Transaction>()
            .Where(x => x.Date >= since && x.Date <= until)
            .ToList();

        foreach (var x in candidates)
        {
            if (!IsAutoSource(x.Source)) continue;
            if (x.Type != t.Type) continue;

            if (Math.Abs(x.Amount - t.Amount) <= 0.01m)
                return true;
        }

        return false;
    }

    /// <summary>Сколько секунд между каналами считаем одной и той же оплатой.</summary>
    private const int WindowSeconds = 180;

    // ================= ЧТЕНИЕ =================

    public List<Transaction> GetTransactions()
    {
        lock (_gate)
        {
            return _db.Table<Transaction>().OrderByDescending(t => t.Date).ToList();
        }
    }

    public List<Transaction> GetTransactionsBySource(string source)
    {
        lock (_gate)
        {
            return _db.Table<Transaction>().Where(t => t.Source == source).ToList();
        }
    }

    public void UpdateTransaction(Transaction transaction)
    {
        lock (_gate)
        {
            _db.Update(transaction);
        }
        OnDataChanged?.Invoke();
    }

    public void DeleteTransaction(Transaction transaction)
    {
        lock (_gate)
        {
            _db.Delete(transaction);
        }
        OnDataChanged?.Invoke();
    }

    /// <summary>Полная очистка истории операций.</summary>
    public void DeleteAllTransactions()
    {
        lock (_gate)
        {
            _db.DeleteAll<Transaction>();
        }
        OnDataChanged?.Invoke();
    }
}

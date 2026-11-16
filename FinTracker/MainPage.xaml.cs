using System.Globalization;
using System.Text;
using System.Text.Json;
using FinTracker.Charts;
using FinTracker.Controls;
using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace FinTracker;

/// <summary>
/// Главный экран «ФинТрек — Soft Matte Pro».
/// Полное соответствие эталонному макету (Soft Matte Dark & Warm Milky Light),
/// мульти-выделение и пакетное удаление транзакций, живая аналитика без заглушек.
/// </summary>
public partial class MainPage : ContentPage
{
    private const string AccMain = "acc-1";
    private const string AccCash = "acc-3";
    private const string WMain = "main";
    private const string WCash = "cash";

    private readonly DatabaseService _databaseService;

    private List<Transaction> _transactions = [];
    private List<CategoryDef> _expenseCategories = [];
    private List<CategoryDef> _incomeCategories = [];
    private List<DebtDef> _debts = [];

    // --- состояние экрана ---
    private string _period = "month";
    private string _accountFilter = "all";
    private string _txTypeFilter = "all";
    private string _analyticsMode = "categories";
    private string _analyticsFlow = "expense";
    private string _hubMode = "radar";
    private int? _hubDonutIndex;
    private int? _hubTreemapIndex;
    private int? _hubWaffleIndex;
    private bool _isPageVisible = true;
    private bool _dataDirty;
    private decimal _monthlyLimit = 2000m;
    private string _currency = "BYN";
    private bool _privacy;
    private string _modalType = "expense";
    private string _modalAccount = WMain;
    private int _modalCatIndex;
    private int? _editingTxId;
    private string _transferDirection = "main_to_cash";
    private string _debtFilter = "owed_to_me";
    private string _modalDebtType = "owed_to_me";
    private string _modalDebtAccount = WMain;
    private int? _selectedCatIndex;
    private string _catMgrType = "expense";
    private bool _selectionMode;
    private readonly HashSet<int> _selectedIds = [];
    private bool _jiggle;
    private string? _editingCatId;
    private string _catEditorIcon = "coffee";
    private int _catEditorColorIndex;
    private string _activeTab = "main";

    // --- оформление, тема и подсветка (Glow) ---
    private bool _isLightTheme;
    private bool _glowEnabled = true;
    private string _glowColorId = "violet";
    private string _accentHex = "#8B5CF6";
    private string _accentName = "Фиолетовый";
    private double _glowOpacity = 100.0;
    private double _customHue = 262.0;
    private Color _accentColor = Color.FromArgb("#8B5CF6");
    private bool _suppressThemeEvents;

    // 6 фирменных пресетов акцента и свечения (1 в 1 как в HTML GLOW_ACCENT_PRESETS)
    private static readonly (string Id, string Name, string Hex, double Hue)[] GlowPresets =
    [
    ("violet", "Фиолетовый", "#8B5CF6", 262),
("indigo", "Индиго", "#6366F1", 239),
("emerald", "Изумрудный", "#10B981", 160),
("rose", "Розовый", "#EC4899", 330),
("amber", "Янтарный", "#F59E0B", 38),
("cyan", "Голубой", "#06B6D4", 189)
     ];

    // Динамические цвета под Тёмную (Soft Matte) и Светлую (Warm Milky Matte) тему
    private Color BasePageColor => _isLightTheme ? Color.FromArgb("#F7F5F0") : Color.FromArgb("#0F0F11");
    private Color CardSurfaceColor => _isLightTheme ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#17171A");
    private Color PrimaryTextColor => _isLightTheme ? Color.FromArgb("#1C1917") : Colors.White;
    private Color SecondaryTextColor => _isLightTheme ? Color.FromArgb("#57534E") : Color.FromArgb("#9E9E9E");
    private Color SubtleFillColor => _isLightTheme ? Color.FromArgb("#ECE9E1") : Color.FromArgb("#0AFFFFFF");
    private Color HairlineStrokeColor => _isLightTheme ? Color.FromArgb("#121C1917") : Color.FromArgb("#1AFFFFFF");
    private Color TrackFillColor => _isLightTheme ? Color.FromArgb("#E5E0D5") : Color.FromArgb("#66000000");
    private Color EditorBoxSurfaceColor => _isLightTheme ? Color.FromArgb("#F0EDE6") : Color.FromArgb("#59000000");
    private Color ActiveControlBg => _accentColor.WithAlpha(_isLightTheme ? 0.16f : 0.20f);
    private Color ActiveControlStroke => _accentColor.WithAlpha(0.55f);

    private readonly List<string> _tabHistory = [];
    private readonly List<Border> _heatmapCells = [];
    private int _selectedHeatDay;

    private bool _loaded;
    private bool _fatalShown;
    private bool _returnToTxSheet;
    private CancellationTokenSource? _toastCts;
    private CancellationTokenSource? _searchDebounce;
    private string _lastBalanceText = string.Empty;
    private Label? _dynAvgStatusLabel;

    private readonly CultureInfo _ru = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly string[] MonthShort = ["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];
    private static readonly string[] DayShort = ["пн", "вт", "ср", "чт", "пт", "сб", "вс"];
    private static readonly string[] DayFull = ["Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье"];

    // ================= КОНСТРУКТОР =================

    public MainPage()
    : this(ResolveDatabaseService())
    {
    }

    private static DatabaseService ResolveDatabaseService()
    {
        try
        {
            var fromDi = IPlatformApplication.Current?.Services?.GetService<DatabaseService>();
            if (fromDi is not null) return fromDi;
        }
        catch { }

        return new DatabaseService();
    }

    public MainPage(DatabaseService databaseService)
    {
        _databaseService = databaseService;

        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "MainPage.InitializeComponent");
            _fatalShown = true;
            Content = BuildFatalView("Не удалось построить интерфейс", ex);
            return;
        }

        LoadPreferences();
        ConfigureSegments();
        LocateDynamicsAvgStatusLabel();
        ApplyThemeAndGlow(render: false);
        WireEvents();
        AttachEntryFocusTracking();

        _databaseService.OnDataChanged += () =>
        {
            // В фоне не перестраиваем графики и списки — экономит батарею.
            if (!_isPageVisible)
            {
                _dataDirty = true;
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_isPageVisible && !_suppressReload) _ = LoadAsync();
            });
        };
    }

    private static View BuildFatalView(string header, Exception ex)
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 12, BackgroundColor = Color.FromArgb("#0F0F11") };
        stack.Add(new Label { Text = header, FontFamily = "InterBold", FontSize = 17, TextColor = Color.FromArgb("#F87171") });
        stack.Add(new ScrollView
        {
            Content = new Label
            {
                Text = ex.ToString(),
                FontSize = 11,
                TextColor = Color.FromArgb("#E5E7EB"),
                LineBreakMode = LineBreakMode.CharacterWrap
            }
        });
        return stack;
    }

    private void LoadPreferences()
    {
        (_expenseCategories, _incomeCategories) = CategoryCatalog.Load();
        _monthlyLimit = (decimal)Preferences.Default.Get("FT_MonthlyLimit", 2000.0);
        _currency = Preferences.Default.Get("FT_CurrencyCode", "BYN");
        _privacy = Preferences.Default.Get("FT_Privacy", false);
        _period = Preferences.Default.Get("FT_Period", "month");
        _activeTab = Preferences.Default.Get("FT_ActiveTab", "main");

        _isLightTheme = Preferences.Default.Get("FT_IsLightTheme", false);
        _glowEnabled = Preferences.Default.Get("FT_GlowEnabled", true);
        _glowColorId = Preferences.Default.Get("FT_GlowColorId", "violet");
        _accentHex = Preferences.Default.Get("FT_AccentHex", "#8B5CF6");
        _accentName = Preferences.Default.Get("FT_AccentName", "Фиолетовый");
        _glowOpacity = Preferences.Default.Get("FT_GlowOpacity", 100.0);
        _customHue = Preferences.Default.Get("FT_CustomHue", 262.0);
        _accentColor = SafeParseColor(_accentHex, Color.FromArgb("#8B5CF6"));

        _debts = LoadDebts();
    }

    private static Color SafeParseColor(string hex, Color fallback)
    {
        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Находит подпись статуса под средним расходом в день («В пределах нормы»),
    /// чтобы обновлять её реальными расчётами вместо заглушки.
    /// </summary>
    private void LocateDynamicsAvgStatusLabel()
    {
        if (DynAvgLabel?.Parent is VerticalStackLayout stack && stack.Children.Count >= 3 && stack.Children[2] is Label statusLbl)
        {
            _dynAvgStatusLabel = statusLbl;
        }
    }

    private void ConfigureSegments()
    {
        PeriodSegMain?.SetItems(["Нед", "Мес", "Год"], PeriodIndex());
        PeriodSegAnalytics?.SetItems(["Нед", "Мес", "Год"], PeriodIndex());
        PeriodSegAccounts?.SetItems(["Нед", "Мес", "Год"], PeriodIndex());

        TxTypeSeg?.SetItems(["Все", "Расход", "Доход"], 0);
        AnalyticsModeSeg?.SetItems(["Категории", "Динамика"], 0, fullWidth: true);
        AnalyticsFlowSeg?.SetItems(["↙ Расход", "↗ Доход"], 0);
        CurrencySeg?.SetItems(["BYN (Br)", "USD ($)", "EUR (€)"], CurrencySegIndex, fullWidth: true);
        DebtSeg?.SetItems(["Мне должны", "Я должен"], 0, fullWidth: true);
        AddTxTypeSeg?.SetItems(["Расход", "Доход"], 0, fullWidth: true);
        AddTxAccountSeg?.SetItems(["Основной", "Наличные"], 0);
        CatMgrTypeSeg?.SetItems(["Категории расходов", "Категории доходов"], 0, fullWidth: true);
        TransferDirectionSeg?.SetItems(["Основной → Наличные", "Наличные → Основной"], 0, fullWidth: true);
        DebtTypeSeg?.SetItems(["Дал в долг", "Взял в долг"], 0, fullWidth: true);
        DebtAccountSeg?.SetItems(["Основной", "Наличные"], 0);
        ThemeModeSeg?.SetItemsWithIcons([("Тёмная Matte", "moon"), ("Светлая Matte", "sun")], _isLightTheme ? 1 : 0, fullWidth: true);

        _suppressThemeEvents = true;
        if (CustomHueSlider is not null) CustomHueSlider.Value = Math.Clamp(_customHue, 0, 360);
        if (GlowOpacitySlider is not null) GlowOpacitySlider.Value = Math.Clamp(_glowOpacity, 0, 100);
        _suppressThemeEvents = false;

        if (PrivacyIcon is not null) PrivacyIcon.Icon = _privacy ? "eye" : "eye-off";
    }

    private void WireEvents()
    {
        PeriodSegMain.SelectionChanged += (_, i) => SetPeriod(i);
        PeriodSegAnalytics.SelectionChanged += (_, i) => SetPeriod(i);
        PeriodSegAccounts.SelectionChanged += (_, i) => SetPeriod(i);

        TxTypeSeg.SelectionChanged += (_, i) =>
        {
            _txTypeFilter = i switch { 1 => "expense", 2 => "income", _ => "all" };
            RenderTransactions();
        };

        AnalyticsModeSeg.SelectionChanged += (_, i) => SetAnalyticsMode(i == 0 ? "categories" : "dynamics");
        AnalyticsFlowSeg.SelectionChanged += (_, i) => SetAnalyticsFlow(i == 0 ? "expense" : "income");
        CurrencySeg.SelectionChanged += (_, i) => SetCurrencyIndex(i);
        DebtSeg.SelectionChanged += (_, i) => SetDebtFilter(i == 0 ? "owed_to_me" : "i_owe");
        AddTxTypeSeg.SelectionChanged += (_, i) => SetModalType(i == 0 ? "expense" : "income");
        AddTxAccountSeg.SelectionChanged += (_, i) => SetModalAccount(i == 0 ? WMain : WCash);
        CatMgrTypeSeg.SelectionChanged += (_, i) => SetCatMgrType(i == 0 ? "expense" : "income");
        TransferDirectionSeg.SelectionChanged += (_, i) => _transferDirection = i == 0 ? "main_to_cash" : "cash_to_main";
        DebtTypeSeg.SelectionChanged += (_, i) => SetModalDebtType(i == 0 ? "owed_to_me" : "i_owe");
        DebtAccountSeg.SelectionChanged += (_, i) => SetModalDebtAccount(i == 0 ? WMain : WCash);

        if (ThemeModeSeg is not null)
        {
            ThemeModeSeg.SelectionChanged += (_, i) => SetThemeMode(i == 1);
        }

        AddAmountEntry.TextChanged += (_, _) => Haptics.Click();
        DebtAmountEntry.TextChanged += (_, _) => Haptics.Click();
        TransferAmountEntry.TextChanged += (_, _) => Haptics.Click();

        var donutTap = new TapGestureRecognizer();
        donutTap.Tapped += OnDonutTapped;
        DonutChart.GestureRecognizers.Add(donutTap);

        var hubChartTap = new TapGestureRecognizer();
        hubChartTap.Tapped += OnHubChartTapped;
        HubChart.GestureRecognizers.Add(hubChartTap);

        OverviewChart.SizeChanged += (_, _) => OverviewChart.Invalidate();
        DonutChart.SizeChanged += (_, _) => DonutChart.Invalidate();
        HubChart.SizeChanged += (_, _) => HubChart.Invalidate();
        SetActiveTab(_activeTab, animate: false, record: false);
    }

    // ================= ОФОРМЛЕНИЕ, ТЕМА И ПОДСВЕТКА (GLOW) =================

}

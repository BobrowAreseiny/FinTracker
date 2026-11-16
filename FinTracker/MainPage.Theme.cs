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

public partial class MainPage : ContentPage
{
    private void OnToggleThemeTapped(object? sender, TappedEventArgs e)
    {
        SetThemeMode(!_isLightTheme);
    }

    private void SetThemeMode(bool isLight)
    {
        if (_isLightTheme == isLight) return;

        _isLightTheme = isLight;
        Preferences.Default.Set("FT_IsLightTheme", _isLightTheme);
        ThemeModeSeg?.Select(_isLightTheme ? 1 : 0, raise: false);

        ApplyThemeAndGlow(render: true);
        ApplySystemBars();

        ShowToast(
        _isLightTheme ? "Светлая тема Matte включена" : "Тёмная тема Matte включена",
        _isLightTheme ? "sun" : "moon");
    }

    private void OnToggleGlowTapped(object? sender, TappedEventArgs e)
    {
        _glowEnabled = !_glowEnabled;
        Preferences.Default.Set("FT_GlowEnabled", _glowEnabled);

        if (_glowEnabled && _glowOpacity <= 0)
        {
            _glowOpacity = 100.0;
            Preferences.Default.Set("FT_GlowOpacity", _glowOpacity);
            _suppressThemeEvents = true;
            if (GlowOpacitySlider is not null) GlowOpacitySlider.Value = 100;
            _suppressThemeEvents = false;
        }

        Haptics.Click();
        ApplyThemeAndGlow(render: true);
        ShowToast(_glowEnabled ? "Подсветка включена" : "Подсветка отключена", "sparkles");
    }

    /// <summary>
    /// Формула расчёта цвета по градиентному спектру (0..360 deg) в точности из HTML-макета (s = 0.85, l = 0.60).
    /// </summary>
    private static Color ColorFromHtmlHue(double hueDegrees)
    {
        double h = Math.Clamp(hueDegrees, 0, 360);
        const double s = 0.85;
        const double l = 0.60;
        double a = s * Math.Min(l, 1.0 - l);

        int Channel(double n)
        {
            double k = (n + h / 30.0) % 12.0;
            double c = l - a * Math.Max(Math.Min(Math.Min(k - 3.0, 9.0 - k), 1.0), -1.0);
            return (int)Math.Clamp(Math.Round(255.0 * c), 0, 255);
        }

        return Color.FromRgb(Channel(0), Channel(8), Channel(4));
    }

    private void OnCustomHueSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_suppressThemeEvents) return;

        _customHue = Math.Round(e.NewValue);
        Color custom = ColorFromHtmlHue(_customHue);
        _accentColor = custom;
        _accentHex = ColorToHex(custom);
        _accentName = "Свой спектр";
        _glowColorId = "custom";

        if (!_glowEnabled)
        {
            _glowEnabled = true;
            Preferences.Default.Set("FT_GlowEnabled", true);
        }

        Preferences.Default.Set("FT_CustomHue", _customHue);
        Preferences.Default.Set("FT_GlowColorId", _glowColorId);
        Preferences.Default.Set("FT_AccentHex", _accentHex);
        Preferences.Default.Set("FT_AccentName", _accentName);

        ApplyThemeAndGlow(render: true);
    }

    private void OnGlowOpacitySliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_suppressThemeEvents) return;

        _glowOpacity = Math.Round(e.NewValue);
        Preferences.Default.Set("FT_GlowOpacity", _glowOpacity);

        bool shouldEnable = _glowOpacity > 0;
        if (_glowEnabled != shouldEnable)
        {
            _glowEnabled = shouldEnable;
            Preferences.Default.Set("FT_GlowEnabled", _glowEnabled);
        }

        ApplyThemeAndGlow(render: true);
    }

    private static string ColorToHex(Color c)
    {
        int r = (int)Math.Clamp(Math.Round(c.Red * 255), 0, 255);
        int g = (int)Math.Clamp(Math.Round(c.Green * 255), 0, 255);
        int b = (int)Math.Clamp(Math.Round(c.Blue * 255), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
}

    /// <summary>Мягкий радиальный ореол вместо резкой заливки (имитация blur-3xl из макета).</summary>
    private static RadialGradientBrush MakeOrbBrush(Color accent, float alpha)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            Radius = 0.5
        };
        brush.GradientStops.Add(new GradientStop(accent.WithAlpha(alpha), 0f));
        brush.GradientStops.Add(new GradientStop(accent.WithAlpha(alpha * 0.55f), 0.5f));
        brush.GradientStops.Add(new GradientStop(accent.WithAlpha(0f), 1f));
        return brush;
    }

    private void SelectGlowPreset(string id, string name, string hex, double hue)
    {
        _glowColorId = id;
        _accentName = name;
        _accentHex = hex;
        _customHue = hue;
        _accentColor = SafeParseColor(hex, Color.FromArgb("#8B5CF6"));

        if (!_glowEnabled)
        {
            _glowEnabled = true;
            Preferences.Default.Set("FT_GlowEnabled", true);
        }

        if (_glowOpacity <= 0)
        {
            _glowOpacity = 100.0;
            Preferences.Default.Set("FT_GlowOpacity", _glowOpacity);
        }

        Preferences.Default.Set("FT_GlowColorId", _glowColorId);
        Preferences.Default.Set("FT_AccentName", _accentName);
        Preferences.Default.Set("FT_AccentHex", _accentHex);
        Preferences.Default.Set("FT_CustomHue", _customHue);

        _suppressThemeEvents = true;
        if (CustomHueSlider is not null) CustomHueSlider.Value = hue;
        if (GlowOpacitySlider is not null) GlowOpacitySlider.Value = _glowOpacity;
        _suppressThemeEvents = false;

        ApplyThemeAndGlow(render: true);
        ShowToast($"Цвет акцента и подсветки: { name}", "palette");
    }

    private void ApplyThemeAndGlow(bool render)
    {
        _accentColor = SafeParseColor(_accentHex, Color.FromArgb("#8B5CF6"));

        // 1. Обновление DynamicResource токенов (Soft Matte Dark / Warm Milky Light)
        Resources["BaseColor"] = BasePageColor;
        Resources["CardColor"] = CardSurfaceColor;
        Resources["AccentPurple"] = _accentColor;
        Resources["TextPrimary"] = PrimaryTextColor;
        Resources["TextSecondaryColor"] = SecondaryTextColor;
        Resources["HairlineColor"] = HairlineStrokeColor;
        Resources["SubtleSurface"] = SubtleFillColor;
        Resources["TrackColor"] = TrackFillColor;
        Resources["EditorBoxBg"] = EditorBoxSurfaceColor;

        // Цвета подписей и разделителей внутри графиков.
        ChartTheme.Label = _isLightTheme ? Color.FromArgb("#3F3A36") : Color.FromArgb("#9E9E9E");
        ChartTheme.Guide = _isLightTheme ? Color.FromArgb("#C9C1B2") : Color.FromArgb("#12FFFFFF");
        ChartTheme.Surface = CardSurfaceColor;
        ChartTheme.InactiveBar = _isLightTheme ? Color.FromArgb("#D8D2C5") : Color.FromArgb("#1FFFFFFF");
        ChartTheme.RankingText = _isLightTheme ? Color.FromArgb("#3F3A36") : Color.FromArgb("#D8D8D8");
        ChartTheme.RankingValue = _isLightTheme ? Color.FromArgb("#57534E") : Color.FromArgb("#F0F0F0");
        ChartTheme.RankingTrack = _isLightTheme ? Color.FromArgb("#1A000000") : Color.FromArgb("#1AFFFFFF");

        // Все сегмент-контролы перекрашиваем под текущую тему.
        SegmentedControl.ApplyTheme(_isLightTheme, _accentColor);

        if (LimitPaceMarker is not null)
        {
            LimitPaceMarker.BackgroundColor = _isLightTheme
            ? Color.FromArgb("#18181B")
            : Color.FromArgb("#CCFFFFFF");
        }

        // 2. Расчёт силы неонового свечения (Glow) как в HTML
        float glowFactor = _glowEnabled ? (float)Math.Clamp(_glowOpacity / 100.0, 0.0, 1.0) : 0f;

        // Внутренние цветовые пятна в главных карточках убраны:
        // остаётся только акцентная подсветка рамки.
        if (OrbMainHero is not null) OrbMainHero.IsVisible = false;
        if (OrbAnalyticsDonut is not null) OrbAnalyticsDonut.IsVisible = false;
        if (OrbAccountsHero is not null) OrbAccountsHero.IsVisible = false;
        if (OrbSettingsAppearance is not null) OrbSettingsAppearance.IsVisible = false;

        if (LogoShadow is not null)
        {
            LogoShadow.Brush = new SolidColorBrush(_accentColor);
            LogoShadow.Opacity = 0.45f * glowFactor;
        }

        if (FabShadow is not null)
        {
            FabShadow.Brush = new SolidColorBrush(_accentColor);
            FabShadow.Opacity = 0.45f * glowFactor;
        }

        // Карточки с классом .matte-card-glow в HTML
        Color glowStroke = glowFactor > 0.02f
        ? _accentColor.WithAlpha(0.24f * glowFactor + 0.06f)
        : HairlineStrokeColor;

        Border?[] glowCards = [MainHeroCard, AnalyticsDonutCard, AnalyticsHubCard, AccountsHeroCard, AppearanceCard];
        foreach (var card in glowCards)
        {
            if (card is null) continue;

            // Только акцентная рамка, без цветного ореола вокруг карточки.
            card.Stroke = glowStroke;
            card.Shadow = new Shadow
            {
                Brush = Colors.Black,
                Offset = new Point(0, 6),
                Radius = 20,
                Opacity = 0.30f
            };
        }

        // 3. Обновление акцентных плашек и иконок
        Color softBadgeBg = _accentColor.WithAlpha(_isLightTheme ? 0.14f : 0.16f);
        Color softBadgeStroke = _accentColor.WithAlpha(0.32f);

        if (ManageCatsBtn is not null)
        {
            ManageCatsBtn.BackgroundColor = softBadgeBg;
            ManageCatsBtn.Stroke = softBadgeStroke;
        }
        if (AccAllBadge is not null)
        {
            AccAllBadge.BackgroundColor = softBadgeBg;
            AccAllBadge.Stroke = softBadgeStroke;
        }
        if (TransferPillButton is not null)
        {
            TransferPillButton.BackgroundColor = softBadgeBg;
            TransferPillButton.Stroke = softBadgeStroke;
        }
        if (AppearanceIconBox is not null)
        {
            AppearanceIconBox.BackgroundColor = softBadgeBg;
            AppearanceIconBox.Stroke = softBadgeStroke;
        }
        if (SettingsCatIconBox is not null)
        {
            SettingsCatIconBox.BackgroundColor = softBadgeBg;
            SettingsCatIconBox.Stroke = softBadgeStroke;
        }
        if (AutoTrackBotBadge is not null)
        {
            AutoTrackBotBadge.BackgroundColor = _accentColor.WithAlpha(0.20f);
        }
        if (HeatmapIconBox is not null)
        {
            HeatmapIconBox.BackgroundColor = softBadgeBg;
        }
        if (ModalOpenCatMgrBtn is not null)
        {
            ModalOpenCatMgrBtn.BackgroundColor = softBadgeBg;
            ModalOpenCatMgrBtn.Stroke = softBadgeStroke;
        }
        if (ToastPulseBox is not null)
        {
            ToastPulseBox.BackgroundColor = _accentColor.WithAlpha(0.20f);
        }
        if (ToastBorder is not null)
        {
            ToastBorder.Stroke = _accentColor.WithAlpha(0.45f);
        }
        if (SearchWrap is not null)
        {
            SearchWrap.Stroke = _accentColor.WithAlpha(0.45f);
        }

        // 4. Кнопка темы в шапке и элементы карточки «Оформление и подсветка»
        if (HeaderThemeIcon is not null)
        {
            HeaderThemeIcon.Icon = _isLightTheme ? "moon" : "sun";
            HeaderThemeIcon.Tint = SecondaryTextColor;
        }
        if (TagsIcon is not null) TagsIcon.Tint = SecondaryTextColor;
        if (PrivacyIcon is not null) PrivacyIcon.Tint = SecondaryTextColor;

        if (GlowSwitchTrack is not null && GlowSwitchThumb is not null)
        {
            GlowSwitchTrack.BackgroundColor = _glowEnabled ? _accentColor : TrackFillColor;
            GlowSwitchTrack.Stroke = _glowEnabled ? _accentColor : HairlineStrokeColor;
            GlowSwitchThumb.TranslationX = _glowEnabled ? 20 : 0;
        }

        if (GlowStatusSubLabel is not null)
        {
            GlowStatusSubLabel.Text = _glowEnabled ? "Включено" : "Выключено";
        }

        if (ActiveGlowColorNameLabel is not null)
        {
            ActiveGlowColorNameLabel.Text = _glowColorId == "custom" ? "Свой спектр" : _accentName;
        }
        if (CustomHexLabel is not null) CustomHexLabel.Text = _accentHex.ToUpperInvariant();
        if (GlowOpacityLabel is not null) GlowOpacityLabel.Text = $"{ (int)Math.Round(_glowOpacity)}% ";
    if (OpacityGradientEndStop is not null) OpacityGradientEndStop.Color = _accentColor;

        RenderGlowColorPicker();
        UpdateNavBarColors();

        if (render && _loaded)
        {
            RenderAll();
        }
    }

    private void RenderGlowColorPicker()
    {
        if (GlowColorPickerRow is null) return;

        GlowColorPickerRow.Children.Clear();

        for (int i = 0; i < GlowPresets.Length; i++)
        {
            var preset = GlowPresets[i];
            bool active = _glowColorId == preset.Id;
            Color swatchColor = Color.FromArgb(preset.Hex);

            var swatch = new Border
            {
                WidthRequest = active ? 34 : 30,
                HeightRequest = active ? 34 : 30,
                StrokeThickness = active ? 2 : 0,
                Stroke = active ? PrimaryTextColor : Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                BackgroundColor = swatchColor,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Shadow = active
            ? new Shadow { Brush = new SolidColorBrush(swatchColor), Offset = new Point(0, 0), Radius = 12, Opacity = 0.45f }
            : null
            };

            string pId = preset.Id;
            string pName = preset.Name;
            string pHex = preset.Hex;
            double pHue = preset.Hue;
            HapticsClick(swatch, () => SelectGlowPreset(pId, pName, pHex, pHue));

            Grid.SetColumn(swatch, i);
            GlowColorPickerRow.Children.Add(swatch);
        }
    }

    /// <summary>
    /// Строит бейдж иконки категории с учётом темы (1 в 1 как getBadgeStyle в HTML).
    /// </summary>
    private Border BuildThemedBadge(CategoryDef cat, double boxSize, double iconSize, double cornerRadius)
    {
        Color chartColor = SafeParseColor(cat.ChartColor, _accentColor);

        Color bg = _isLightTheme
        ? chartColor.WithAlpha(0.10f)
        : SafeParseColor(cat.BoxBg, Color.FromArgb("#17171A"));
        Color border = _isLightTheme
        ? chartColor.WithAlpha(0.22f)
        : SafeParseColor(cat.BoxBorder, Color.FromArgb("#2E2E32"));
        Color iconTint = _isLightTheme
        ? chartColor
        : SafeParseColor(cat.IconColor, Colors.White);

        return new Border
        {
            WidthRequest = boxSize,
            HeightRequest = boxSize,
            StrokeThickness = 1,
            Stroke = border,
            StrokeShape = new RoundRectangle { CornerRadius = (float)cornerRadius },
            BackgroundColor = bg,
            Padding = 0,
            Content = new LucideIcon
            {
                Icon = cat.Icon,
                Size = iconSize,
                Tint = iconTint,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    private void UpdateNavBarColors()
    {
        if (NavMain is null) return;
        UpdateNavItem(NavMain, NavMainIcon, NavMainLabel, _activeTab == "main");
        UpdateNavItem(NavAnalytics, NavAnalyticsIcon, NavAnalyticsLabel, _activeTab == "analytics");
        UpdateNavItem(NavAccounts, NavAccountsIcon, NavAccountsLabel, _activeTab == "accounts");
        UpdateNavItem(NavSettings, NavSettingsIcon, NavSettingsLabel, _activeTab == "settings");
    }

    // ================= ЖИЗНЕННЫЙ ЦИКЛ =================

}

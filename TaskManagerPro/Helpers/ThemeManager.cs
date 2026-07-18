using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace TaskManagerPro.Helpers
{
    /// <summary>
    /// اعمال تم‌ها + رنگ Accent انتخابی کاربر روی کل برنامه:
    /// Dark         = تم تیره + Mica
    /// Light        = تم روشن + Mica
    /// Liquid Glass = تم تیره + Acrylic (بلور عمیق و شفافیت لایه‌ای)
    /// Midnight     = تم تیره + Mica Alt (تیره‌تر و عمیق‌تر)
    /// Aurora       = تم روشن + Acrylic (شیشه‌ای روشن)
    /// OLED         = مشکی مطلق بدون Backdrop (برای نمایشگرهای OLED)
    /// Paper        = روشن مات بدون Backdrop
    /// </summary>
    public static class ThemeManager
    {
        public static void Apply(Window? window = null)
        {
            window ??= App.MainAppWindow;
            if (window == null) return;
            if (window.Content is not FrameworkElement root) return;

            // رنگ Accent انتخابی کاربر روی کل برنامه (نه فقط گراف‌ها)
            ApplyAccent();

            ElementTheme theme = ElementTheme.Dark;
            SystemBackdrop? backdrop = new MicaBackdrop();
            Windows.UI.Color? solidBg = null;

            switch (AppSettings.Theme)
            {
                case AppTheme.Dark:
                    theme = ElementTheme.Dark;
                    backdrop = new MicaBackdrop();
                    break;

                case AppTheme.Light:
                    theme = ElementTheme.Light;
                    backdrop = new MicaBackdrop();
                    break;

                case AppTheme.LiquidGlass:
                    theme = ElementTheme.Dark;
                    backdrop = new DesktopAcrylicBackdrop();
                    break;

                case AppTheme.Midnight:
                    theme = ElementTheme.Dark;
                    backdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
                    break;

                case AppTheme.Aurora:
                    theme = ElementTheme.Light;
                    backdrop = new DesktopAcrylicBackdrop();
                    break;

                case AppTheme.Oled:
                    theme = ElementTheme.Dark;
                    backdrop = null;
                    solidBg = Windows.UI.Color.FromArgb(255, 0, 0, 0);
                    break;

                case AppTheme.Paper:
                    theme = ElementTheme.Light;
                    backdrop = null;
                    solidBg = Windows.UI.Color.FromArgb(255, 243, 242, 240);
                    break;
            }

            window.SystemBackdrop = backdrop;

            // پس‌زمینه‌ی تخت برای تم‌های بدون Backdrop؛ برای بقیه شفاف تا Mica/Acrylic دیده شود
            if (root is Panel panel)
                panel.Background = solidBg is Windows.UI.Color c ? new SolidColorBrush(c) : null;

            // فیکس: تعویض تم باید کل برنامه را عوض کند نه فقط گراف‌ها.
            // اگر تم همان تم قبلی است، یک فریم تم برعکس اعمال و در فریم بعد برگردانده می‌شود
            // تا همه‌ی ThemeResource ها (از جمله رنگ Accent جدید) واقعاً دوباره ارزیابی شوند.
            if (root.RequestedTheme == theme)
            {
                root.RequestedTheme = theme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                var target = theme;
                root.DispatcherQueue.TryEnqueue(() => root.RequestedTheme = target);
            }
            else
            {
                root.RequestedTheme = theme;
            }
        }

        /// <summary>
        /// رنگ Accent کل برنامه (دکمه‌ها، آیکون‌ها، آیتم انتخاب‌شده، تاگل‌ها و ...) را
        /// از روی رنگ انتخابی کاربر ست می‌کند (به جای رنگ پیش‌فرض ویندوز).
        /// </summary>
        public static void ApplyAccent()
        {
            try
            {
                var c = ColorUtil.FromHex(AppSettings.AccentColor);
                var app = Application.Current.Resources;

                // فیکس قطعی: کلیدها باید در دیکشنری «اصلی» Application.Resources نوشته شوند.
                // ThemeDictionaries سطح اپ در lookup از XamlControlsResources (merged) نمی‌بَرد،
                // ولی دیکشنری اصلی همیشه اول چک می‌شود — برای همین قبلاً فقط گراف‌ها عوض می‌شدند.
                bool darkTheme = AppSettings.Theme is not (AppTheme.Light or AppTheme.Aurora or AppTheme.Paper);

                SetAccentColorsInto(app, c);
                SetAccentBrushesInto(app, c, darkTheme);
            }
            catch { }
        }

        private static void SetAccentColorsInto(ResourceDictionary res, Windows.UI.Color c)
        {
            res["SystemAccentColor"] = c;
            res["SystemAccentColorLight1"] = ColorUtil.Shift(c, 0.2);
            res["SystemAccentColorLight2"] = ColorUtil.Shift(c, 0.4);
            res["SystemAccentColorLight3"] = ColorUtil.Shift(c, 0.6);
            res["SystemAccentColorDark1"] = ColorUtil.Shift(c, -0.2);
            res["SystemAccentColorDark2"] = ColorUtil.Shift(c, -0.4);
            res["SystemAccentColorDark3"] = ColorUtil.Shift(c, -0.6);
        }

        /// <summary>
        /// همان نگاشت رسمی Fluent: در تم تیره از سایه‌های روشن Accent و در تم روشن
        /// از سایه‌های تیره استفاده می‌شود.
        /// </summary>
        private static void SetAccentBrushesInto(ResourceDictionary res, Windows.UI.Color c, bool darkTheme)
        {
            var fill = darkTheme ? ColorUtil.Shift(c, 0.4) : ColorUtil.Shift(c, -0.2);   // Light2 / Dark1
            var text = darkTheme ? ColorUtil.Shift(c, 0.6) : ColorUtil.Shift(c, -0.4);   // Light3 / Dark2
            var text2 = darkTheme ? ColorUtil.Shift(c, 0.6) : ColorUtil.Shift(c, -0.6);  // Light3 / Dark3
            var text3 = darkTheme ? ColorUtil.Shift(c, 0.4) : ColorUtil.Shift(c, -0.2);  // Light2 / Dark1

            res["AccentFillColorDefaultBrush"] = new SolidColorBrush(fill);
            res["AccentFillColorSecondaryBrush"] = new SolidColorBrush(fill) { Opacity = 0.9 };
            res["AccentFillColorTertiaryBrush"] = new SolidColorBrush(fill) { Opacity = 0.8 };
            res["AccentFillColorSelectedTextBackgroundBrush"] = new SolidColorBrush(c);

            res["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(text);
            res["AccentTextFillColorSecondaryBrush"] = new SolidColorBrush(text2);
            res["AccentTextFillColorTertiaryBrush"] = new SolidColorBrush(text3);

            res["SystemFillColorAttentionBrush"] = new SolidColorBrush(darkTheme ? ColorUtil.Shift(c, 0.2) : ColorUtil.Shift(c, -0.2));

            // کلیدهای قدیمی‌تر (WinUI/UWP) که بعضی کنترل‌ها هنوز استفاده می‌کنند
            res["SystemControlHighlightAccentBrush"] = new SolidColorBrush(c);
            res["SystemControlForegroundAccentBrush"] = new SolidColorBrush(c);
            res["SystemControlBackgroundAccentBrush"] = new SolidColorBrush(c);
            res["SystemControlHighlightListAccentLowBrush"] = new SolidColorBrush(c) { Opacity = 0.6 };
            res["SystemControlHighlightListAccentMediumBrush"] = new SolidColorBrush(c) { Opacity = 0.8 };
            res["SystemControlHighlightListAccentHighBrush"] = new SolidColorBrush(c) { Opacity = 0.9 };
            res["SystemControlHyperlinkTextBrush"] = new SolidColorBrush(text);

            // کلیدهای lightweight styling — کنترل‌هایی که داخل تمپلیتشان مستقیم به این
            // کلیدها اشاره می‌کنند (ProgressBar، نشانگر انتخاب NavigationView، اسلایدر و ...)
            res["ProgressBarForeground"] = new SolidColorBrush(fill);
            res["ProgressRingForeground"] = new SolidColorBrush(fill);
            res["NavigationViewSelectionIndicatorForeground"] = new SolidColorBrush(fill);

            res["ToggleSwitchFillOn"] = new SolidColorBrush(fill);
            res["ToggleSwitchFillOnPointerOver"] = new SolidColorBrush(fill) { Opacity = 0.9 };
            res["ToggleSwitchFillOnPressed"] = new SolidColorBrush(fill) { Opacity = 0.8 };
            res["ToggleSwitchStrokeOn"] = new SolidColorBrush(fill);
            res["ToggleSwitchStrokeOnPointerOver"] = new SolidColorBrush(fill) { Opacity = 0.9 };
            res["ToggleSwitchStrokeOnPressed"] = new SolidColorBrush(fill) { Opacity = 0.8 };

            res["SliderTrackValueFill"] = new SolidColorBrush(fill);
            res["SliderTrackValueFillPointerOver"] = new SolidColorBrush(fill) { Opacity = 0.9 };
            res["SliderTrackValueFillPressed"] = new SolidColorBrush(fill) { Opacity = 0.8 };
            res["SliderThumbBackground"] = new SolidColorBrush(fill);
            res["SliderThumbBackgroundPointerOver"] = new SolidColorBrush(fill) { Opacity = 0.9 };
            res["SliderThumbBackgroundPressed"] = new SolidColorBrush(fill) { Opacity = 0.8 };

            res["CheckBoxCheckBackgroundFillChecked"] = new SolidColorBrush(fill);
            res["CheckBoxCheckBackgroundFillCheckedPointerOver"] = new SolidColorBrush(fill) { Opacity = 0.9 };
            res["CheckBoxCheckBackgroundFillCheckedPressed"] = new SolidColorBrush(fill) { Opacity = 0.8 };
            res["CheckBoxCheckBackgroundStrokeChecked"] = new SolidColorBrush(fill);

            res["RadioButtonOuterEllipseCheckedStroke"] = new SolidColorBrush(fill);
            res["RadioButtonOuterEllipseCheckedStrokePointerOver"] = new SolidColorBrush(fill) { Opacity = 0.9 };
            res["RadioButtonOuterEllipseCheckedStrokePressed"] = new SolidColorBrush(fill) { Opacity = 0.8 };

            res["HyperlinkButtonForeground"] = new SolidColorBrush(text);
            res["HyperlinkButtonForegroundPointerOver"] = new SolidColorBrush(text) { Opacity = 0.9 };
            res["HyperlinkButtonForegroundPressed"] = new SolidColorBrush(text) { Opacity = 0.8 };
        }

        /// <summary>اعمال حالت «همیشه رو» بودن پنجره‌ی اصلی</summary>
        public static void ApplyAlwaysOnTop(Window? window = null)
        {
            window ??= App.MainAppWindow;
            if (window?.AppWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = AppSettings.AlwaysOnTop;
            }
        }
    }
}

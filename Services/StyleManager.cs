using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Curio.Services
{
    public static class StyleManager
    {
        public static void Apply(string uiStyle, string theme)
        {
            try
            {
                if (Application.Current == null) return;

                var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

                // 1. Resolve Theme (System / Light / Dark)
                string themeSource = "pack://application:,,,/Styles/ThemeLight.xaml";
                bool isDark = false;

                if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
                {
                    isDark = true;
                }
                else if (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase))
                {
                    isDark = IsSystemDarkTheme();
                }

                if (isDark)
                {
                    themeSource = "pack://application:,,,/Styles/ThemeDark.xaml";
                }

                // 2. Resolve UI Style (Modern vs XP)
                string styleSource = "pack://application:,,,/Styles/ModernStyle.xaml";
                if (string.Equals(uiStyle, "XP", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uiStyle, "Windows XP風", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(uiStyle, "OLD", StringComparison.OrdinalIgnoreCase))
                {
                    styleSource = "pack://application:,,,/Styles/XPStyle.xaml";
                }

                // 3. Update MergedDictionaries
                var themeUri = new Uri(themeSource, UriKind.RelativeOrAbsolute);
                var styleUri = new Uri(styleSource, UriKind.RelativeOrAbsolute);

                var themeDict = new ResourceDictionary { Source = themeUri };
                var styleDict = new ResourceDictionary { Source = styleUri };

                mergedDictionaries.Clear();
                mergedDictionaries.Add(themeDict);
                mergedDictionaries.Add(styleDict);

                // MainWindow intentionally keeps the existing, small resource hook:
                // its implicit Button style is based on AppButtonStyle.  Because
                // BasedOn is resolved as a static resource, refresh already-created
                // buttons after replacing the style dictionary so a setting change
                // takes effect immediately without rebuilding the window or touching
                // the XP layout.
                RefreshOpenWindows(styleDict);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StyleManager Error] {ex.Message}");
            }
        }

        private static void RefreshOpenWindows(ResourceDictionary styleDictionary)
        {
            if (styleDictionary["AppButtonStyle"] is not Style baseButtonStyle)
            {
                return;
            }

            foreach (Window window in Application.Current.Windows)
            {
                foreach (Button button in FindButtons(window))
                {
                    button.Style = new Style(typeof(Button), baseButtonStyle);
                }
            }
        }

        private static IEnumerable<Button> FindButtons(DependencyObject root)
        {
            if (root is Button button)
            {
                yield return button;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                foreach (Button child in FindButtons(VisualTreeHelper.GetChild(root, i)))
                {
                    yield return child;
                }
            }
        }

        public static bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0; // 0 = Dark mode in Windows
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }
}

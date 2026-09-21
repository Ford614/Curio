using System.Globalization;
using System.Windows;

namespace Curio.Services
{
    public static class LocalizationService
    {
        public const string Japanese = "ja-JP";
        public const string English = "en-US";

        public static string CurrentLanguage { get; private set; } = Japanese;

        private static readonly IReadOnlyDictionary<string, string> RoleNameKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Arrow"] = "RoleArrow",
                ["Help"] = "RoleHelp",
                ["AppStarting"] = "RoleAppStarting",
                ["Wait"] = "RoleWait",
                ["Crosshair"] = "RoleCrosshair",
                ["IBeam"] = "RoleIBeam",
                ["NWPen"] = "RoleNWPen",
                ["No"] = "RoleNo",
                ["SizeNS"] = "RoleSizeNS",
                ["SizeWE"] = "RoleSizeWE",
                ["SizeNWSE"] = "RoleSizeNWSE",
                ["SizeNESW"] = "RoleSizeNESW",
                ["SizeAll"] = "RoleSizeAll",
                ["UpArrow"] = "RoleUpArrow",
                ["Hand"] = "RoleHand",
                ["Pin"] = "RolePin",
                ["Person"] = "RolePerson"
            };

        internal static void SetCurrentLanguage(string language)
        {
            CurrentLanguage = language;
        }

        public static void Apply(string? language)
        {
            CurrentLanguage = string.Equals(language, English, StringComparison.OrdinalIgnoreCase)
                ? English
                : Japanese;

            if (Application.Current == null)
                return;

            string source = $"pack://application:,,,/Resources/Strings.{CurrentLanguage}.xaml";
            var dictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Absolute) };
            ResourceDictionary? oldDictionary = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(item => item.Source?.OriginalString.Contains("/Resources/Strings.", StringComparison.OrdinalIgnoreCase) == true);

            if (oldDictionary != null)
            {
                int index = Application.Current.Resources.MergedDictionaries.IndexOf(oldDictionary);
                Application.Current.Resources.MergedDictionaries[index] = dictionary;
            }
            else
            {
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        public static string Get(string key, string? fallback = null)
        {
            return Application.Current?.TryFindResource(key) as string ?? fallback ?? key;
        }

        public static string Format(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), arguments)
                .Replace("\\n", Environment.NewLine, StringComparison.Ordinal);
        }

        public static string GetRoleDisplayName(string registryKey, string fallback)
        {
            return RoleNameKeys.TryGetValue(registryKey, out string? key) ? Get(key, fallback) : fallback;
        }

        public static string GetRoleDescription(string registryKey, string fallback)
        {
            return RoleNameKeys.TryGetValue(registryKey, out string? key) ? Get(key + "Description", fallback) : fallback;
        }
    }
}

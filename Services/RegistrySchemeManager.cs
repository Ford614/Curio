using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Curio.Models;

namespace Curio.Services
{
    public class InstallResult
    {
        public bool Success { get; set; }
        public string SchemeName { get; set; } = string.Empty;
        public string TargetDirectory { get; set; } = string.Empty;
        public List<string> LogMessages { get; } = new();
        public int ErrorsCount { get; set; }
    }

    public static class RegistrySchemeManager
    {
        private const string SchemesSubKey = @"Control Panel\Cursors\Schemes";
        private const string CursorsSubKey = @"Control Panel\Cursors";

        private const uint SPI_SETCURSORS = 0x0057;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        private static string? _customStorageDirectory;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string? pvParam, uint fWinIni);

        public static string GetDefaultStorageDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Curio", "Schemes");
        }

        public static string StorageDirectory
        {
            get => string.IsNullOrWhiteSpace(_customStorageDirectory) ? GetDefaultStorageDirectory() : _customStorageDirectory;
            set => _customStorageDirectory = value;
        }

        public static bool SchemeExists(string schemeName)
        {
            if (string.IsNullOrWhiteSpace(schemeName)) return false;

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SchemesSubKey, false);
            if (key == null) return false;

            return key.GetValueNames().Any(v => v.Equals(schemeName, StringComparison.OrdinalIgnoreCase));
        }

        public static List<InstalledSchemeInfo> GetInstalledSchemes()
        {
            var result = new List<InstalledSchemeInfo>();

            string currentActiveScheme = GetCurrentActiveSchemeName();

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SchemesSubKey, false);
            if (key == null) return result;

            string baseAppDir = StorageDirectory;
            string defaultAppDir = GetDefaultStorageDirectory();
            string oldAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MouseCursorInstaller", "Schemes");

            foreach (string valueName in key.GetValueNames())
            {
                object? val = key.GetValue(valueName);
                if (val is string regStr)
                {
                    var paths = regStr.Split(',').ToList();
                    bool managed = paths.Any(p => !string.IsNullOrEmpty(p) && 
                        (p.StartsWith(baseAppDir, StringComparison.OrdinalIgnoreCase) || 
                         p.StartsWith(defaultAppDir, StringComparison.OrdinalIgnoreCase) ||
                         p.StartsWith(oldAppDir, StringComparison.OrdinalIgnoreCase)));

                    result.Add(new InstalledSchemeInfo
                    {
                        Name = valueName,
                        RawRegistryString = regStr,
                        FilePaths = paths,
                        IsCurrentActive = valueName.Equals(currentActiveScheme, StringComparison.OrdinalIgnoreCase),
                        IsManagedByApp = managed
                    });
                }
            }

            return result.OrderBy(s => s.Name).ToList();
        }

        public static string GetCurrentActiveSchemeName()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(CursorsSubKey, false);
            if (key == null) return string.Empty;

            object? val = key.GetValue(null); // (Default) value
            if (val is string str) return str;

            object? valScheme = key.GetValue("Scheme Source");
            if (valScheme is string strScheme) return strScheme;

            return string.Empty;
        }

        public static InstallResult InstallScheme(string schemeName, List<CursorRoleMapping> roleMappings, bool applyImmediately)
        {
            var result = new InstallResult { SchemeName = schemeName };
            string sanitizedSchemeName = SanitizeFileName(schemeName);

            if (string.IsNullOrWhiteSpace(sanitizedSchemeName))
            {
                result.LogMessages.Add("[エラー] 有効なスキーム名を入力してください。");
                result.ErrorsCount++;
                return result;
            }

            string schemeStorageDir = Path.Combine(StorageDirectory, sanitizedSchemeName);
            result.TargetDirectory = schemeStorageDir;

            try
            {
                if (!Directory.Exists(schemeStorageDir))
                {
                    Directory.CreateDirectory(schemeStorageDir);
                }

                result.LogMessages.Add($"[情報] インストール先フォルダ作成: {schemeStorageDir}");
            }
            catch (Exception ex)
            {
                result.LogMessages.Add($"[エラー] フォルダの作成に失敗しました: {ex.Message}");
                result.ErrorsCount++;
                return result;
            }

            var rolePaths = new string[17];
            for (int i = 0; i < 17; i++) rolePaths[i] = string.Empty;

            foreach (var mapping in roleMappings)
            {
                int index = mapping.Role.Index;
                if (index < 0 || index >= 17) continue;

                if (!mapping.IsAssigned || string.IsNullOrEmpty(mapping.FilePath) || !File.Exists(mapping.FilePath))
                {
                    rolePaths[index] = string.Empty;
                    continue;
                }

                try
                {
                    string extension = Path.GetExtension(mapping.FilePath);
                    string targetFileName = $"{mapping.Role.RegistryKey}{extension}";
                    string targetFilePath = Path.Combine(schemeStorageDir, targetFileName);

                    File.Copy(mapping.FilePath, targetFilePath, overwrite: true);
                    rolePaths[index] = targetFilePath;

                    result.LogMessages.Add($"[成功] コピー完了 [{mapping.Role.DisplayName}] -> {targetFileName}");
                }
                catch (Exception ex)
                {
                    result.LogMessages.Add($"[エラー] ファイルコピー失敗 [{mapping.Role.DisplayName}]: {ex.Message}");
                    result.ErrorsCount++;
                    // Non-blocking: continue with next files
                    rolePaths[index] = string.Empty;
                }
            }

            string registryValueString = string.Join(",", rolePaths);

            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(SchemesSubKey, true);
                if (key != null)
                {
                    key.SetValue(schemeName, registryValueString, RegistryValueKind.String);
                    result.LogMessages.Add($"[成功] レジストリへのスキーム登録完了: HKCU\\{SchemesSubKey}\\{schemeName}");
                }
                else
                {
                    result.LogMessages.Add("[エラー] レジストリキーの開上に失敗しました。");
                    result.ErrorsCount++;
                }
            }
            catch (Exception ex)
            {
                result.LogMessages.Add($"[エラー] レジストリ書き込みエラー: {ex.Message}");
                result.ErrorsCount++;
                return result;
            }

            if (applyImmediately && result.ErrorsCount == 0)
            {
                ApplySchemeToSystem(schemeName, roleMappings, rolePaths, result.LogMessages);
            }

            result.Success = true;
            result.LogMessages.Add($"[完了] スキーム '{schemeName}' の一括インストールが終了しました。");
            return result;
        }

        public static bool ApplySchemeToSystem(string schemeName, List<CursorRoleMapping>? roleMappings, string[]? installedPaths, List<string>? logMessages)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(CursorsSubKey, true);
                if (key == null) return false;

                key.SetValue(null, schemeName, RegistryValueKind.String);
                key.SetValue("Scheme Source", "1", RegistryValueKind.String);

                var standardRoles = CursorRole.GetStandardRoles();

                if (installedPaths != null && installedPaths.Length >= 15)
                {
                    for (int i = 0; i < Math.Min(installedPaths.Length, standardRoles.Count); i++)
                    {
                        var role = standardRoles[i];
                        key.SetValue(role.RegistryKey, installedPaths[i] ?? string.Empty, RegistryValueKind.String);
                    }
                }
                else
                {
                    using RegistryKey? schemeKey = Registry.CurrentUser.OpenSubKey(SchemesSubKey, false);
                    object? regVal = schemeKey?.GetValue(schemeName);
                    if (regVal is string regStr)
                    {
                        var paths = regStr.Split(',');
                        for (int i = 0; i < Math.Min(paths.Length, standardRoles.Count); i++)
                        {
                            var role = standardRoles[i];
                            key.SetValue(role.RegistryKey, paths[i] ?? string.Empty, RegistryValueKind.String);
                        }
                    }
                }

                // Broadcast change to Windows system
                bool spiResult = SystemParametersInfo(SPI_SETCURSORS, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                if (logMessages != null)
                {
                    logMessages.Add($"[情報] スキーム '{schemeName}' をWindowsの現在のカーソルとして適用しました。");
                }

                return spiResult;
            }
            catch (Exception ex)
            {
                if (logMessages != null)
                {
                    logMessages.Add($"[エラー] カーソル適用失敗: {ex.Message}");
                }
                return false;
            }
        }

        public static bool DeleteScheme(string schemeName, List<string> logMessages)
        {
            if (string.IsNullOrWhiteSpace(schemeName)) return false;

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SchemesSubKey, true);
                if (key != null)
                {
                    key.DeleteValue(schemeName, false);
                    logMessages.Add($"[成功] レジストリからスキーム '{schemeName}' を削除しました。");
                }

                string sanitizedSchemeName = SanitizeFileName(schemeName);
                string schemeStorageDir = Path.Combine(StorageDirectory, sanitizedSchemeName);

                if (Directory.Exists(schemeStorageDir))
                {
                    Directory.Delete(schemeStorageDir, true);
                    logMessages.Add($"[成功] スキーム保存フォルダを削除しました: {schemeStorageDir}");
                }

                // If deleted scheme was current active, reset cursor scheme to default
                if (GetCurrentActiveSchemeName().Equals(schemeName, StringComparison.OrdinalIgnoreCase))
                {
                    ResetSystemCursorsToDefault(logMessages);
                }

                return true;
            }
            catch (Exception ex)
            {
                logMessages.Add($"[エラー] スキーム '{schemeName}' の削除中にエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        public static void ResetSystemCursorsToDefault(List<string> logMessages)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(CursorsSubKey, true);
                if (key != null)
                {
                    key.SetValue(null, "Windows Standard", RegistryValueKind.String);
                    key.SetValue("Scheme Source", "0", RegistryValueKind.String);

                    foreach (var role in CursorRole.GetStandardRoles())
                    {
                        key.SetValue(role.RegistryKey, string.Empty, RegistryValueKind.String);
                    }
                }

                SystemParametersInfo(SPI_SETCURSORS, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                logMessages.Add("[情報] システムのカーソル設定を標準に戻しました。");
            }
            catch (Exception ex)
            {
                logMessages.Add($"[エラー] カーソルリセットエラー: {ex.Message}");
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Concat(fileName.Select(c => invalidChars.Contains(c) ? '_' : c)).Trim();
        }
    }
}

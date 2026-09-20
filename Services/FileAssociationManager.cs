using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Curio.Services
{
    public static class FileAssociationManager
    {
        public static bool RegisterFileAssociations(List<string>? logMessages = null)
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    logMessages?.Add("[エラー] 実行ファイルのパスが取得できませんでした。");
                    return false;
                }

                string progId = "Curio.CursorFile";
                string openCommand = $"\"{exePath}\" \"%1\"";

                using (RegistryKey? progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    if (progIdKey != null)
                    {
                        progIdKey.SetValue("", "Curio Cursor File");
                        using (RegistryKey shellKey = progIdKey.CreateSubKey(@"shell\open\command"))
                        {
                            shellKey.SetValue("", openCommand);
                        }
                    }
                }

                string[] extensions = { ".cur", ".ani", ".zip" };
                foreach (string ext in extensions)
                {
                    using RegistryKey? extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}");
                    if (extKey != null)
                    {
                        using RegistryKey openWithKey = extKey.CreateSubKey(@"OpenWithProgids");
                        openWithKey.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
                    }
                }

                CreateSendToShortcut(exePath);

                logMessages?.Add("[成功] ファイル関連付けおよび SendTo ショートカットを登録しました。");
                return true;
            }
            catch (Exception ex)
            {
                logMessages?.Add($"[エラー] ファイル関連付け登録失敗: {ex.Message}");
                return false;
            }
        }

        public static void CreateSendToShortcut(string exePath)
        {
            try
            {
                string sendToFolder = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
                if (Directory.Exists(sendToFolder))
                {
                    string shortcutPath = Path.Combine(sendToFolder, "Curio.lnk");
                    CreateShortcut(shortcutPath, exePath, "Curio で開く");
                }
            }
            catch { }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string description)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = targetPath;
                        shortcut.Description = description;
                        shortcut.Save();
                    }
                }
            }
            catch { }
        }
    }
}

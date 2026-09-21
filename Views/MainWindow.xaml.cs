using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Curio.Models;
using Curio.Services;
using Curio.Views.PaintEditor;
using System.Runtime.InteropServices;

namespace Curio.Views

{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings = App.Settings ?? AppSettings.Load();
        private bool _isInitializingControls = true;

        public ObservableCollection<CursorFileInfo> ScannedFiles { get; } = new();
        public ObservableCollection<CursorRoleMapping> RoleMappings { get; } = new();
        public ObservableCollection<InstalledSchemeInfo> InstalledSchemes { get; } = new();

        private ScanResult? _currentScanResult;
        private bool _isWebViewInitialized;
        private PaintEditorView? _paintEditorView;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            WebUrlTextBox.Text = _settings.DefaultUrl;
            SettingsDefaultUrlTextBox.Text = _settings.DefaultUrl;
            if (!string.IsNullOrWhiteSpace(_settings.StorageDirectory) && Path.IsPathFullyQualified(_settings.StorageDirectory))
                RegistrySchemeManager.StorageDirectory = _settings.StorageDirectory;
            StoragePathTextBox.Text = RegistrySchemeManager.StorageDirectory;
            InstalledSchemesListView.ItemsSource = InstalledSchemes;

            InitializeSettingsControls();

            InitializeRoleMappings();
            LoadInstalledSchemes();

            _isInitializingControls = false;

            AppendLog("Curio アプリケーションを起動しました。");
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);

        private void SetTitleBarDarkMode(bool enabled)
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            int useDarkMode = enabled ? 1 : 0;

            DwmSetWindowAttribute(
                hwnd,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDarkMode,
                sizeof(int));
        }

        private void InitializeSettingsControls()
        {
            // Set initial Theme selection
            if (string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase))
                ThemeComboBox.SelectedIndex = 1;
            else if (string.Equals(_settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase))
                ThemeComboBox.SelectedIndex = 2;
            else
                ThemeComboBox.SelectedIndex = 0; // System

            // Set initial UIStyle selection
            if (string.Equals(_settings.UIStyle, "XP", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_settings.UIStyle, "Windows XP風", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_settings.UIStyle, "OLD", StringComparison.OrdinalIgnoreCase))
                UIStyleComboBox.SelectedIndex = 1; // XP
            else
                UIStyleComboBox.SelectedIndex = 0; // Modern

            LanguageComboBox.SelectedIndex = string.Equals(_settings.Language, LocalizationService.English, StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

            UpdateTitleBarTheme();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitleBarTheme();

            await InitializeWebViewAsync();

            // Process startup file arguments (Open With, Drag & Drop to EXE, Command Line)
            if (App.StartupFilePaths.Count > 0)
            {
                AppendLog($"[起動引数] {App.StartupFilePaths.Count} 個の入力パスを処理中...");
                ProcessIncomingFiles(App.StartupFilePaths, isWebDownload: false);
            }
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (_isWebViewInitialized && WebViewControl.CoreWebView2 != null)
            {
                WebViewControl.CoreWebView2.DownloadStarting -= CoreWebView2_DownloadStarting;
                WebViewControl.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
            }

            WebViewControl.Dispose();
            ImportService.CleanTempExtracts();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                await WebViewControl.EnsureCoreWebView2Async();
                _isWebViewInitialized = true;

                WebViewControl.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                WebViewControl.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;

                AppendLog("[Webブラウザ] WebView2 の初期化が完了しました。");
            }
            catch (Exception ex)
            {
                AppendLog($"[Webブラウザエラー] WebView2の初期化に失敗しました: {ex.Message}");
            }
        }

        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (_isWebViewInitialized && WebViewControl.Source != null)
            {
                WebUrlTextBox.Text = WebViewControl.Source.ToString();
            }
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string downloadsDir = ImportService.GetDownloadsDirectory();
            string filename = Path.GetFileName(e.ResultFilePath);
            if (string.IsNullOrEmpty(filename)) filename = $"cursor_{DateTime.Now.Ticks}.cur";

            string targetPath = Path.Combine(downloadsDir, filename);

            e.ResultFilePath = targetPath;
            AppendLog($"[Webダウンロード開始] {filename} -> {targetPath}");

            var downloadOp = e.DownloadOperation;
            downloadOp.StateChanged += (s, args) =>
            {
                if (downloadOp.State == CoreWebView2DownloadState.Completed)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLog($"[Webダウンロード完了] {filename}");
                        ProcessIncomingFiles(new[] { targetPath }, isWebDownload: true);
                    });
                }
                else if (downloadOp.State == CoreWebView2DownloadState.Interrupted)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLog($"[Webダウンロード失敗] {filename} (理由: {downloadOp.InterruptReason})");
                    });
                }
            };
        }

        #region Navigation Bar Handlers

        private void WebNavBack_Click(object sender, RoutedEventArgs e)
        {
            if (_isWebViewInitialized && WebViewControl.CanGoBack)
                WebViewControl.GoBack();
        }

        private void WebNavForward_Click(object sender, RoutedEventArgs e)
        {
            if (_isWebViewInitialized && WebViewControl.CanGoForward)
                WebViewControl.GoForward();
        }

        private void WebNavRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_isWebViewInitialized)
                WebViewControl.Reload();
        }

        private void WebNavHome_Click(object sender, RoutedEventArgs e)
        {
            string url = _settings.DefaultUrl;
            WebUrlTextBox.Text = url;
            NavigateWebUrl(url);
        }

        private void WebNavSetDefault_Click(object sender, RoutedEventArgs e)
        {
            string currentUrl = WebUrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(currentUrl)) return;

            _settings.DefaultUrl = currentUrl;
            _settings.Save();

            SettingsDefaultUrlTextBox.Text = currentUrl;

            MessageBox.Show(
                $"デフォルトURLを以下に変更しました:\n{currentUrl}",
                "設定保存",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            AppendLog($"[設定] デフォルトURLを変更: {currentUrl}");
        }

        private void WebNavGo_Click(object sender, RoutedEventArgs e)
        {
            NavigateWebUrl(WebUrlTextBox.Text);
        }

        private void WebUrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateWebUrl(WebUrlTextBox.Text);
            }
        }

        private void NavigateWebUrl(string url)
        {
            if (!_isWebViewInitialized) return;

            string target = url.Trim();
            if (string.IsNullOrWhiteSpace(target)) return;

            if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                target = "https://" + target;
            }

            try
            {
                WebViewControl.Source = new Uri(target);
            }
            catch (Exception ex)
            {
                AppendLog($"[Webナビゲーションエラー] 無効なURL: {target} ({ex.Message})");
            }
        }

        #endregion

        #region Drag & Drop Handlers

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DragDropOverlay.Visibility = Visibility.Visible;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? droppedPaths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (droppedPaths != null && droppedPaths.Length > 0)
                {
                    AppendLog($"[ドラッグ＆ドロップ] {droppedPaths.Length} 件のアイテムを受信しました。");
                    ProcessIncomingFiles(droppedPaths, isWebDownload: false);
                }
            }
        }

        #endregion

        #region Unified Import Pipeline

        private void ClearDetectedFilesButton_Click(object sender, RoutedEventArgs e)
{
    ScannedFiles.Clear();
    ScanSummaryTextBlock.Text = "フォルダ選択またはドラッグ＆ドロップしてください";
}

        private void ProcessIncomingFiles(IEnumerable<string> paths, bool isWebDownload)
        {
            var result = ImportService.ProcessImport(paths);

            foreach (var log in result.LogMessages)
            {
                AppendLog(log);
            }

            if (result.ImportedFiles.Count > 0)
            {
                foreach (var item in result.ImportedFiles)
                {
                    if (!ScannedFiles.Any(f => f.FilePath.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        ScannedFiles.Add(item);
                    }
                }

                DetectedFilesListBox.ItemsSource = ScannedFiles;
                ScanSummaryTextBlock.Text = $"インポート検出: 全 {ScannedFiles.Count} 件のファイル";

                AutoMatchRoles(ScannedFiles.ToList());

                if (isWebDownload)
                {
                    WebToastTitleTextBlock.Text = $"カーソルファイルを {result.ImportedFiles.Count} 個検出しました！";
                    WebToastNotification.Visibility = Visibility.Visible;
                }
                else
                {
                    MainTabControl.SelectedIndex = 0; // Batch Installer tab
                    MessageBox.Show(LocalizationService.Format("ImportComplete", result.ImportedFiles.Count), LocalizationService.Get("ImportCompleteTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                if (result.ErrorCount > 0 || result.SkippedExecutablesCount > 0)
                {
                    MessageBox.Show(LocalizationService.Get("ImportWarning"), LocalizationService.Get("ImportWarningTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void WebToastOpen_Click(object sender, RoutedEventArgs e)
        {
            WebToastNotification.Visibility = Visibility.Collapsed;
            MainTabControl.SelectedIndex = 0; // Switch to Batch Installer tab
        }

        private void WebToastClose_Click(object sender, RoutedEventArgs e)
        {
            WebToastNotification.Visibility = Visibility.Collapsed;
        }

        private void RegisterAssociations_Click(object sender, RoutedEventArgs e)
        {
            var logs = new List<string>();
            bool success = FileAssociationManager.RegisterFileAssociations(logs);
            foreach (var l in logs) AppendLog(l);

            if (success)
            {
                MessageBox.Show(LocalizationService.Get("AssociationComplete"), LocalizationService.Get("AssociationCompleteTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(LocalizationService.Get("AssociationFailed"), LocalizationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Paint Editor

        private void CreatePaintEditor_Click(object sender, RoutedEventArgs e)
        {
            ShowPaintEditor(new PaintEditorView(), "新しいカーソル作成画面を開きました。");
        }

        private void OpenPaintEditor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "カーソルファイル (*.cur;*.ani)|*.cur;*.ani",
                Title = "編集するCUR/ANIファイルを開く"
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                ShowPaintEditor(
                    CreatePaintEditorForFile(dialog.FileName),
                    $"カーソルを開きました: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.Format("CursorOpenError", ex.Message),
                    LocalizationService.Get("Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void EditSelectedPaintEditor_Click(object sender, RoutedEventArgs e)
        {
            if (DetectedFilesListBox.SelectedItem is not CursorFileInfo selectedFile)
            {
                MessageBox.Show(this, LocalizationService.Get("NoCursorSelection"), LocalizationService.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if ((!string.Equals(selectedFile.Extension, ".cur", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(selectedFile.Extension, ".ani", StringComparison.OrdinalIgnoreCase)) ||
                !File.Exists(selectedFile.FilePath))
            {
                MessageBox.Show(this, LocalizationService.Get("NoEditableCursor"), LocalizationService.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ShowPaintEditor(
                    CreatePaintEditorForFile(selectedFile.FilePath),
                    $"選択中のカーソルを開きました: {selectedFile.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, LocalizationService.Format("CursorOpenError", ex.Message), LocalizationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static PaintEditorView CreatePaintEditorForFile(string filePath)
        {
            if (string.Equals(Path.GetExtension(filePath), ".ani", StringComparison.OrdinalIgnoreCase))
            {
                AniCursorData animation = AniCursorReader.Read(filePath);
                return new PaintEditorView(animation.Frames, animation.FrameDelaysMs, filePath);
            }

            return new PaintEditorView(CursorCanvasService.Read(filePath), filePath);
        }

        private void ShowPaintEditor(PaintEditorView editor, string logMessage)
        {
            if (_paintEditorView != null)
                HidePaintEditor();

            _paintEditorView = editor;
            _paintEditorView.BackRequested += PaintEditorView_BackRequested;
            _paintEditorView.Saved += PaintEditorView_Saved;
            PaintEditorHost.Content = _paintEditorView;

            MainTabControl.Visibility = Visibility.Collapsed;
            PaintEditorHost.Visibility = Visibility.Visible;
            LogRowDefinition.Height = new GridLength(0);
            MainSplitter.Visibility = Visibility.Collapsed;
            AppendLog($"[カーソル作成] {logMessage}");
        }

        private void PaintEditorView_Saved(object? sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_paintEditorView?.SavedFilePath))
            {
                AppendLog($"[カーソル作成] {_paintEditorView.SavedFilePath}");
                AppendLog("[カーソル作成] カーソルファイルを保存しました。");

                CursorFileInfo? editedFile = ScannedFiles.FirstOrDefault(file =>
                    string.Equals(file.FilePath, _paintEditorView.SavedFilePath, StringComparison.OrdinalIgnoreCase));
                if (editedFile != null)
                {
                    editedFile.Preview = CursorPreviewRenderer.CreatePreview(editedFile.FilePath);
                    DetectedFilesListBox.Items.Refresh();
                }
            }
        }

        private void PaintEditorView_BackRequested(object? sender, EventArgs e)
        {
            HidePaintEditor();
        }

        private void HidePaintEditor()
        {
            if (_paintEditorView != null)
            {
                _paintEditorView.BackRequested -= PaintEditorView_BackRequested;
                _paintEditorView.Saved -= PaintEditorView_Saved;
            }

            PaintEditorHost.Content = null;
            _paintEditorView = null;
            PaintEditorHost.Visibility = Visibility.Collapsed;
            MainTabControl.Visibility = Visibility.Visible;
            LogRowDefinition.Height = new GridLength(170);
            MainSplitter.Visibility = Visibility.Visible;
        }

        #endregion

        #region Settings Event Handlers

        private void SettingsSaveDefaultUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = SettingsDefaultUrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            _settings.DefaultUrl = url;
            _settings.Save();

            WebUrlTextBox.Text = url;

            MessageBox.Show(
                $"デフォルトURLを以下に変更しました:\n{url}",
                "設定保存",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            AppendLog($"[設定] デフォルトURLを変更: {url}");
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingControls) return;

            string selectedTheme = ThemeComboBox.SelectedIndex switch
            {
                1 => "Light",
                2 => "Dark",
                _ => "System"
            };

            _settings.Theme = selectedTheme;
            _settings.Save();

            StyleManager.Apply(_settings.UIStyle, _settings.Theme, _settings.Language);
            UpdateTitleBarTheme();
            AppendLog($"[設定] テーマを変更: {selectedTheme}");
        }

        private bool IsWindowsDarkMode()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            object? value = key?.GetValue("AppsUseLightTheme");

            return value is int intValue && intValue == 0;
        }
        private void UpdateTitleBarTheme()
        {
            bool isDark = string.Equals(
                _settings.Theme,
                "Dark",
                StringComparison.OrdinalIgnoreCase);

            if (string.Equals(_settings.Theme, "System", StringComparison.OrdinalIgnoreCase))
            {
                isDark = IsWindowsDarkMode();
            }

            SetTitleBarDarkMode(isDark);
        }
        private void UIStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingControls) return;

            string selectedStyle = UIStyleComboBox.SelectedIndex switch
            {
                1 => "XP",
                _ => "Modern"
            };

            _settings.UIStyle = selectedStyle;
            _settings.Save();

            StyleManager.Apply(_settings.UIStyle, _settings.Theme, _settings.Language);
            UpdateTitleBarTheme();
            AppendLog($"[設定] UIスタイルを変更: {selectedStyle}");
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingControls) return;

            _settings.Language = LanguageComboBox.SelectedIndex == 1
                ? LocalizationService.English
                : LocalizationService.Japanese;
            _settings.Save();
            StyleManager.Apply(_settings.UIStyle, _settings.Theme, _settings.Language);
            InitializeRoleMappings(preserveAssignments: true);
            Title = LocalizationService.Get("WindowTitle", "Curio");
        }

        private void Donate_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://ko-fi.com/ford614");
        }

        private void SupportEmail_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/issues");
        }

        private void GitHub_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser("https://github.com/Ford614/Curio");
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        #endregion

        #region Original Batch Installer & Scheme Management Logic

        private void InitializeRoleMappings(bool preserveAssignments = false)
        {
            Dictionary<string, CursorFileInfo?> existingAssignments = preserveAssignments
                ? RoleMappings.ToDictionary(mapping => mapping.Role.RegistryKey, mapping => mapping.SelectedFile, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, CursorFileInfo?>(StringComparer.OrdinalIgnoreCase);
            RoleMappings.Clear();
            foreach (var role in CursorRole.GetStandardRoles())
            {
                role.DisplayName = LocalizationService.GetRoleDisplayName(role.RegistryKey, role.DisplayName);
                role.Description = LocalizationService.GetRoleDescription(role.RegistryKey, role.Description);
                var mapping = new CursorRoleMapping(role);
                if (existingAssignments.TryGetValue(role.RegistryKey, out CursorFileInfo? selectedFile))
                    mapping.SelectedFile = selectedFile;
                RoleMappings.Add(mapping);
            }
            RoleMappingDataGrid.ItemsSource = RoleMappings;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "カーソルファイル (.cur / .ani) が入ったフォルダを選択",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    FolderPathTextBox.Text = dialog.FolderName;
                    ExecuteScan(dialog.FolderName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"フォルダ選択ダイアログの表示中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "カーソルスキームの保存先フォルダを選択",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    StoragePathTextBox.Text = dialog.FolderName;
                    RegistrySchemeManager.StorageDirectory = dialog.FolderName;
                    _settings.StorageDirectory = dialog.FolderName;
                    _settings.Save();
                    AppendLog($"[設定] 保存先フォルダを '{dialog.FolderName}' に変更しました。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"フォルダ選択ダイアログの表示中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            string defaultPath = RegistrySchemeManager.GetDefaultStorageDirectory();
            StoragePathTextBox.Text = defaultPath;
            RegistrySchemeManager.StorageDirectory = defaultPath;
            _settings.StorageDirectory = defaultPath;
            _settings.Save();
            AppendLog($"[設定] 保存先フォルダをデフォルト ({defaultPath}) にリセットしました。");
        }

        private void StoragePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string newPath = StoragePathTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newPath))
            {
                RegistrySchemeManager.StorageDirectory = newPath;
            }
        }

        private void StoragePathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string newPath = StoragePathTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newPath) && Path.IsPathFullyQualified(newPath))
            {
                _settings.StorageDirectory = newPath;
                _settings.Save();
            }
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = FolderPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                MessageBox.Show("フォルダパスを入力してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExecuteScan(folderPath);
        }

        private void ExecuteScan(string folderPath)
        {
            bool includeSubfolders = SubfoldersCheckBox.IsChecked ?? true;
            _currentScanResult = CursorScanner.ScanDirectory(folderPath, includeSubfolders);

            foreach (var log in _currentScanResult.LogMessages)
            {
                AppendLog(log);
            }

            ScannedFiles.Clear();
            foreach (var file in _currentScanResult.AllFiles)
            {
                ScannedFiles.Add(file);
            }
            DetectedFilesListBox.ItemsSource = ScannedFiles;

            string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(folderName))
            {
                SchemeNameTextBox.Text = folderName;
            }

            if (_currentScanResult.GroupedByFolder.Count > 1)
            {
                FolderGroupComboBox.ItemsSource = _currentScanResult.GroupedByFolder.ToList();
                FolderGroupComboBox.SelectedIndex = 0;
                FolderGroupComboBox.Visibility = Visibility.Visible;
                ScanSummaryTextBlock.Text = $"検出: 全 {_currentScanResult.AllFiles.Count} 件 ({_currentScanResult.GroupedByFolder.Count} フォルダ)";
            }
            else
            {
                FolderGroupComboBox.Visibility = Visibility.Collapsed;
                ScanSummaryTextBlock.Text = $"検出: {_currentScanResult.AllFiles.Count} 件のファイル";
            }

            AutoMatchRoles(ScannedFiles.ToList());
        }

        private void AutoMatchRoles_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = FolderPathTextBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                ExecuteScan(folderPath);
                return;
            }

            if (ScannedFiles.Count > 0)
            {
                AutoMatchRoles(ScannedFiles.ToList());
                AppendLog($"[自動割り当て] 既に読み込まれている {ScannedFiles.Count} 件のファイルから再割り当てを行いました。");
                return;
            }

            MessageBox.Show("フォルダパスを入力して「参照...」で選択するか、ファイルをドラッグ＆ドロップしてください。", "案内", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AutoMatchRoles(List<CursorFileInfo> filesToMatch)
        {
            var matchedList = CursorMatcher.MatchRoles(filesToMatch);
            for (int i = 0; i < Math.Min(RoleMappings.Count, matchedList.Count); i++)
            {
                RoleMappings[i].SelectedFile = matchedList[i].SelectedFile;
            }

            int assignedCount = RoleMappings.Count(m => m.IsAssigned);
            AppendLog($"[自動割り当て] {RoleMappings.Count} 個の標準役割のうち {assignedCount} 個にカーソルを割り当てました。");
        }

        private void FolderGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderGroupComboBox.SelectedItem is KeyValuePair<string, List<CursorFileInfo>> pair)
            {
                SchemeNameTextBox.Text = pair.Key;
                AutoMatchRoles(pair.Value);
            }
        }

        private void DetectedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void BrowseSingleFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CursorRoleMapping mapping)
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "カーソルファイル (*.cur;*.ani)|*.cur;*.ani|すべてのファイル (*.*)|*.*",
                    Title = $"[{mapping.Role.DisplayName}] 用のファイルを選択"
                };

                if (dialog.ShowDialog() == true)
                {
                    var preview = CursorPreviewRenderer.CreatePreview(dialog.FileName);
                    var item = new CursorFileInfo
                    {
                        FilePath = dialog.FileName,
                        FileName = Path.GetFileName(dialog.FileName),
                        Extension = Path.GetExtension(dialog.FileName).ToLowerInvariant(),
                        Preview = preview
                    };

                    if (!ScannedFiles.Any(f => f.FilePath.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        ScannedFiles.Add(item);
                    }

                    mapping.SelectedFile = item;
                }
            }
        }

        private void ClearMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CursorRoleMapping mapping)
            {
                mapping.SelectedFile = null;
            }
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            string schemeName = SchemeNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(schemeName))
            {
                MessageBox.Show(LocalizationService.Get("SchemeNameRequired", "スキーム名を入力してください。"), LocalizationService.Get("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int assignedCount = RoleMappings.Count(m => m.IsAssigned);
            if (assignedCount == 0)
            {
                MessageBox.Show(LocalizationService.Get("NoRoleAssigned"), LocalizationService.Get("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RegistrySchemeManager.SchemeExists(schemeName))
            {
                var overwriteDialog = new OverwriteDialog(schemeName) { Owner = this };
                if (overwriteDialog.ShowDialog() != true || overwriteDialog.ResultOption == OverwriteOption.Cancel)
                {
                    AppendLog("[中断] ユーザーによってインストール処理がキャンセルされました。");
                    return;
                }

                if (overwriteDialog.ResultOption == OverwriteOption.Rename)
                {
                    schemeName = overwriteDialog.ResultSchemeName;
                    SchemeNameTextBox.Text = schemeName;
                }
            }

            bool applyImmediately = ApplyImmediatelyCheckBox.IsChecked ?? true;

            AppendLog($"[開始] スキーム '{schemeName}' の一括インストールを実行中...");
            var result = RegistrySchemeManager.InstallScheme(schemeName, RoleMappings.ToList(), applyImmediately);

            foreach (var log in result.LogMessages)
            {
                AppendLog(log);
            }

            if (result.Success)
            {
                MessageBox.Show(LocalizationService.Format("SchemeInstalled", schemeName), LocalizationService.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                LoadInstalledSchemes();
            }
            else
            {
                MessageBox.Show(LocalizationService.Get("InstallFailed"), LocalizationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == MainTabControl && MainTabControl.SelectedIndex == 1)
            {
                LoadInstalledSchemes();
            }
        }

        private void LoadInstalledSchemes()
        {
            var currentSelectionName = (InstalledSchemesListView.SelectedItem as InstalledSchemeInfo)?.Name;

            InstalledSchemes.Clear();
            var schemes = RegistrySchemeManager.GetInstalledSchemes();
            foreach (var s in schemes)
            {
                InstalledSchemes.Add(s);
            }

            if (!string.IsNullOrEmpty(currentSelectionName))
            {
                var reselectItem = InstalledSchemes.FirstOrDefault(s => s.Name.Equals(currentSelectionName, StringComparison.OrdinalIgnoreCase));
                if (reselectItem != null)
                {
                    InstalledSchemesListView.SelectedItem = reselectItem;
                }
            }
        }

        private void RefreshInstalledSchemes_Click(object sender, RoutedEventArgs e)
        {
            LoadInstalledSchemes();
            AppendLog("[情報] レジストリの登録済みスキーム一覧を更新しました。");
        }

        private void InstalledSchemesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = InstalledSchemesListView.SelectedItem != null;
            ApplySelectedSchemeButton.IsEnabled = hasSelection;
            DeleteSelectedSchemeButton.IsEnabled = hasSelection;
        }

        private void ApplySelectedScheme_Click(object sender, RoutedEventArgs e)
        {
            if (InstalledSchemesListView.SelectedItem is InstalledSchemeInfo scheme)
            {
                var logs = new List<string>();
                bool success = RegistrySchemeManager.ApplySchemeToSystem(scheme.Name, null, null, logs);
                foreach (var l in logs) AppendLog(l);

                if (success)
                {
                    MessageBox.Show(LocalizationService.Format("SchemeApplied", scheme.Name), LocalizationService.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadInstalledSchemes();
                }
            }
        }

        private void DeleteSelectedScheme_Click(object sender, RoutedEventArgs e)
        {
            if (InstalledSchemesListView.SelectedItem is InstalledSchemeInfo scheme)
            {
                var confirm = MessageBox.Show(LocalizationService.Format("DeleteConfirm", scheme.Name), LocalizationService.Get("Warning"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    var logs = new List<string>();
                    bool success = RegistrySchemeManager.DeleteScheme(scheme.Name, logs);
                    foreach (var l in logs) AppendLog(l);

                    if (success)
                    {
                        MessageBox.Show(LocalizationService.Format("SchemeDeleted", scheme.Name), LocalizationService.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadInstalledSchemes();
                    }
                }
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void AppendLog(string message)
        {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timeStamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        #endregion
    }
}

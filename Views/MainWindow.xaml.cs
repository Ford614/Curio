using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Curio.Views
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<CursorFileInfo> ScannedFiles { get; } = new();
        public ObservableCollection<CursorRoleMapping> RoleMappings { get; } = new();
        public ObservableCollection<InstalledSchemeInfo> InstalledSchemes { get; } = new();

        private ScanResult? _currentScanResult;
        private bool _isWebViewInitialized;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            StoragePathTextBox.Text = RegistrySchemeManager.StorageDirectory;
            InstalledSchemesListView.ItemsSource = InstalledSchemes;

            InitializeRoleMappings();
            LoadInstalledSchemes();

            AppendLog("Curio アプリケーションを起動しました。");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();

            // Process startup file arguments (Open With, Drag & Drop to EXE, Command Line)
            if (App.StartupFilePaths.Count > 0)
            {
                AppendLog($"[起動引数] {App.StartupFilePaths.Count} 個の入力パスを処理中...");
                ProcessIncomingFiles(App.StartupFilePaths, isWebDownload: false);
            }
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
            string homeUrl = "https://www.rw-designer.com/cursor-library";
            WebUrlTextBox.Text = homeUrl;
            NavigateWebUrl(homeUrl);
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

        private void ProcessIncomingFiles(IEnumerable<string> paths, bool isWebDownload)
        {
            var result = ImportService.ProcessImport(paths);

            foreach (var log in result.LogMessages)
            {
                AppendLog(log);
            }

            if (result.ImportedFiles.Count > 0)
            {
                // Add newly imported items into ScannedFiles collection
                foreach (var item in result.ImportedFiles)
                {
                    if (!ScannedFiles.Any(f => f.FilePath.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        ScannedFiles.Add(item);
                    }
                }

                DetectedFilesListBox.ItemsSource = ScannedFiles;
                ScanSummaryTextBlock.Text = $"インポート検出: 全 {ScannedFiles.Count} 件のファイル";

                // Auto-match roles
                AutoMatchRoles(ScannedFiles.ToList());

                if (isWebDownload)
                {
                    WebToastTitleTextBlock.Text = $"カーソルファイルを {result.ImportedFiles.Count} 個検出しました！";
                    WebToastNotification.Visibility = Visibility.Visible;
                }
                else
                {
                    // Switch to Batch Installer Tab
                    MainTabControl.SelectedIndex = 1;
                    MessageBox.Show($"カーソルファイルを {result.ImportedFiles.Count} 個取り込みました！\n役割の割り当てを確認して一括インストールしてください。", "取り込み完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                if (result.ErrorCount > 0 || result.SkippedExecutablesCount > 0)
                {
                    MessageBox.Show($"カーソルファイルが見つからなかったか、セキュリティ除外されました。ログパネルをご確認ください。", "インポート通知", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void WebToastOpen_Click(object sender, RoutedEventArgs e)
        {
            WebToastNotification.Visibility = Visibility.Collapsed;
            MainTabControl.SelectedIndex = 1; // Switch to Batch Installer tab
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
                MessageBox.Show("Curio を .cur / .ani / .zip ファイルのプログラムおよび「送る」メニューに登録しました。", "関連付け登録完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("ファイル関連付けの登録に失敗しました。ログパネルをご確認ください。", "登録失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Original Batch Installer & Scheme Management Logic

        private void InitializeRoleMappings()
        {
            RoleMappings.Clear();
            foreach (var role in CursorRole.GetStandardRoles())
            {
                RoleMappings.Add(new CursorRoleMapping(role));
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
                MessageBox.Show("スキーム名を入力してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int assignedCount = RoleMappings.Count(m => m.IsAssigned);
            if (assignedCount == 0)
            {
                MessageBox.Show("少なくとも1つのカーソル役割にファイルを割り当ててください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"カーソルスキーム '{schemeName}' を登録しました！\nWindowsの「マウスのプロパティ -> ポインター」からいつでも選択できます。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadInstalledSchemes();
            }
            else
            {
                MessageBox.Show($"スキームの登録中にエラーが発生しました。ログをご確認ください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == MainTabControl && MainTabControl.SelectedIndex == 2)
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
                    MessageBox.Show($"スキーム '{scheme.Name}' を反映しました。", "適用完了", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadInstalledSchemes();
                }
            }
        }

        private void DeleteSelectedScheme_Click(object sender, RoutedEventArgs e)
        {
            if (InstalledSchemesListView.SelectedItem is InstalledSchemeInfo scheme)
            {
                var confirm = MessageBox.Show($"登録済みスキーム '{scheme.Name}' をアンインストールしますか？\n（レジストリ設定およびコピーされたファイルが削除されます）", "削除確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    var logs = new List<string>();
                    bool success = RegistrySchemeManager.DeleteScheme(scheme.Name, logs);
                    foreach (var l in logs) AppendLog(l);

                    if (success)
                    {
                        MessageBox.Show($"スキーム '{scheme.Name}' を削除しました。", "削除完了", MessageBoxButton.OK, MessageBoxImage.Information);
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

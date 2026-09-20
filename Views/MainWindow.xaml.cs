using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MouseCursorCustom.Models;
using MouseCursorCustom.Services;

namespace MouseCursorCustom.Views
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<CursorFileInfo> ScannedFiles { get; } = new();
        public ObservableCollection<CursorRoleMapping> RoleMappings { get; } = new();
        public ObservableCollection<InstalledSchemeInfo> InstalledSchemes { get; } = new();

        private ScanResult? _currentScanResult;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            StoragePathTextBox.Text = RegistrySchemeManager.StorageDirectory;
            InstalledSchemesListView.ItemsSource = InstalledSchemes;

            InitializeRoleMappings();
            LoadInstalledSchemes();

            AppendLog("アプリケーションを起動しました。フォルダを選択してスキャンを実行してください。");
        }

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

            // Auto-suggest scheme name from folder name
            string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(folderName))
            {
                SchemeNameTextBox.Text = folderName;
            }

            // Populate folder group combobox if multiple groups exist
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

            // Auto-match roles
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
            // Optional preview highlight
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

            // Check if scheme exists
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
            // CRITICAL FIX: Ignore routed SelectionChanged events coming from child controls (ListView, ComboBox, ListBox)
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
    }
}

using System.Windows;

namespace Curio.Views
{
    public enum OverwriteOption
    {
        Cancel,
        Overwrite,
        Rename
    }

    public partial class OverwriteDialog : Window
    {
        public OverwriteOption ResultOption { get; private set; } = OverwriteOption.Cancel;
        public string ResultSchemeName { get; private set; } = string.Empty;

        public OverwriteDialog(string existingSchemeName)
        {
            InitializeComponent();
            MessageTextBlock.Text = $"スキーム名 '{existingSchemeName}' は既に登録されています。\n動作を選択してください。";
            NewNameTextBox.Text = $"{existingSchemeName}_Copy";
            ResultSchemeName = existingSchemeName;
        }

        private void OverwriteButton_Click(object sender, RoutedEventArgs e)
        {
            ResultOption = OverwriteOption.Overwrite;
            DialogResult = true;
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            string newName = NewNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("新しいスキーム名を入力してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultOption = OverwriteOption.Rename;
            ResultSchemeName = newName;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ResultOption = OverwriteOption.Cancel;
            DialogResult = false;
        }
    }
}

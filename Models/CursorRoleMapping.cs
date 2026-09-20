using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Curio.Models
{
    public class CursorRoleMapping : INotifyPropertyChanged
    {
        private CursorFileInfo? _selectedFile;

        public CursorRole Role { get; }

        public CursorFileInfo? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (_selectedFile != value)
                {
                    _selectedFile = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Preview));
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(IsAssigned));
                }
            }
        }

        public ImageSource? Preview => SelectedFile?.Preview;

        public string FilePath => SelectedFile?.FilePath ?? string.Empty;

        public bool IsAssigned => !string.IsNullOrEmpty(FilePath);

        public CursorRoleMapping(CursorRole role)
        {
            Role = role;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

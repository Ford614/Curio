using System.Windows.Media;

namespace MouseCursorCustom.Models
{
    public class CursorFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public ImageSource? Preview { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(RelativePath) ? FileName : RelativePath;
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using Curio.Models;
using Curio.Services;
using Microsoft.Win32;
using Line = System.Windows.Shapes.Line;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Curio.Views.PaintEditor
{
    public partial class PaintEditorView : UserControl
    {
        private const double DisplaySize = 512d;
        private const int MaxHistory = 50;

        private readonly Stack<EditorSnapshot> _undoStack = new();
        private readonly Stack<EditorSnapshot> _redoStack = new();
        private readonly DispatcherTimer _previewTimer;
        private List<CursorCanvasImage> _frames = new();
        private List<int> _frameDelaysMs = new();
        private int _currentFrameIndex;
        private bool _isAnimation;
        private CursorCanvasImage _image;
        private WriteableBitmap? _previewBitmap;
        private Color _selectedColor = Colors.White;
        private int _brushSize = 1;
        private EditorTool _tool = EditorTool.Pencil;
        private bool _isPainting;
        private bool _isSettingHotspot;
        private bool _isDraggingHotspot;
        private bool _isSelecting;
        private bool _suppressTimelineSelection;
        private Point _selectionStartPoint;
        private Int32Rect? _selection;
        private byte[]? _clipboardBgra;
        private int _clipboardWidth;
        private int _clipboardHeight;
        private bool _hasUnsavedChanges;
        private string? _sourcePath;

        public PaintEditorView(CursorCanvasImage? initialImage = null, string? sourcePath = null)
        {
            InitializeComponent();

            _image = (initialImage ?? CreateBlankImage()).Clone();
            _frames.Add(_image.Clone());
            _frameDelaysMs.Add(100);
            _sourcePath = sourcePath;
            _previewTimer = new DispatcherTimer(DispatcherPriority.Render);
            _previewTimer.Tick += PreviewTimer_Tick;

            BrushSizeComboBox.SelectedIndex = 0;
            SelectCanvasSizeItem(_image.Width);
            UpdateColorControls();
            LoadImageState(_image, clearHistory: true);
        }

        public PaintEditorView(
            IReadOnlyList<CursorCanvasImage> frames,
            IReadOnlyList<int> frameDelaysMs,
            string? sourcePath = null)
        {
            InitializeComponent();

            if (frames.Count == 0)
                throw new ArgumentException("ANIには1つ以上のフレームが必要です。", nameof(frames));

            _frames = frames.Select(frame => frame.Clone()).ToList();
            _frameDelaysMs = NormalizeDelays(frameDelaysMs, _frames.Count);
            _currentFrameIndex = 0;
            _isAnimation = true;
            _image = _frames[0].Clone();
            _sourcePath = sourcePath;
            _previewTimer = new DispatcherTimer(DispatcherPriority.Render);
            _previewTimer.Tick += PreviewTimer_Tick;

            BrushSizeComboBox.SelectedIndex = 0;
            SelectCanvasSizeItem(_image.Width);
            UpdateColorControls();
            LoadImageState(_image, clearHistory: true);
            TimelinePanel.Visibility = Visibility.Visible;
            RefreshTimeline();
        }

        public string? SavedFilePath { get; private set; }
        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public event EventHandler? BackRequested;
        public event EventHandler? Saved;

        private enum EditorTool
        {
            Pencil,
            Eraser,
            Eyedropper,
            Fill,
            Select,
            Hotspot
        }

        private sealed class EditorSnapshot
        {
            public required List<CursorCanvasImage> Frames { get; init; }
            public required List<int> FrameDelaysMs { get; init; }
            public required int CurrentFrameIndex { get; init; }
            public required bool IsAnimation { get; init; }
        }

        private static CursorCanvasImage CreateBlankImage()
        {
            return new CursorCanvasImage(32, 32, 0, 0, new byte[32 * 32 * 4]);
        }

        private static CursorCanvasImage ResizeFrame(CursorCanvasImage source, int width, int height)
        {
            byte[] pixels = new byte[width * height * 4];
            int copyWidth = Math.Min(width, source.Width);
            int copyHeight = Math.Min(height, source.Height);
            for (int y = 0; y < copyHeight; y++)
            {
                Buffer.BlockCopy(
                    source.Bgra,
                    y * source.Width * 4,
                    pixels,
                    y * width * 4,
                    copyWidth * 4);
            }

            return new CursorCanvasImage(
                width,
                height,
                Math.Clamp(source.HotspotX, 0, width - 1),
                Math.Clamp(source.HotspotY, 0, height - 1),
                pixels);
        }

        private void LoadImageState(CursorCanvasImage image, bool clearHistory)
        {
            _image = image.Clone();
            if (_frames.Count == 0)
                _frames.Add(_image.Clone());
            _frames[_currentFrameIndex] = _image.Clone();
            _selection = null;
            _isSelecting = false;
            _isDraggingHotspot = false;
            TimelinePanel.Visibility = _isAnimation ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Content = LocalizationService.Get(_isAnimation ? "EditorSaveAni" : "EditorSave");

            if (clearHistory)
            {
                _undoStack.Clear();
                _redoStack.Clear();
                _hasUnsavedChanges = false;
            }

            HotspotXTextBox.Text = _image.HotspotX.ToString();
            HotspotYTextBox.Text = _image.HotspotY.ToString();
            SelectCanvasSizeItem(_image.Width);
            RenderCanvas();
            UpdateHistoryButtons();
            UpdateEditorStatus();
            RefreshTimelineSelection();
        }

        private void LoadAnimationState(AniCursorData animation, string sourcePath)
        {
            if (animation.Frames.Count == 0)
                throw new InvalidDataException("アニメーションにフレームがありません。");

            _previewTimer.Stop();
            _frames = animation.Frames.Select(frame => frame.Clone()).ToList();
            int commonWidth = _frames[0].Width;
            int commonHeight = _frames[0].Height;
            _frames = _frames
                .Select(frame => frame.Width == commonWidth && frame.Height == commonHeight
                    ? frame
                    : ResizeFrame(frame, commonWidth, commonHeight))
                .ToList();
            int commonHotspotX = Math.Clamp(_frames[0].HotspotX, 0, commonWidth - 1);
            int commonHotspotY = Math.Clamp(_frames[0].HotspotY, 0, commonHeight - 1);
            _frames = _frames
                .Select(frame => CursorHotspotService.WithHotspot(frame, commonHotspotX, commonHotspotY))
                .ToList();
            _frameDelaysMs = NormalizeDelays(animation.FrameDelaysMs, _frames.Count);
            _currentFrameIndex = 0;
            _isAnimation = true;
            _sourcePath = sourcePath;
            _image = _frames[0].Clone();
            TimelinePanel.Visibility = Visibility.Visible;
            LoadImageState(_image, clearHistory: true);
            RefreshTimeline();
            FooterTextBlock.Text = $"アニメーションを読み込みました: {_frames.Count}フレーム";
        }

        private EditorSnapshot CaptureSnapshot()
        {
            CommitCurrentFrame();
            return new EditorSnapshot
            {
                Frames = _frames.Select(frame => frame.Clone()).ToList(),
                FrameDelaysMs = new List<int>(_frameDelaysMs),
                CurrentFrameIndex = _currentFrameIndex,
                IsAnimation = _isAnimation
            };
        }

        private void PushHistory()
        {
            PushHistory(CaptureSnapshot());
        }

        private void PushHistory(EditorSnapshot snapshot)
        {
            _undoStack.Push(snapshot);
            while (_undoStack.Count > MaxHistory)
                _undoStack.RemoveBottom();

            _redoStack.Clear();
            _hasUnsavedChanges = true;
            UpdateHistoryButtons();
        }

        private void RestoreSnapshot(EditorSnapshot snapshot)
        {
            _frames = snapshot.Frames.Select(frame => frame.Clone()).ToList();
            _frameDelaysMs = NormalizeDelays(snapshot.FrameDelaysMs, _frames.Count);
            _currentFrameIndex = Math.Clamp(snapshot.CurrentFrameIndex, 0, _frames.Count - 1);
            _isAnimation = snapshot.IsAnimation;
            _image = _frames[_currentFrameIndex].Clone();

            HotspotXTextBox.Text = _image.HotspotX.ToString();
            HotspotYTextBox.Text = _image.HotspotY.ToString();
            SelectCanvasSizeItem(_image.Width);
            TimelinePanel.Visibility = _isAnimation ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Content = LocalizationService.Get(_isAnimation ? "EditorSaveAni" : "EditorSave");
            _selection = null;
            RenderCanvas();
            UpdateHistoryButtons();
            UpdateEditorStatus();
            RefreshTimeline();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0) return;
            _redoStack.Push(CaptureSnapshot());
            RestoreSnapshot(_undoStack.Pop());
            _hasUnsavedChanges = true;
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Push(CaptureSnapshot());
            RestoreSnapshot(_redoStack.Pop());
            _hasUnsavedChanges = true;
        }

        private void UpdateHistoryButtons()
        {
            if (UndoButton != null) UndoButton.IsEnabled = _undoStack.Count > 0;
            if (RedoButton != null) RedoButton.IsEnabled = _redoStack.Count > 0;
        }

        private void CommitCurrentFrame()
        {
            if (_frames.Count == 0 || _currentFrameIndex < 0 || _currentFrameIndex >= _frames.Count) return;
            _frames[_currentFrameIndex] = _image.Clone();
        }

        private static List<int> NormalizeDelays(IReadOnlyList<int> delays, int count)
        {
            var normalized = new List<int>(count);
            for (int i = 0; i < count; i++)
                normalized.Add(Math.Clamp(i < delays.Count ? delays[i] : 100, 10, 60000));
            return normalized;
        }

        private void NormalizeFrameHotspots()
        {
            CommitCurrentFrame();
            if (_frames.Count == 0) return;

            int hotspotX = Math.Clamp(_frames[0].HotspotX, 0, _frames[0].Width - 1);
            int hotspotY = Math.Clamp(_frames[0].HotspotY, 0, _frames[0].Height - 1);
            for (int i = 0; i < _frames.Count; i++)
            {
                CursorCanvasImage frame = _frames[i];
                _frames[i] = CursorHotspotService.WithHotspot(frame, hotspotX, hotspotY);
            }

            _image = _frames[_currentFrameIndex].Clone();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            _previewTimer.Stop();
            _sourcePath = null;
            SavedFilePath = null;
            _isAnimation = false;
            _currentFrameIndex = 0;
            _frames = new List<CursorCanvasImage> { CreateBlankImage() };
            _frameDelaysMs = new List<int> { 100 };
            TimelinePanel.Visibility = Visibility.Collapsed;
            SaveButton.Content = LocalizationService.Get("EditorSave");
            LoadImageState(_frames[0], clearHistory: true);
            FooterTextBlock.Text = "新しい32×32キャンバスを作成しました。";
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "画像・カーソル (*.cur;*.ani;*.png;*.gif)|*.cur;*.ani;*.png;*.gif|カーソルファイル (*.cur;*.ani)|*.cur;*.ani|PNG/GIF画像 (*.png;*.gif)|*.png;*.gif",
                Title = "CUR、ANI、PNGまたはGIFを開く"
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (extension == ".ani")
                {
                    LoadAnimationState(AniCursorReader.Read(dialog.FileName), dialog.FileName);
                }
                else if (extension == ".gif")
                {
                    LoadAnimationState(GifAnimationReader.Read(dialog.FileName), string.Empty);
                }
                else
                {
                    bool isPng = extension == ".png";
                    CursorCanvasImage image = isPng
                        ? ReadPngImage(dialog.FileName)
                        : CursorCanvasService.Read(dialog.FileName);

                    _previewTimer.Stop();
                    _isAnimation = false;
                    _currentFrameIndex = 0;
                    _frames = new List<CursorCanvasImage> { image.Clone() };
                    _frameDelaysMs = new List<int> { 100 };
                    TimelinePanel.Visibility = Visibility.Collapsed;
                    LoadImageState(image, clearHistory: true);
                    _sourcePath = isPng ? null : dialog.FileName;
                }
                SavedFilePath = null;
                if (extension == ".png")
                    FooterTextBlock.Text = $"PNGを読み込みました: {Path.GetFileName(dialog.FileName)}";
                else if (extension == ".gif")
                    FooterTextBlock.Text = $"GIFを読み込みました: {_frames.Count}フレーム";
                else if (extension == ".cur")
                    FooterTextBlock.Text = $"開きました: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), $"カーソルファイルを開けませんでした。\n{ex.Message}", "読み込みエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static CursorCanvasImage ReadPngImage(string filePath)
        {
            var bitmap = new BitmapImage();
            using (FileStream stream = File.OpenRead(filePath))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
            }

            if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0 || bitmap.PixelWidth > 256 || bitmap.PixelHeight > 256)
                throw new InvalidDataException("PNG画像は1〜256ピクセルの範囲で読み込んでください。");

            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            converted.Freeze();

            int stride = converted.PixelWidth * 4;
            byte[] pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            return new CursorCanvasImage(converted.PixelWidth, converted.PixelHeight, 0, 0, pixels);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            TrySave();
        }

        private bool TrySave()
        {
            CommitCurrentFrame();
            var dialog = new SaveFileDialog
            {
                Filter = _isAnimation
                    ? "アニメーションカーソル (*.ani)|*.ani"
                    : "カーソルファイル (*.cur)|*.cur",
                DefaultExt = _isAnimation ? ".ani" : ".cur",
                AddExtension = true,
                FileName = string.IsNullOrWhiteSpace(_sourcePath)
                    ? (_isAnimation ? "cursor.ani" : "cursor.cur")
                    : Path.GetFileName(_sourcePath),
                Title = _isAnimation ? "ANIファイルを保存" : "CURファイルを保存"
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return false;

            try
            {
                if (_isAnimation)
                {
                    NormalizeFrameHotspots();
                    AniCursorWriter.Write(dialog.FileName, _frames, _frameDelaysMs);
                }
                else
                {
                    CursorCanvasService.Write(dialog.FileName, _image);
                }
                _sourcePath = dialog.FileName;
                SavedFilePath = dialog.FileName;
                _hasUnsavedChanges = false;
                FooterTextBlock.Text = $"保存しました: {Path.GetFileName(dialog.FileName)}";
                RefreshTimeline();
                Saved?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), $"カーソルファイルを保存できませんでした。\n{ex.Message}", "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                MessageBoxResult result = MessageBox.Show(
                    Window.GetWindow(this),
                    "未保存の変更があります。\n保存して戻りますか？",
                    "カーソル編集",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes && !TrySave()) return;
            }

            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Pencil_Click(object sender, RoutedEventArgs e)
        {
            _tool = EditorTool.Pencil;
            _isSettingHotspot = false;
            UpdateToolStatus("ペン");
        }

        private void Eraser_Click(object sender, RoutedEventArgs e)
        {
            _tool = EditorTool.Eraser;
            _isSettingHotspot = false;
            UpdateToolStatus("消しゴム");
        }

        private void Eyedropper_Click(object sender, RoutedEventArgs e)
        {
            _tool = EditorTool.Eyedropper;
            _isSettingHotspot = false;
            UpdateToolStatus("スポイト");
        }

        private void Fill_Click(object sender, RoutedEventArgs e)
        {
            _tool = EditorTool.Fill;
            _isSettingHotspot = false;
            UpdateToolStatus("塗りつぶし");
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            _tool = EditorTool.Select;
            _isSettingHotspot = false;
            UpdateToolStatus("選択");
        }

        private void FlipHorizontal_Click(object sender, RoutedEventArgs e)
        {
            FlipSelection(horizontal: true);
        }

        private void FlipVertical_Click(object sender, RoutedEventArgs e)
        {
            FlipSelection(horizontal: false);
        }

        private void CopySelection_Click(object sender, RoutedEventArgs e)
        {
            if (_selection is not Int32Rect selection)
            {
                FooterTextBlock.Text = "コピーする選択範囲がありません。";
                return;
            }

            _clipboardWidth = selection.Width;
            _clipboardHeight = selection.Height;
            _clipboardBgra = CopyPixels(selection);
            FooterTextBlock.Text = $"選択範囲をコピーしました: {_clipboardWidth}×{_clipboardHeight}";
        }

        private void PasteSelection_Click(object sender, RoutedEventArgs e)
        {
            if (_clipboardBgra == null || _clipboardWidth <= 0 || _clipboardHeight <= 0)
            {
                FooterTextBlock.Text = "貼り付ける選択範囲がありません。";
                return;
            }

            int left = _selection?.X ?? 0;
            int top = _selection?.Y ?? 0;
            left = Math.Clamp(left, 0, Math.Max(0, _image.Width - _clipboardWidth));
            top = Math.Clamp(top, 0, Math.Max(0, _image.Height - _clipboardHeight));

            PushHistory();
            byte[] pixels = (byte[])_image.Bgra.Clone();
            for (int y = 0; y < _clipboardHeight && top + y < _image.Height; y++)
            {
                for (int x = 0; x < _clipboardWidth && left + x < _image.Width; x++)
                {
                    Buffer.BlockCopy(_clipboardBgra, (y * _clipboardWidth + x) * 4, pixels, ((top + y) * _image.Width + left + x) * 4, 4);
                }
            }

            _image = new CursorCanvasImage(_image.Width, _image.Height, _image.HotspotX, _image.HotspotY, pixels);
            _selection = new Int32Rect(left, top, Math.Min(_clipboardWidth, _image.Width - left), Math.Min(_clipboardHeight, _image.Height - top));
            RenderCanvas();
            FooterTextBlock.Text = "選択範囲を貼り付けました。";
        }

        private void DeleteSelection_Click(object sender, RoutedEventArgs e)
        {
            if (_selection is not Int32Rect selection)
            {
                FooterTextBlock.Text = "削除する選択範囲がありません。";
                return;
            }

            PushHistory();
            byte[] pixels = (byte[])_image.Bgra.Clone();
            for (int y = selection.Y; y < selection.Y + selection.Height; y++)
            {
                for (int x = selection.X; x < selection.X + selection.Width; x++)
                {
                    if (x < 0 || y < 0 || x >= _image.Width || y >= _image.Height) continue;
                    pixels[(y * _image.Width + x) * 4 + 3] = 0;
                }
            }

            _image = new CursorCanvasImage(_image.Width, _image.Height, _image.HotspotX, _image.HotspotY, pixels);
            RenderCanvas();
            FooterTextBlock.Text = "選択範囲を削除しました。";
        }

        private void SetHotspot_Click(object sender, RoutedEventArgs e)
        {
            _tool = EditorTool.Hotspot;
            _isSettingHotspot = true;
            UpdateToolStatus("Hotspot設定");
            FooterTextBlock.Text = "キャンバス上をクリックしてHotspotを設定します。";
        }

        private void UpdateToolStatus(string toolName)
        {
            FooterTextBlock.Text = $"現在のツール: {toolName}";
            SetHotspotButton.IsEnabled = true;
        }

        private void BrushSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BrushSizeComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int size))
                _brushSize = size;
        }

        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Background is SolidColorBrush brush)
            {
                _selectedColor = Color.FromArgb((byte)AlphaSlider.Value, brush.Color.R, brush.Color.G, brush.Color.B);
                UpdateColorControls();
            }
        }

        private void ApplyHexColor_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseColor(HexColorTextBox.Text, out Color color))
            {
                MessageBox.Show(Window.GetWindow(this), "色は #RRGGBB または #AARRGGBB 形式で入力してください。", "色の入力", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _selectedColor = color;
            AlphaSlider.Value = color.A;
            UpdateColorControls();
        }

        private void AlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            _selectedColor.A = (byte)Math.Clamp((int)e.NewValue, 0, 255);
            UpdateColorControls();
        }

        private void UpdateColorControls()
        {
            if (ColorPreview == null || HexColorTextBox == null) return;
            ColorPreview.Background = new SolidColorBrush(_selectedColor);
            HexColorTextBox.Text = $"#{_selectedColor.A:X2}{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
            if (AlphaSlider != null && Math.Abs(AlphaSlider.Value - _selectedColor.A) > 0.5)
                AlphaSlider.Value = _selectedColor.A;
        }

        private void ApplyHotspot_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(HotspotXTextBox.Text, out int x) || !int.TryParse(HotspotYTextBox.Text, out int y))
            {
                MessageBox.Show(Window.GetWindow(this), "Hotspotは整数で入力してください。", "Hotspot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetHotspot(x, y);
        }

        private void SetHotspot(int x, int y)
        {
            SetHotspotCore(x, y, recordHistory: true);
        }

        private void SetHotspotFromPointWithoutHistory(Point point)
        {
            int x = Math.Clamp((int)(point.X / DisplaySize * _image.Width), 0, _image.Width - 1);
            int y = Math.Clamp((int)(point.Y / DisplaySize * _image.Height), 0, _image.Height - 1);
            SetHotspotCore(x, y, recordHistory: false);
        }

        private void SetHotspotCore(int x, int y, bool recordHistory)
        {
            x = Math.Clamp(x, 0, _image.Width - 1);
            y = Math.Clamp(y, 0, _image.Height - 1);
            if (x == _image.HotspotX && y == _image.HotspotY) return;

            if (recordHistory)
                PushHistory();

            if (_isAnimation)
            {
                for (int i = 0; i < _frames.Count; i++)
                    _frames[i] = CursorHotspotService.WithHotspot(_frames[i], x, y);
                _image = _frames[_currentFrameIndex].Clone();
            }
            else
            {
                _image = CursorHotspotService.WithHotspot(_image, x, y);
            }
            HotspotXTextBox.Text = x.ToString();
            HotspotYTextBox.Text = y.ToString();
            RenderCanvas();
            UpdateEditorStatus();
            if (_isAnimation) RefreshTimeline();
        }

        private bool IsNearHotspot(Point point)
        {
            double pixelWidth = DisplaySize / _image.Width;
            double pixelHeight = DisplaySize / _image.Height;
            double hotspotX = (_image.HotspotX + 0.5) * pixelWidth;
            double hotspotY = (_image.HotspotY + 0.5) * pixelHeight;
            return Math.Abs(point.X - hotspotX) <= Math.Max(10, pixelWidth) &&
                   Math.Abs(point.Y - hotspotY) <= Math.Max(10, pixelHeight);
        }

        private void PixelCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(PixelCanvas);
            if (_tool == EditorTool.Select)
            {
                _isSelecting = true;
                _selectionStartPoint = point;
                _selection = PixelSelectionFromPoints(point, point);
                PixelCanvas.CaptureMouse();
                UpdateSelectionOverlay();
                e.Handled = true;
                return;
            }

            if (_tool == EditorTool.Eyedropper)
            {
                PickColorAt(point);
                _tool = EditorTool.Pencil;
                UpdateToolStatus("ペン");
                e.Handled = true;
                return;
            }

            if (_tool == EditorTool.Fill)
            {
                FloodFillAt(point);
                _tool = EditorTool.Pencil;
                UpdateToolStatus("ペン");
                e.Handled = true;
                return;
            }

            if (_isSettingHotspot || _tool == EditorTool.Hotspot)
            {
                if (IsNearHotspot(point))
                {
                    PushHistory();
                    _isDraggingHotspot = true;
                    PixelCanvas.CaptureMouse();
                }
                else
                {
                    SetHotspotFromPoint(point);
                    _isSettingHotspot = false;
                    _tool = EditorTool.Pencil;
                    UpdateToolStatus("ペン");
                }
                e.Handled = true;
                return;
            }

            PushHistory();
            _isPainting = true;
            PixelCanvas.CaptureMouse();
            PaintAt(point);
            e.Handled = true;
        }

        private void PixelCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingHotspot)
            {
                SetHotspotFromPointWithoutHistory(e.GetPosition(PixelCanvas));
                return;
            }

            if (_isSelecting)
            {
                _selection = PixelSelectionFromPoints(_selectionStartPoint, e.GetPosition(PixelCanvas));
                UpdateSelectionOverlay();
                return;
            }

            if (_isPainting && e.LeftButton == MouseButtonState.Pressed)
                PaintAt(e.GetPosition(PixelCanvas));
        }

        private void PixelCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingHotspot)
            {
                _isDraggingHotspot = false;
                PixelCanvas.ReleaseMouseCapture();
                if (_isAnimation) RefreshTimeline();
                e.Handled = true;
                return;
            }

            if (_isSelecting)
            {
                _isSelecting = false;
                PixelCanvas.ReleaseMouseCapture();
                UpdateSelectionOverlay();
                e.Handled = true;
                return;
            }

            if (!_isPainting) return;
            _isPainting = false;
            PixelCanvas.ReleaseMouseCapture();
            if (_isAnimation) RefreshTimeline();
            e.Handled = true;
        }

        private void SetHotspotFromPoint(Point point)
        {
            int x = Math.Clamp((int)(point.X / DisplaySize * _image.Width), 0, _image.Width - 1);
            int y = Math.Clamp((int)(point.Y / DisplaySize * _image.Height), 0, _image.Height - 1);
            SetHotspot(x, y);
        }

        private void PaintAt(Point point)
        {
            int centerX = Math.Clamp((int)(point.X / DisplaySize * _image.Width), 0, _image.Width - 1);
            int centerY = Math.Clamp((int)(point.Y / DisplaySize * _image.Height), 0, _image.Height - 1);
            int startX = centerX - _brushSize / 2;
            int startY = centerY - _brushSize / 2;
            byte[] pixels = (byte[])_image.Bgra.Clone();

            for (int y = startY; y < startY + _brushSize; y++)
            {
                for (int x = startX; x < startX + _brushSize; x++)
                {
                    if (x < 0 || x >= _image.Width || y < 0 || y >= _image.Height) continue;

                    int index = (y * _image.Width + x) * 4;
                    if (_tool == EditorTool.Eraser)
                    {
                        pixels[index + 3] = 0;
                    }
                    else
                    {
                        pixels[index] = _selectedColor.B;
                        pixels[index + 1] = _selectedColor.G;
                        pixels[index + 2] = _selectedColor.R;
                        pixels[index + 3] = _selectedColor.A;
                    }
                }
            }

            _image = new CursorCanvasImage(_image.Width, _image.Height, _image.HotspotX, _image.HotspotY, pixels);
            RenderCanvas();
        }

        private (int X, int Y) PointToPixel(Point point)
        {
            return (
                Math.Clamp((int)(point.X / DisplaySize * _image.Width), 0, _image.Width - 1),
                Math.Clamp((int)(point.Y / DisplaySize * _image.Height), 0, _image.Height - 1));
        }

        private void PickColorAt(Point point)
        {
            (int x, int y) = PointToPixel(point);
            int index = (y * _image.Width + x) * 4;
            _selectedColor = Color.FromArgb(
                _image.Bgra[index + 3],
                _image.Bgra[index + 2],
                _image.Bgra[index + 1],
                _image.Bgra[index]);
            UpdateColorControls();
            FooterTextBlock.Text = $"色を取得しました: {HexColorTextBox.Text}";
        }

        private void FloodFillAt(Point point)
        {
            (int startX, int startY) = PointToPixel(point);
            int startIndex = (startY * _image.Width + startX) * 4;
            byte targetB = _image.Bgra[startIndex];
            byte targetG = _image.Bgra[startIndex + 1];
            byte targetR = _image.Bgra[startIndex + 2];
            byte targetA = _image.Bgra[startIndex + 3];
            byte replacementB = _selectedColor.B;
            byte replacementG = _selectedColor.G;
            byte replacementR = _selectedColor.R;
            byte replacementA = _selectedColor.A;

            if (targetB == replacementB && targetG == replacementG && targetR == replacementR && targetA == replacementA)
                return;

            EditorSnapshot before = CaptureSnapshot();
            byte[] pixels = (byte[])_image.Bgra.Clone();
            bool[] visited = new bool[_image.Width * _image.Height];
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((startX, startY));
            visited[startY * _image.Width + startX] = true;

            while (queue.Count > 0)
            {
                (int x, int y) = queue.Dequeue();
                int index = (y * _image.Width + x) * 4;
                if (pixels[index] != targetB || pixels[index + 1] != targetG || pixels[index + 2] != targetR || pixels[index + 3] != targetA)
                    continue;

                pixels[index] = replacementB;
                pixels[index + 1] = replacementG;
                pixels[index + 2] = replacementR;
                pixels[index + 3] = replacementA;

                EnqueueFillNeighbor(x - 1, y, visited, queue);
                EnqueueFillNeighbor(x + 1, y, visited, queue);
                EnqueueFillNeighbor(x, y - 1, visited, queue);
                EnqueueFillNeighbor(x, y + 1, visited, queue);
            }

            PushHistory(before);
            _image = new CursorCanvasImage(_image.Width, _image.Height, _image.HotspotX, _image.HotspotY, pixels);
            RenderCanvas();
            if (_isAnimation) RefreshTimeline();
            FooterTextBlock.Text = "領域を塗りつぶしました。";
        }

        private void EnqueueFillNeighbor(int x, int y, bool[] visited, Queue<(int X, int Y)> queue)
        {
            if (x < 0 || y < 0 || x >= _image.Width || y >= _image.Height) return;
            int index = y * _image.Width + x;
            if (visited[index]) return;
            visited[index] = true;
            queue.Enqueue((x, y));
        }

        private Int32Rect PixelSelectionFromPoints(Point first, Point second)
        {
            (int firstX, int firstY) = PointToPixel(first);
            (int secondX, int secondY) = PointToPixel(second);
            int left = Math.Min(firstX, secondX);
            int top = Math.Min(firstY, secondY);
            int right = Math.Max(firstX, secondX);
            int bottom = Math.Max(firstY, secondY);
            return new Int32Rect(left, top, right - left + 1, bottom - top + 1);
        }

        private byte[] CopyPixels(Int32Rect selection)
        {
            byte[] pixels = new byte[selection.Width * selection.Height * 4];
            for (int y = 0; y < selection.Height; y++)
            {
                Buffer.BlockCopy(
                    _image.Bgra,
                    ((selection.Y + y) * _image.Width + selection.X) * 4,
                    pixels,
                    y * selection.Width * 4,
                    selection.Width * 4);
            }
            return pixels;
        }

        private void UpdateSelectionOverlay()
        {
            if (SelectionOverlay == null) return;
            SelectionOverlay.Children.Clear();
            if (_selection is not Int32Rect selection || selection.Width <= 0 || selection.Height <= 0) return;

            double pixelWidth = DisplaySize / _image.Width;
            double pixelHeight = DisplaySize / _image.Height;
            var rectangle = new Rectangle
            {
                Width = selection.Width * pixelWidth,
                Height = selection.Height * pixelHeight,
                Stroke = (Brush)FindResource("EditorSelectionBrush"),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(rectangle, selection.X * pixelWidth);
            Canvas.SetTop(rectangle, selection.Y * pixelHeight);
            SelectionOverlay.Children.Add(rectangle);
        }

        private void FlipSelection(bool horizontal)
        {
            Int32Rect region = _selection ?? new Int32Rect(0, 0, _image.Width, _image.Height);
            EditorSnapshot before = CaptureSnapshot();
            byte[] pixels = (byte[])_image.Bgra.Clone();

            for (int y = region.Y; y < region.Y + region.Height; y++)
            {
                for (int x = region.X; x < region.X + region.Width; x++)
                {
                    int sourceX = horizontal ? region.X + region.Width - 1 - (x - region.X) : x;
                    int sourceY = horizontal ? y : region.Y + region.Height - 1 - (y - region.Y);
                    Buffer.BlockCopy(_image.Bgra, (sourceY * _image.Width + sourceX) * 4, pixels, (y * _image.Width + x) * 4, 4);
                }
            }

            int hotspotX = _image.HotspotX;
            int hotspotY = _image.HotspotY;
            if (hotspotX >= region.X && hotspotX < region.X + region.Width && hotspotY >= region.Y && hotspotY < region.Y + region.Height)
            {
                if (horizontal) hotspotX = region.X + region.Width - 1 - (hotspotX - region.X);
                else hotspotY = region.Y + region.Height - 1 - (hotspotY - region.Y);
            }

            PushHistory(before);
            _image = new CursorCanvasImage(_image.Width, _image.Height, hotspotX, hotspotY, pixels);
            HotspotXTextBox.Text = hotspotX.ToString();
            HotspotYTextBox.Text = hotspotY.ToString();
            RenderCanvas();
            if (_isAnimation) RefreshTimeline();
            FooterTextBlock.Text = horizontal ? "左右反転しました。" : "上下反転しました。";
        }

        private void ResizeCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasSizeComboBox.SelectedItem is not ComboBoxItem item || !int.TryParse(item.Tag?.ToString(), out int size))
                return;

            if (size == _image.Width && size == _image.Height) return;

            EditorSnapshot before = CaptureSnapshot();
            int hotspotX = Math.Clamp(_image.HotspotX, 0, size - 1);
            int hotspotY = Math.Clamp(_image.HotspotY, 0, size - 1);
            PushHistory(before);
            if (_isAnimation)
            {
                _frames = _frames
                    .Select(frame => ResizeFrame(frame, size, size))
                    .Select(frame => CursorHotspotService.WithHotspot(frame, hotspotX, hotspotY))
                    .ToList();
                _image = _frames[_currentFrameIndex].Clone();
            }
            else
            {
                _image = ResizeFrame(_image, size, size);
            }
            _selection = null;
            HotspotXTextBox.Text = hotspotX.ToString();
            HotspotYTextBox.Text = hotspotY.ToString();
            RenderCanvas();
            UpdateEditorStatus();
            if (_isAnimation) RefreshTimeline();
            FooterTextBlock.Text = $"キャンバスサイズを{size}×{size}に変更しました。";
        }

        private void SelectCanvasSizeItem(int size)
        {
            if (CanvasSizeComboBox == null) return;
            foreach (object item in CanvasSizeComboBox.Items)
            {
                if (item is ComboBoxItem comboItem && string.Equals(comboItem.Tag?.ToString(), size.ToString(), StringComparison.Ordinal))
                {
                    CanvasSizeComboBox.SelectedItem = comboItem;
                    return;
                }
            }
        }

        private void RefreshTimeline()
        {
            if (TimelineListBox == null || !_isAnimation) return;
            CommitCurrentFrame();

            _suppressTimelineSelection = true;
            try
            {
                TimelineListBox.Items.Clear();
                for (int i = 0; i < _frames.Count; i++)
                {
                    CursorCanvasImage frame = _frames[i];
                    var image = new Image
                    {
                        Width = 48,
                        Height = 48,
                        Stretch = Stretch.Uniform,
                        Source = CreateThumbnail(frame)
                    };
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                    var panel = new StackPanel { Width = 76, HorizontalAlignment = HorizontalAlignment.Center };
                    panel.Children.Add(image);
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"{i + 1}: {_frameDelaysMs[i]}ms",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = (Brush)FindResource("TextSecondaryBrush"),
                        FontSize = 10
                    });
                    TimelineListBox.Items.Add(new ListBoxItem { Content = panel, Tag = i, Padding = new Thickness(4) });
                }

                if (_currentFrameIndex >= 0 && _currentFrameIndex < TimelineListBox.Items.Count)
                    TimelineListBox.SelectedIndex = _currentFrameIndex;
            }
            finally
            {
                _suppressTimelineSelection = false;
            }

            RefreshTimelineSelection();
        }

        private void RefreshTimelineSelection()
        {
            if (TimelineListBox == null || !_isAnimation) return;
            _suppressTimelineSelection = true;
            try
            {
                if (_currentFrameIndex >= 0 && _currentFrameIndex < TimelineListBox.Items.Count)
                    TimelineListBox.SelectedIndex = _currentFrameIndex;
                if (FrameDurationTextBox != null && _currentFrameIndex < _frameDelaysMs.Count)
                    FrameDurationTextBox.Text = _frameDelaysMs[_currentFrameIndex].ToString();
            }
            finally
            {
                _suppressTimelineSelection = false;
            }
        }

        private static BitmapSource CreateThumbnail(CursorCanvasImage frame)
        {
            var bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Bgra, frame.Width * 4, 0);
            bitmap.Freeze();
            return bitmap;
        }

        private void TimelineListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTimelineSelection || TimelineListBox.SelectedItem is not ListBoxItem item || item.Tag is not int index)
                return;
            SwitchFrame(index, refreshTimeline: true);
        }

        private void SwitchFrame(int index, bool refreshTimeline)
        {
            if (index < 0 || index >= _frames.Count || index == _currentFrameIndex) return;
            CommitCurrentFrame();
            _currentFrameIndex = index;
            _image = _frames[index].Clone();
            _selection = null;
            RenderCanvas();
            UpdateEditorStatus();
            if (refreshTimeline) RefreshTimeline();
            else RefreshTimelineSelection();
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAnimation || _frames.Count == 0) return;
            if (_previewTimer.IsEnabled)
            {
                StopPreview();
                return;
            }

            _previewTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, _frameDelaysMs[_currentFrameIndex]));
            _previewTimer.Start();
            PlayButton.Content = LocalizationService.Get("Playing");
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopPreview();
        }

        private void StopPreview()
        {
            _previewTimer.Stop();
            if (PlayButton != null) PlayButton.Content = LocalizationService.Get("Play");
        }

        private void PreviewTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isAnimation || _frames.Count == 0) return;
            int next = (_currentFrameIndex + 1) % _frames.Count;
            SwitchFrame(next, refreshTimeline: false);
            _previewTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, _frameDelaysMs[_currentFrameIndex]));
        }

        private void AddEmptyFrame_Click(object sender, RoutedEventArgs e)
        {
            CommitCurrentFrame();
            PushHistory();
            CursorCanvasImage blank = new CursorCanvasImage(_image.Width, _image.Height, _image.HotspotX, _image.HotspotY, new byte[_image.Width * _image.Height * 4]);
            int insertAt = Math.Min(_currentFrameIndex + 1, _frames.Count);
            _frames.Insert(insertAt, blank);
            _frameDelaysMs.Insert(insertAt, 100);
            _isAnimation = true;
            _currentFrameIndex = insertAt;
            _image = blank.Clone();
            TimelinePanel.Visibility = Visibility.Visible;
            RenderCanvas();
            RefreshTimeline();
        }

        private void DuplicateFrame_Click(object sender, RoutedEventArgs e)
        {
            CommitCurrentFrame();
            PushHistory();
            int insertAt = Math.Min(_currentFrameIndex + 1, _frames.Count);
            _frames.Insert(insertAt, _frames[_currentFrameIndex].Clone());
            _frameDelaysMs.Insert(insertAt, _frameDelaysMs[_currentFrameIndex]);
            _currentFrameIndex = insertAt;
            _image = _frames[insertAt].Clone();
            RenderCanvas();
            RefreshTimeline();
        }

        private void DeleteFrame_Click(object sender, RoutedEventArgs e)
        {
            if (_frames.Count <= 1) return;
            PushHistory();
            _frames.RemoveAt(_currentFrameIndex);
            _frameDelaysMs.RemoveAt(_currentFrameIndex);
            _currentFrameIndex = Math.Min(_currentFrameIndex, _frames.Count - 1);
            _image = _frames[_currentFrameIndex].Clone();
            RenderCanvas();
            RefreshTimeline();
        }

        private void MoveFrameUp_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFrameIndex <= 0) return;
            MoveFrame(-1);
        }

        private void MoveFrameDown_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFrameIndex >= _frames.Count - 1) return;
            MoveFrame(1);
        }

        private void MoveFrame(int direction)
        {
            PushHistory();
            int target = _currentFrameIndex + direction;
            (_frames[_currentFrameIndex], _frames[target]) = (_frames[target], _frames[_currentFrameIndex]);
            (_frameDelaysMs[_currentFrameIndex], _frameDelaysMs[target]) = (_frameDelaysMs[target], _frameDelaysMs[_currentFrameIndex]);
            _currentFrameIndex = target;
            _image = _frames[target].Clone();
            RenderCanvas();
            RefreshTimeline();
        }

        private void ApplyFrameDuration_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAnimation || !int.TryParse(FrameDurationTextBox.Text, out int milliseconds) || milliseconds < 10 || milliseconds > 60000)
            {
                MessageBox.Show(Window.GetWindow(this), "表示時間は10〜60000msの整数で指定してください。", "フレーム表示時間", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_frameDelaysMs[_currentFrameIndex] == milliseconds) return;
            PushHistory();
            _frameDelaysMs[_currentFrameIndex] = milliseconds;
            RefreshTimeline();
        }

        private void RenderCanvas()
        {
            CommitCurrentFrame();
            int stride = _image.Width * 4;
            _previewBitmap = new WriteableBitmap(
                _image.Width,
                _image.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null);
            _previewBitmap.WritePixels(
                new Int32Rect(0, 0, _image.Width, _image.Height),
                _image.Bgra,
                stride,
                0);
            CanvasImage.Source = _previewBitmap;
            RenderGridOverlay();
            UpdateSelectionOverlay();

            double pixelWidth = DisplaySize / _image.Width;
            double pixelHeight = DisplaySize / _image.Height;
            double hotspotX = (_image.HotspotX + 0.5) * pixelWidth;
            double hotspotY = (_image.HotspotY + 0.5) * pixelHeight;

            HotspotVerticalLine.X1 = hotspotX;
            HotspotVerticalLine.X2 = hotspotX;
            HotspotVerticalLine.Y1 = 0;
            HotspotVerticalLine.Y2 = DisplaySize;
            HotspotHorizontalLine.X1 = 0;
            HotspotHorizontalLine.X2 = DisplaySize;
            HotspotHorizontalLine.Y1 = hotspotY;
            HotspotHorizontalLine.Y2 = hotspotY;
            Canvas.SetLeft(HotspotMarker, hotspotX - HotspotMarker.Width / 2);
            Canvas.SetTop(HotspotMarker, hotspotY - HotspotMarker.Height / 2);
        }

        private void GridCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            RenderGridOverlay();
        }

        private void RenderGridOverlay()
        {
            if (GridOverlay == null || _image == null) return;

            GridOverlay.Children.Clear();
            if (GridCheckBox?.IsChecked != true) return;

            double pixelWidth = DisplaySize / _image.Width;
            double pixelHeight = DisplaySize / _image.Height;

            // At very small display sizes the grid becomes visual noise. The
            // underlying image and all saved data remain unchanged.
            if (pixelWidth < 4 || pixelHeight < 4) return;

            for (int x = 0; x <= _image.Width; x++)
            {
                double position = Math.Min(DisplaySize, x * pixelWidth);
                GridOverlay.Children.Add(new Line
                {
                    X1 = position,
                    X2 = position,
                    Y1 = 0,
                    Y2 = DisplaySize,
                    Stroke = (Brush)FindResource("EditorGridBrush"),
                    StrokeThickness = 1,
                    Opacity = 0.55
                });
            }

            for (int y = 0; y <= _image.Height; y++)
            {
                double position = Math.Min(DisplaySize, y * pixelHeight);
                GridOverlay.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = DisplaySize,
                    Y1 = position,
                    Y2 = position,
                    Stroke = (Brush)FindResource("EditorGridBrush"),
                    StrokeThickness = 1,
                    Opacity = 0.55
                });
            }
        }

        private void UpdateEditorStatus()
        {
            string format = _isAnimation
                ? $"ANI Frame {_currentFrameIndex + 1}/{_frames.Count}"
                : "CUR";
            EditorStatusTextBlock.Text = $"{_image.Width}×{_image.Height} {format} / Hotspot ({_image.HotspotX}, {_image.HotspotY})";
        }

        private static bool TryParseColor(string? text, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string value = text.Trim();
            if (value.StartsWith('#')) value = value[1..];
            if ((value.Length != 6 && value.Length != 8) || !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out uint number))
                return false;

            color = value.Length == 6
                ? Color.FromRgb((byte)(number >> 16), (byte)(number >> 8), (byte)number)
                : Color.FromArgb((byte)(number >> 24), (byte)(number >> 16), (byte)(number >> 8), (byte)number);
            return true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) Redo_Click(sender, e);
                else Undo_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                Redo_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.P)
            {
                Pencil_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                Eraser_Click(sender, e);
                e.Handled = true;
            }
        }
    }

    internal static class StackExtensions
    {
        public static void RemoveBottom<T>(this Stack<T> stack)
        {
            if (stack.Count == 0) return;
            T[] values = stack.ToArray();
            stack.Clear();
            for (int i = values.Length - 2; i >= 0; i--)
                stack.Push(values[i]);
        }
    }
}

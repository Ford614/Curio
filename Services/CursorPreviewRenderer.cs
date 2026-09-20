using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Curio.Services
{
    public static class CursorPreviewRenderer
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadCursorFromFile(string lpFileName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// Loads a .cur or .ani file and renders it into a WPF ImageSource preview.
        /// </summary>
        public static ImageSource? CreatePreview(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            IntPtr hCursor = IntPtr.Zero;
            try
            {
                hCursor = LoadCursorFromFile(filePath);
                if (hCursor == IntPtr.Zero)
                    return null;

                BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                    hCursor,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bitmap.Freeze(); // Make cross-thread accessible
                return bitmap;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hCursor != IntPtr.Zero)
                {
                    DestroyIcon(hCursor);
                }
            }
        }
    }
}

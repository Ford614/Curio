using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using Curio.Models;

namespace Curio.Services
{
    public static class GifAnimationReader
    {
        public static AniCursorData Read(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("GIFにフレームがありません。");

            int canvasWidth = 1;
            int canvasHeight = 1;
            foreach (BitmapFrame frame in decoder.Frames)
            {
                int left = GetMetadataInt(frame, "/imgdesc/Left", 0);
                int top = GetMetadataInt(frame, "/imgdesc/Top", 0);
                canvasWidth = Math.Max(canvasWidth, left + frame.PixelWidth);
                canvasHeight = Math.Max(canvasHeight, top + frame.PixelHeight);
            }

            int canvasSize = Math.Max(canvasWidth, canvasHeight);
            if (canvasSize > 256)
                throw new InvalidDataException("GIF画像は256×256以内で読み込んでください。");

            canvasWidth = canvasSize;
            canvasHeight = canvasSize;
            byte[] canvas = new byte[canvasWidth * canvasHeight * 4];
            var frames = new List<CursorCanvasImage>(decoder.Frames.Count);
            var delays = new List<int>(decoder.Frames.Count);

            foreach (BitmapFrame frame in decoder.Frames)
            {
                int left = Math.Max(0, GetMetadataInt(frame, "/imgdesc/Left", 0));
                int top = Math.Max(0, GetMetadataInt(frame, "/imgdesc/Top", 0));
                int delay = Math.Max(10, GetMetadataInt(frame, "/grctlext/Delay", 10) * 10);
                int disposal = GetMetadataInt(frame, "/grctlext/Disposal", 0);
                byte[] previous = disposal == 3 ? (byte[])canvas.Clone() : Array.Empty<byte>();
                byte[] source = ToBgra(frame);

                Composite(source, frame.PixelWidth, frame.PixelHeight, left, top, canvas, canvasWidth, canvasHeight);
                frames.Add(new CursorCanvasImage(canvasWidth, canvasHeight, 0, 0, (byte[])canvas.Clone()));
                delays.Add(delay);

                if (disposal == 2)
                    ClearRegion(canvas, canvasWidth, canvasHeight, left, top, frame.PixelWidth, frame.PixelHeight);
                else if (disposal == 3 && previous.Length > 0)
                    canvas = previous;
            }

            return new AniCursorData(frames, delays);
        }

        private static byte[] ToBgra(BitmapSource source)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int stride = converted.PixelWidth * 4;
            byte[] pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        private static void Composite(byte[] source, int sourceWidth, int sourceHeight, int left, int top, byte[] destination, int destinationWidth, int destinationHeight)
        {
            for (int y = 0; y < sourceHeight; y++)
            {
                int destinationY = top + y;
                if (destinationY < 0 || destinationY >= destinationHeight) continue;
                for (int x = 0; x < sourceWidth; x++)
                {
                    int destinationX = left + x;
                    if (destinationX < 0 || destinationX >= destinationWidth) continue;

                    int sourceIndex = (y * sourceWidth + x) * 4;
                    int destinationIndex = (destinationY * destinationWidth + destinationX) * 4;
                    byte sourceAlpha = source[sourceIndex + 3];
                    if (sourceAlpha == 0) continue;
                    if (sourceAlpha == 255)
                    {
                        Buffer.BlockCopy(source, sourceIndex, destination, destinationIndex, 4);
                        continue;
                    }

                    byte destinationAlpha = destination[destinationIndex + 3];
                    int outputAlpha = sourceAlpha + destinationAlpha * (255 - sourceAlpha) / 255;
                    if (outputAlpha == 0) continue;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int sourceValue = source[sourceIndex + channel] * sourceAlpha;
                        int destinationValue = destination[destinationIndex + channel] * destinationAlpha * (255 - sourceAlpha) / 255;
                        destination[destinationIndex + channel] = (byte)Math.Clamp((sourceValue + destinationValue) / outputAlpha, 0, 255);
                    }
                    destination[destinationIndex + 3] = (byte)outputAlpha;
                }
            }
        }

        private static void ClearRegion(byte[] canvas, int canvasWidth, int canvasHeight, int left, int top, int width, int height)
        {
            for (int y = Math.Max(0, top); y < Math.Min(canvasHeight, top + height); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(canvasWidth, left + width); x++)
                    Array.Clear(canvas, (y * canvasWidth + x) * 4, 4);
            }
        }

        private static int GetMetadataInt(BitmapFrame frame, string query, int fallback)
        {
            try
            {
                if (frame.Metadata is BitmapMetadata metadata && metadata.ContainsQuery(query))
                {
                    object? value = metadata.GetQuery(query);
                    if (value is ushort ushortValue) return ushortValue;
                    if (value is byte byteValue) return byteValue;
                    if (value is uint uintValue) return checked((int)uintValue);
                    if (value is int intValue) return intValue;
                }
            }
            catch
            {
            }
            return fallback;
        }
    }
}

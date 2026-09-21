using System.Buffers.Binary;
using System.IO;
using Curio.Models;

namespace Curio.Services
{
    /// <summary>
    /// Minimal 32-bit DIB CUR reader/writer used by the Phase 1 editor.
    /// </summary>
    public static class CursorCanvasService
    {
        private const int IconDirectorySize = 6;
        private const int IconDirectoryEntrySize = 16;
        private const int BitmapInfoHeaderSize = 40;

        public static CursorCanvasImage Read(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            return Read(File.ReadAllBytes(filePath));
        }

        public static CursorCanvasImage Read(byte[] data)
        {
            if (data.Length < IconDirectorySize + IconDirectoryEntrySize)
                throw new InvalidDataException("CURヘッダーが不正です。");

            ushort reserved = ReadUInt16(data, 0);
            ushort type = ReadUInt16(data, 2);
            ushort count = ReadUInt16(data, 4);

            if (reserved != 0 || type != 2 || count == 0)
                throw new InvalidDataException("CURファイルではありません。");

            int entryOffset = IconDirectorySize;
            int imageOffset = checked((int)ReadUInt32(data, entryOffset + 12));
            int imageSize = checked((int)ReadUInt32(data, entryOffset + 8));
            int hotspotX = ReadUInt16(data, entryOffset + 4);
            int hotspotY = ReadUInt16(data, entryOffset + 6);

            if (imageOffset < 0 || imageSize <= 0 || imageOffset > data.Length - imageSize)
                throw new InvalidDataException("CUR画像データの範囲が不正です。");

            return ReadDib(data, imageOffset, imageSize, hotspotX, hotspotY);
        }

        public static void Write(string destinationPath, CursorCanvasImage image)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
            ArgumentNullException.ThrowIfNull(image);

            File.WriteAllBytes(destinationPath, BuildBytes(image));
        }

        public static byte[] BuildBytes(CursorCanvasImage image)
        {
            ArgumentNullException.ThrowIfNull(image);

            byte[] dib = BuildDib(image);
            const int entryOffset = IconDirectorySize;
            const int imageOffset = IconDirectorySize + IconDirectoryEntrySize;
            byte[] result = new byte[imageOffset + dib.Length];

            WriteUInt16(result, 0, 0);
            WriteUInt16(result, 2, 2);
            WriteUInt16(result, 4, 1);

            result[entryOffset] = image.Width == 256 ? (byte)0 : checked((byte)image.Width);
            result[entryOffset + 1] = image.Height == 256 ? (byte)0 : checked((byte)image.Height);
            result[entryOffset + 2] = 0;
            result[entryOffset + 3] = 0;
            WriteUInt16(result, entryOffset + 4, checked((ushort)image.HotspotX));
            WriteUInt16(result, entryOffset + 6, checked((ushort)image.HotspotY));
            WriteUInt32(result, entryOffset + 8, checked((uint)dib.Length));
            WriteUInt32(result, entryOffset + 12, imageOffset);

            Buffer.BlockCopy(dib, 0, result, imageOffset, dib.Length);
            return result;
        }

        private static CursorCanvasImage ReadDib(
            byte[] data,
            int offset,
            int availableLength,
            int hotspotX,
            int hotspotY)
        {
            if (availableLength < BitmapInfoHeaderSize)
                throw new InvalidDataException("CURのDIBヘッダーが不正です。");

            int headerSize = checked((int)ReadUInt32(data, offset));
            if (headerSize < BitmapInfoHeaderSize || headerSize > availableLength)
                throw new InvalidDataException("未対応のDIBヘッダーです。");

            int width = ReadInt32(data, offset + 4);
            int storedHeight = ReadInt32(data, offset + 8);
            ushort planes = ReadUInt16(data, offset + 12);
            ushort bitCount = ReadUInt16(data, offset + 14);
            uint compression = ReadUInt32(data, offset + 16);

            if (width <= 0 || storedHeight == 0 || planes != 1 || compression != 0)
                throw new InvalidDataException("未対応のCUR画像形式です。");

            int height = Math.Abs(storedHeight) / 2;
            if (height <= 0 || width > 256 || height > 256)
                throw new InvalidDataException("CUR画像サイズが対応範囲外です。");

            int pixelOffset = checked(offset + headerSize);
            bool topDown = storedHeight < 0;
            int xorStride = checked(((width * bitCount + 31) / 32) * 4);
            int xorBytes = checked(xorStride * height);
            int maskStride = checked(((width + 31) / 32) * 4);
            int maskOffset = checked(pixelOffset + xorBytes);
            int maskBytes = checked(maskStride * height);

            if (pixelOffset < offset || maskOffset > data.Length - maskBytes)
                throw new InvalidDataException("CUR画像データが切り詰められています。");

            if (bitCount != 32 && bitCount != 24)
                throw new InvalidDataException("Phase 1では24bit/32bit DIB CURのみ対応しています。");

            byte[] bgra = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                int sourceY = topDown ? y : height - 1 - y;
                int sourceRow = pixelOffset + sourceY * xorStride;
                int targetRow = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int sourcePixel = sourceRow + x * (bitCount / 8);
                    int targetPixel = targetRow + x * 4;

                    bgra[targetPixel] = data[sourcePixel];
                    bgra[targetPixel + 1] = data[sourcePixel + 1];
                    bgra[targetPixel + 2] = data[sourcePixel + 2];
                    bgra[targetPixel + 3] = bitCount == 32 ? data[sourcePixel + 3] : (byte)255;
                }
            }

            // The AND mask is authoritative for transparency in older CUR files.
            for (int y = 0; y < height; y++)
            {
                int sourceY = topDown ? y : height - 1 - y;
                int row = maskOffset + sourceY * maskStride;

                for (int x = 0; x < width; x++)
                {
                    if ((data[row + (x / 8)] & (0x80 >> (x % 8))) != 0)
                    {
                        bgra[(y * width + x) * 4 + 3] = 0;
                    }
                }
            }

            return new CursorCanvasImage(width, height, hotspotX, hotspotY, bgra);
        }

        private static byte[] BuildDib(CursorCanvasImage image)
        {
            int xorStride = checked(image.Width * 4);
            int maskStride = checked(((image.Width + 31) / 32) * 4);
            int xorBytes = checked(xorStride * image.Height);
            int maskBytes = checked(maskStride * image.Height);
            byte[] dib = new byte[BitmapInfoHeaderSize + xorBytes + maskBytes];

            WriteUInt32(dib, 0, BitmapInfoHeaderSize);
            WriteInt32(dib, 4, image.Width);
            WriteInt32(dib, 8, image.Height * 2);
            WriteUInt16(dib, 12, 1);
            WriteUInt16(dib, 14, 32);
            WriteUInt32(dib, 16, 0);
            WriteUInt32(dib, 20, checked((uint)xorBytes));

            for (int y = 0; y < image.Height; y++)
            {
                int sourceRow = (image.Height - 1 - y) * image.Width * 4;
                int targetRow = BitmapInfoHeaderSize + y * xorStride;
                Buffer.BlockCopy(image.Bgra, sourceRow, dib, targetRow, xorStride);

                int maskRow = BitmapInfoHeaderSize + xorBytes + y * maskStride;
                for (int x = 0; x < image.Width; x++)
                {
                    if (image.Bgra[(sourceRow + x * 4) + 3] == 0)
                        dib[maskRow + x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }

            return dib;
        }

        private static ushort ReadUInt16(byte[] data, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));

        private static short ReadInt16(byte[] data, int offset) =>
            BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));

        private static uint ReadUInt32(byte[] data, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

        private static int ReadInt32(byte[] data, int offset) =>
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));

        private static void WriteUInt16(byte[] data, int offset, ushort value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);

        private static void WriteUInt32(byte[] data, int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);

        private static void WriteInt32(byte[] data, int offset, int value) =>
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), value);
    }
}

using System.Text;
using System.IO;
using Curio.Models;

namespace Curio.Services
{
    public static class AniCursorWriter
    {
        private const double JiffiesPerSecond = 60d;

        public static void Write(
            string destinationPath,
            IReadOnlyList<CursorCanvasImage> frames,
            IReadOnlyList<int> frameDelaysMs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
            if (frames.Count == 0)
                throw new ArgumentException("ANIには1つ以上のフレームが必要です。", nameof(frames));

            var iconBytes = frames.Select(CursorCanvasService.BuildBytes).ToList();
            var rates = frames.Select((_, index) => ToJiffies(index < frameDelaysMs.Count ? frameDelaysMs[index] : 100)).ToArray();

            using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            long sizePosition = stream.Position;
            writer.Write((uint)0);
            writer.Write(Encoding.ASCII.GetBytes("ACON"));

            WriteAnih(writer, frames.Count, rates[0]);
            WriteRate(writer, rates);
            WriteFrames(writer, iconBytes);

            long endPosition = stream.Position;
            stream.Position = sizePosition;
            writer.Write((uint)(endPosition - sizePosition - 4));
        }

        private static uint ToJiffies(int milliseconds)
        {
            return (uint)Math.Max(1, Math.Round(Math.Max(1, milliseconds) * JiffiesPerSecond / 1000d));
        }

        private static void WriteAnih(BinaryWriter writer, int frameCount, uint defaultRate)
        {
            WriteChunkHeader(writer, "anih", 36);
            writer.Write((uint)36);
            writer.Write((uint)frameCount);
            writer.Write((uint)frameCount);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write(defaultRate);
            writer.Write((uint)1);
        }

        private static void WriteRate(BinaryWriter writer, IReadOnlyList<uint> rates)
        {
            WriteChunkHeader(writer, "rate", rates.Count * 4);
            foreach (uint rate in rates)
                writer.Write(rate);
            WritePadding(writer, rates.Count * 4);
        }

        private static void WriteFrames(BinaryWriter writer, IReadOnlyList<byte[]> frames)
        {
            int listSize = 4 + frames.Sum(bytes => 8 + bytes.Length + bytes.Length % 2);
            WriteChunkHeader(writer, "LIST", listSize);
            writer.Write(Encoding.ASCII.GetBytes("fram"));

            foreach (byte[] frame in frames)
            {
                WriteChunkHeader(writer, "icon", frame.Length);
                writer.Write(frame);
                WritePadding(writer, frame.Length);
            }
        }

        private static void WriteChunkHeader(BinaryWriter writer, string fourCc, int size)
        {
            writer.Write(Encoding.ASCII.GetBytes(fourCc));
            writer.Write((uint)size);
        }

        private static void WritePadding(BinaryWriter writer, int size)
        {
            if (size % 2 != 0)
                writer.Write((byte)0);
        }
    }
}

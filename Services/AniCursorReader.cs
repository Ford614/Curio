using System.Buffers.Binary;
using System.IO;
using System.Text;
using Curio.Models;

namespace Curio.Services
{
    public sealed record AniCursorData(
        IReadOnlyList<CursorCanvasImage> Frames,
        IReadOnlyList<int> FrameDelaysMs);

    public static class AniCursorReader
    {
        private const int RiffHeaderSize = 12;
        private const int ChunkHeaderSize = 8;
        private const int AnimationHeaderSize = 36;
        private const double JiffiesPerSecond = 60d;
        private const int DefaultJiffiesPerFrame = 6;

        public static AniCursorData Read(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            return Read(File.ReadAllBytes(filePath));
        }

        public static AniCursorData Read(byte[] bytes)
        {
            if (!IsRiffAcon(bytes))
                throw new InvalidDataException("ANIファイルではありません。");

            var header = new AnimationHeader();
            var iconChunks = new List<(int Offset, int Length)>();
            WalkChunks(bytes, RiffHeaderSize, bytes.Length, (fourCc, offset, length) =>
            {
                switch (fourCc)
                {
                    case "anih":
                        ReadAnimationHeader(bytes, offset, length, header);
                        break;
                    case "rate":
                        header.Rates = ReadUInt32Array(bytes, offset, length);
                        break;
                    case "seq ":
                        header.Sequence = ReadUInt32Array(bytes, offset, length);
                        break;
                    case "icon":
                        iconChunks.Add((offset, length));
                        break;
                }
            });

            if (iconChunks.Count == 0)
                throw new InvalidDataException("ANIにフレームがありません。");

            int physicalFrameCount = header.FrameCount > 0
                ? Math.Min((int)header.FrameCount, iconChunks.Count)
                : iconChunks.Count;
            var physicalFrames = new List<CursorCanvasImage>(physicalFrameCount);

            for (int i = 0; i < physicalFrameCount; i++)
            {
                var (offset, length) = iconChunks[i];
                byte[] curBytes = bytes[offset..(offset + length)];
                physicalFrames.Add(CursorCanvasService.Read(curBytes));
            }

            int stepCount = header.StepCount > 0
                ? (int)header.StepCount
                : physicalFrames.Count;
            var frames = new List<CursorCanvasImage>(stepCount);
            var delays = new List<int>(stepCount);

            for (int step = 0; step < stepCount; step++)
            {
                int frameIndex = header.Sequence != null && step < header.Sequence.Length
                    ? (int)header.Sequence[step]
                    : step % physicalFrames.Count;
                frameIndex = Math.Clamp(frameIndex, 0, physicalFrames.Count - 1);
                frames.Add(physicalFrames[frameIndex].Clone());

                uint jiffies = header.Rates != null && step < header.Rates.Length && header.Rates[step] > 0
                    ? header.Rates[step]
                    : header.DefaultJiffies;
                delays.Add(Math.Max(1, (int)Math.Round(jiffies * 1000d / JiffiesPerSecond)));
            }

            return new AniCursorData(frames, delays);
        }

        private sealed class AnimationHeader
        {
            public uint FrameCount;
            public uint StepCount;
            public uint DefaultJiffies = DefaultJiffiesPerFrame;
            public uint[]? Rates;
            public uint[]? Sequence;
        }

        private static bool IsRiffAcon(byte[] bytes)
        {
            return bytes.Length >= RiffHeaderSize &&
                   Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                   Encoding.ASCII.GetString(bytes, 8, 4) == "ACON";
        }

        private static void ReadAnimationHeader(byte[] bytes, int offset, int length, AnimationHeader header)
        {
            if (length < AnimationHeaderSize) return;
            header.FrameCount = ReadUInt32(bytes, offset + 4);
            header.StepCount = ReadUInt32(bytes, offset + 8);
            header.DefaultJiffies = ReadUInt32(bytes, offset + 28);
            if (header.DefaultJiffies == 0)
                header.DefaultJiffies = DefaultJiffiesPerFrame;
        }

        private static void WalkChunks(byte[] bytes, int start, int end, Action<string, int, int> onChunk)
        {
            int position = start;
            while (position + ChunkHeaderSize <= end)
            {
                string fourCc = Encoding.ASCII.GetString(bytes, position, 4);
                uint unsignedLength = ReadUInt32(bytes, position + 4);
                if (unsignedLength > int.MaxValue) break;
                int length = (int)unsignedLength;
                int dataOffset = position + ChunkHeaderSize;
                if (dataOffset > end || length > end - dataOffset) break;

                if (fourCc == "LIST" && length >= 4)
                    WalkChunks(bytes, dataOffset + 4, dataOffset + length, onChunk);
                else
                    onChunk(fourCc, dataOffset, length);

                position = dataOffset + length + (length % 2);
            }
        }

        private static uint ReadUInt32(byte[] bytes, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

        private static uint[] ReadUInt32Array(byte[] bytes, int offset, int length)
        {
            int count = length / 4;
            var values = new uint[count];
            for (int i = 0; i < count; i++)
                values[i] = ReadUInt32(bytes, offset + i * 4);
            return values;
        }
    }
}

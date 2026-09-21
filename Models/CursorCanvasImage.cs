namespace Curio.Models
{
    /// <summary>
    /// A single cursor image stored as a 32-bit BGRA buffer.
    /// </summary>
    public sealed class CursorCanvasImage
    {
        public CursorCanvasImage(int width, int height, int hotspotX, int hotspotY, byte[] bgra)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "画像サイズは1以上である必要があります。");

            if (bgra.Length != width * height * 4)
                throw new ArgumentException("BGRA配列のサイズが画像サイズと一致しません。", nameof(bgra));

            Width = width;
            Height = height;
            HotspotX = Math.Clamp(hotspotX, 0, width - 1);
            HotspotY = Math.Clamp(hotspotY, 0, height - 1);
            Bgra = bgra;
        }

        public int Width { get; }
        public int Height { get; }
        public int HotspotX { get; }
        public int HotspotY { get; }
        public byte[] Bgra { get; }

        public CursorCanvasImage Clone()
        {
            return new CursorCanvasImage(
                Width,
                Height,
                HotspotX,
                HotspotY,
                (byte[])Bgra.Clone());
        }
    }
}

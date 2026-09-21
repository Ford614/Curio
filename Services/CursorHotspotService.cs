using Curio.Models;

namespace Curio.Services
{
    public static class CursorHotspotService
    {
        public static CursorCanvasImage WithHotspot(CursorCanvasImage image, int x, int y)
        {
            ArgumentNullException.ThrowIfNull(image);
            return new CursorCanvasImage(image.Width, image.Height, x, y, (byte[])image.Bgra.Clone());
        }
    }
}

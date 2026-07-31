namespace Norscope.AskAI
{
    /// <summary>In-memory still image captured for the current Ask AI session.</summary>
    public sealed class AskAiCapturedImage
    {
        public byte[] JpegBytes { get; }
        public int Width { get; }
        public int Height { get; }
        public string Format { get; }

        public AskAiCapturedImage(byte[] jpegBytes, int width, int height, string format = "jpeg")
        {
            JpegBytes = jpegBytes ?? System.Array.Empty<byte>();
            Width = width;
            Height = height;
            Format = string.IsNullOrEmpty(format) ? "jpeg" : format;
        }

        public bool IsValid =>
            JpegBytes != null &&
            JpegBytes.Length > 0 &&
            Width > 0 &&
            Height > 0;
    }
}

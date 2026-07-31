using System;

namespace Norscope.AskAI
{
    /// <summary>
    /// Session-scoped Ask AI payload. Held in memory only — not uploaded in Phase 3.
    /// </summary>
    public sealed class AskAiRequest
    {
        public string Transcript { get; set; } = string.Empty;
        public byte[] ImageBytes { get; set; }
        public string ImageFormat { get; set; } = "jpeg";
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string CaptureNotes { get; set; } = string.Empty;

        public bool HasImage =>
            ImageBytes != null &&
            ImageBytes.Length > 0 &&
            Width > 0 &&
            Height > 0;

        public void ApplyImage(AskAiCapturedImage image)
        {
            if (image == null || !image.IsValid)
            {
                ImageBytes = null;
                Width = 0;
                Height = 0;
                ImageFormat = "jpeg";
                return;
            }

            ImageBytes = image.JpegBytes;
            Width = image.Width;
            Height = image.Height;
            ImageFormat = image.Format;
            TimestampUtc = DateTime.UtcNow;
        }

        public void Clear()
        {
            Transcript = string.Empty;
            ImageBytes = null;
            ImageFormat = "jpeg";
            Width = 0;
            Height = 0;
            TimestampUtc = default;
            CaptureNotes = string.Empty;
        }
    }
}

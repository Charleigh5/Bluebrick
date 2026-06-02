using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using PdfiumViewer;

namespace BlueBrick.Agent
{
    internal static class AssistantImageTools
    {
        private const string SolidWorksOrForegroundTarget = "solidworks_or_foreground";
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        internal static string CaptureForegroundWindow(string sessionId)
        {
            return CaptureForegroundWindowArtifact(sessionId)?.Path ?? string.Empty;
        }

        internal static AssistantScreenshotArtifact CaptureForegroundWindowArtifact(string sessionId)
        {
            return CaptureWindowArtifact(new AssistantScreenshotCaptureRequest
            {
                SessionId = sessionId,
                CaptureTarget = "foreground"
            });
        }

        internal static AssistantScreenshotArtifact CaptureWindowArtifact(AssistantScreenshotCaptureRequest request)
        {
            request = NormalizeCaptureRequest(request);
            var captureSource = "foreground";
            var handle = ResolveCaptureWindow(request.CaptureTarget, out captureSource);
            if (handle == IntPtr.Zero) return null;
            if (!GetWindowRect(handle, out var rect)) return null;

            var width = Math.Max(1, rect.Right - rect.Left);
            var height = Math.Max(1, rect.Bottom - rect.Top);
            var artifactId = AssistantScreenshotArtifactStore.NewArtifactId();
            var output = AssistantScreenshotArtifactStore.BuildCapturePath(artifactId, ".png");

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                bitmap.Save(output, ImageFormat.Png);
            }

            var artifact = new AssistantScreenshotArtifact
            {
                SchemaVersion = AssistantApiEnvelope.CurrentSchemaVersion,
                ScreenshotId = artifactId,
                ArtifactId = artifactId,
                SessionId = request.SessionId,
                Path = output,
                CapturedUtc = DateTime.UtcNow,
                Width = width,
                Height = height,
                SourceWindowTitle = GetWindowTitle(handle),
                CaptureTarget = request.CaptureTarget,
                CaptureSource = captureSource
            };
            AssistantScreenshotAnalyzer.EnsurePrivacyMetadata(artifact, null, false);
            AssistantScreenshotArtifactStore.CompleteArtifact(artifact);
            return artifact;
        }

        internal static AssistantScreenshotCaptureRequest NormalizeCaptureRequest(AssistantScreenshotCaptureRequest request)
        {
            request = request ?? new AssistantScreenshotCaptureRequest();
            if (string.IsNullOrWhiteSpace(request.CaptureTarget))
            {
                request.CaptureTarget = SolidWorksOrForegroundTarget;
            }
            request.CaptureTarget = request.CaptureTarget.Trim().ToLowerInvariant();
            if (request.CaptureTarget != "foreground" && request.CaptureTarget != "solidworks" && request.CaptureTarget != SolidWorksOrForegroundTarget)
            {
                request.CaptureTarget = SolidWorksOrForegroundTarget;
            }
            return request;
        }

        internal static bool IsSolidWorksWindowTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            return title.IndexOf("SOLIDWORKS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ||
                   title.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
                   title.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase) ||
                   title.IndexOf(".SLDASM ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf(".SLDPRT ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf(".SLDDRW ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IntPtr ResolveCaptureWindow(string captureTarget, out string captureSource)
        {
            captureSource = "foreground";
            if (captureTarget == "solidworks" || captureTarget == SolidWorksOrForegroundTarget)
            {
                var solidWorks = FindSolidWorksWindow();
                if (solidWorks != IntPtr.Zero)
                {
                    captureSource = "solidworks";
                    return solidWorks;
                }

                if (captureTarget == "solidworks")
                {
                    captureSource = "solidworks_not_found";
                    return IntPtr.Zero;
                }
            }

            return GetForegroundWindow();
        }

        private static IntPtr FindSolidWorksWindow()
        {
            var found = IntPtr.Zero;
            EnumWindows((handle, _) =>
            {
                if (!IsWindowVisible(handle)) return true;
                var title = GetWindowTitle(handle);
                if (!IsSolidWorksWindowTitle(title)) return true;
                found = handle;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        internal static string PrepareAttachment(string sessionId, string sourcePath, int maxImageDimension, int jpegQuality)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return string.Empty;
            var boundedDimension = Math.Max(512, Math.Min(maxImageDimension, 4096));
            var boundedQuality = Math.Max(35, Math.Min(jpegQuality, 95));

            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            switch (extension)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                    return ResizeImage(sessionId, sourcePath, boundedDimension, boundedQuality);
                case ".pdf":
                    return RenderPdfFirstPage(sessionId, sourcePath, boundedDimension, boundedQuality);
                default:
                    return string.Empty;
            }
        }

        private static string ResizeImage(string sessionId, string sourcePath, int maxImageDimension, int jpegQuality)
        {
            using (var source = Image.FromFile(sourcePath))
            {
                var ratio = Math.Min((float)maxImageDimension / source.Width, (float)maxImageDimension / source.Height);
                if (ratio > 1f) ratio = 1f;
                var width = Math.Max(1, (int)(source.Width * ratio));
                var height = Math.Max(1, (int)(source.Height * ratio));
                var output = BuildTempPath(sessionId, "img", ".jpg");
                using (var bitmap = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, 0, 0, width, height);
                    var encoder = ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg");
                    var parameters = new EncoderParameters(1);
                    parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)jpegQuality);
                    bitmap.Save(output, encoder, parameters);
                }
                return output;
            }
        }

        private static string RenderPdfFirstPage(string sessionId, string sourcePath, int maxImageDimension, int jpegQuality)
        {
            using (var document = PdfDocument.Load(sourcePath))
            using (var image = document.Render(0, maxImageDimension, maxImageDimension, 96, 96, true))
            {
                var temp = BuildTempPath(sessionId, "pdf-src", ".png");
                image.Save(temp, ImageFormat.Png);
                return ResizeImage(sessionId, temp, maxImageDimension, jpegQuality);
            }
        }

        private static string BuildTempPath(string sessionId, string prefix, string extension)
        {
            var root = Path.Combine(AppIdentity.AssistantHistoryRoot, "attachments", sessionId ?? "global");
            Directory.CreateDirectory(root);
            return Path.Combine(root, prefix + "-" + Guid.NewGuid().ToString("N") + extension);
        }

        private static string GetWindowTitle(IntPtr handle)
        {
            var length = GetWindowTextLength(handle);
            if (length <= 0) return string.Empty;
            var builder = new StringBuilder(length + 1);
            return GetWindowText(handle, builder, builder.Capacity) > 0 ? builder.ToString() : string.Empty;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}

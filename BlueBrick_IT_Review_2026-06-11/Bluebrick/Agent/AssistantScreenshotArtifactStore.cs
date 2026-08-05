using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal static class AssistantScreenshotArtifactStore
    {
        internal static string NewArtifactId()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static string BuildCapturePath(string artifactId, string extension)
        {
            var safeId = SafeId(artifactId);
            var root = DateRoot(DateTime.UtcNow);
            Directory.CreateDirectory(root);
            return Path.Combine(root, "capture_" + safeId + extension);
        }

        internal static AssistantScreenshotReceipt CompleteArtifact(AssistantScreenshotArtifact artifact)
        {
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.Path))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(artifact.ArtifactId))
            {
                artifact.ArtifactId = NewArtifactId();
            }

            artifact.SchemaVersion = AssistantApiEnvelope.CurrentSchemaVersion;
            artifact.ScreenshotId = artifact.ArtifactId;
            artifact.MetadataPath = Path.ChangeExtension(artifact.Path, null) + ".metadata.json";
            artifact.ThumbnailPath = Path.ChangeExtension(artifact.Path, null) + ".thumb.jpg";
            artifact.AnnotationsPath = Path.ChangeExtension(artifact.Path, null) + ".annotations.json";
            artifact.AnnotatedPath = Path.ChangeExtension(artifact.Path, null) + ".annotated.png";

            CreateThumbnail(artifact.Path, artifact.ThumbnailPath);
            File.WriteAllText(artifact.MetadataPath, JsonConvert.SerializeObject(artifact, Formatting.Indented));
            if (!File.Exists(artifact.AnnotationsPath))
            {
                File.WriteAllText(artifact.AnnotationsPath, JsonConvert.SerializeObject(new AssistantScreenshotAnnotationDocument
                {
                    SchemaVersion = AssistantApiEnvelope.CurrentSchemaVersion,
                    ScreenshotId = artifact.ScreenshotId,
                    ImageWidth = artifact.Width,
                    ImageHeight = artifact.Height,
                    Annotations = artifact.Annotations
                }, Formatting.Indented));
            }

            artifact.Receipt = BuildReceipt(artifact);
            File.WriteAllText(artifact.MetadataPath, JsonConvert.SerializeObject(artifact, Formatting.Indented));
            return artifact.Receipt;
        }

        internal static AssistantScreenshotArtifact FindArtifact(string screenshotId)
        {
            var id = SafeId(screenshotId);
            if (string.IsNullOrWhiteSpace(id)) return null;
            var root = Root;
            if (!Directory.Exists(root)) return null;

            foreach (var metadataPath in Directory.GetFiles(root, "capture_" + id + ".metadata.json", SearchOption.AllDirectories))
            {
                try
                {
                    return JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(File.ReadAllText(metadataPath));
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        internal static AssistantScreenshotReceipt BuildReceipt(AssistantScreenshotArtifact artifact)
        {
            if (artifact == null) return null;
            return new AssistantScreenshotReceipt
            {
                ScreenshotId = artifact.ScreenshotId ?? artifact.ArtifactId,
                ArtifactId = artifact.ArtifactId,
                CapturedUtc = artifact.CapturedUtc,
                Width = artifact.Width,
                Height = artifact.Height,
                SourceWindowTitle = artifact.SourceWindowTitle,
                SolidWorksDocumentTitle = artifact.SolidWorksDocumentTitle,
                ImagePath = artifact.Path,
                MetadataPath = artifact.MetadataPath,
                ThumbnailPath = artifact.ThumbnailPath,
                LocalOnly = !artifact.SentToModel,
                SentToModel = artifact.SentToModel,
                RetentionPolicy = artifact.RetentionPolicy,
                ReviewStatus = "pending"
            };
        }

        internal static string Root =>
            Path.Combine(AppIdentity.AssistantHistoryRoot, "AssistantArtifacts", "screenshots");

        private static string DateRoot(DateTime utc)
        {
            return Path.Combine(Root, utc.ToString("yyyy-MM-dd"));
        }

        private static string SafeId(string artifactId)
        {
            var value = (artifactId ?? string.Empty).Trim();
            foreach (var ch in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(ch.ToString(), string.Empty);
            }
            return value;
        }

        private static void CreateThumbnail(string sourcePath, string thumbnailPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;

            using (var source = Image.FromFile(sourcePath))
            {
                var max = 360;
                var ratio = Math.Min((float)max / source.Width, (float)max / source.Height);
                if (ratio > 1f) ratio = 1f;
                var width = Math.Max(1, (int)(source.Width * ratio));
                var height = Math.Max(1, (int)(source.Height * ratio));
                using (var bitmap = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, 0, 0, width, height);
                    var encoder = GetJpegEncoder();
                    if (encoder == null)
                    {
                        bitmap.Save(thumbnailPath, ImageFormat.Jpeg);
                        return;
                    }

                    using (var parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(Encoder.Quality, 82L);
                        bitmap.Save(thumbnailPath, encoder, parameters);
                    }
                }
            }
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            foreach (var encoder in ImageCodecInfo.GetImageEncoders())
            {
                if (string.Equals(encoder.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    return encoder;
                }
            }
            return null;
        }
    }
}

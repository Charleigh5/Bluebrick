using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Linq;

namespace BlueBrick.Agent
{
    internal static class AssistantScreenshotArtifactStore
    {
        private static readonly object ReviewLock = new object();

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
            artifact.MimeType = "image/png";
            artifact.Sha256 = ContentHash(artifact.Path);
            PersistMetadata(artifact);
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
            PersistMetadata(artifact);
            return artifact.Receipt;
        }

        internal static AssistantScreenshotArtifact FindArtifact(string screenshotId, string artifactRoot = null)
        {
            var id = SafeId(screenshotId);
            if (string.IsNullOrWhiteSpace(id)) return null;
            var root = artifactRoot ?? Root;
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
                LocalOnly = !artifact.SentToModel && string.IsNullOrEmpty(artifact.TransmissionState),
                TransmissionState = artifact.TransmissionState ?? "LOCAL_ONLY",
                RuntimeBuildId = artifact.RuntimeBuildId,
                ApprovalMode = artifact.ApprovalMode,
                ApprovalPolicySource = artifact.ApprovalPolicySource,
                SessionId = artifact.SessionId,
                Sha256 = artifact.Sha256,
                SentToModel = artifact.SentToModel,
                RetentionPolicy = artifact.RetentionPolicy,
                ReviewStatus = artifact.ReviewStatus ?? "pending"
            };
        }

        private static void PersistMetadata(AssistantScreenshotArtifact artifact)
        {
            lock (ReviewLock)
            {
                // Annotation/capture refresh cannot overwrite a later review decision.
                if (File.Exists(artifact.MetadataPath))
                {
                    var previous = JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(File.ReadAllText(artifact.MetadataPath));
                    artifact.ReviewStatus = previous?.ReviewStatus ?? "pending";
                    artifact.ReviewedUtc = previous?.ReviewedUtc;
                    artifact.ReviewedBy = previous?.ReviewedBy;
                    artifact.ReviewNote = previous?.ReviewNote;
                    artifact.CloudSendApproved = previous?.CloudSendApproved ?? false;
                    artifact.ApprovedContentHash = previous?.ApprovedContentHash;
                }
                artifact.Receipt = BuildReceipt(artifact);
                WriteMetadataAtomically(artifact.MetadataPath, artifact);
            }
        }

        private static void WriteMetadataAtomically(string path, AssistantScreenshotArtifact artifact)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonConvert.SerializeObject(artifact, Formatting.Indented));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal static AssistantScreenshotArtifact Review(string screenshotId, string targetType, string targetId, string status, string root = null, string approvalMode = "MANUAL", string policySource = "explicit-user-review", string runtimeBuildId = null, string reviewNote = null, string reviewedBy = null)
        {
            if (!Guid.TryParseExact(screenshotId, "N", out _) || screenshotId != targetId ||
                (targetType != "screenshot" && targetType != "screenshot-upload") ||
                (status != "approved" && status != "rejected"))
                throw new ArgumentException("A valid screenshot identity, review target and approved/rejected decision are required.");
            lock (ReviewLock)
            {
                root = root ?? Root;
                var matches = Directory.Exists(root) ? Directory.GetFiles(root, "capture_" + screenshotId + ".metadata.json", SearchOption.AllDirectories) : Array.Empty<string>();
                if (matches.Length != 1) throw new FileNotFoundException("Screenshot artifact was not found uniquely.");
                var metadata = matches[0];
                var artifact = JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(File.ReadAllText(metadata));
                if (artifact == null || artifact.ScreenshotId != screenshotId || artifact.ArtifactId != screenshotId ||
                    !string.Equals(Path.GetFullPath(Path.ChangeExtension(artifact.Path, null) + ".metadata.json"), Path.GetFullPath(metadata), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Screenshot metadata identity does not match.");
                if (targetType == "screenshot-upload")
                {
                    if (status == "approved" && artifact.ReviewStatus != "approved")
                        throw new InvalidOperationException("Approve the local screenshot review before allowing upload.");
                    artifact.CloudSendApproved = status == "approved";
                    artifact.ApprovedContentHash = artifact.CloudSendApproved ? ContentHash(artifact.Path) : null;
                }
                else
                {
                    artifact.ReviewStatus = status;
                    artifact.ApprovalMode = approvalMode;
                    artifact.ApprovalPolicySource = policySource;
                    artifact.RuntimeBuildId = runtimeBuildId ?? artifact.RuntimeBuildId;
                    // Every changed local decision requires fresh, separate transmission consent.
                    artifact.CloudSendApproved = false;
                    artifact.ApprovedContentHash = null;
                }
                artifact.ReviewedUtc = DateTime.UtcNow;
                // Null means "no note supplied" (e.g. auto-approval flows) and preserves
                // any existing note; empty string explicitly clears it on re-review.
                if (reviewNote != null) artifact.ReviewNote = reviewNote;
                if (reviewedBy != null) artifact.ReviewedBy = reviewedBy;
                artifact.Receipt = BuildReceipt(artifact);
                WriteMetadataAtomically(metadata, artifact);
                return artifact;
            }
        }

        internal static void RecordProviderSuccess(string id, string modelId, string root = null)
        {
            lock (ReviewLock)
            {
                var artifact = FindArtifact(id, root);
                if (artifact == null) throw new FileNotFoundException("Provider result artifact is missing.");
                artifact.SentToModel = true;
                artifact.TransmissionState = "SUCCEEDED";
                artifact.ModelProfileId = modelId;
                artifact.Receipt = BuildReceipt(artifact);
                WriteMetadataAtomically(artifact.MetadataPath, artifact);
            }
        }

        internal static void RecordTransmissionAttempt(string id, string modelId, string root = null)
        {
            lock (ReviewLock)
            {
                var artifact = FindArtifact(id, root);
                if (artifact == null) throw new FileNotFoundException("Transmission artifact is missing.");
                artifact.TransmissionState = "ATTEMPTED_OUTCOME_UNKNOWN";
                artifact.ModelProfileId = modelId;
                artifact.Receipt = BuildReceipt(artifact);
                WriteMetadataAtomically(artifact.MetadataPath, artifact);
            }
        }

        internal static void EnsureAttachmentTransmissionAllowed(string path, string root = null)
        {
            root = Path.GetFullPath(root ?? Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
            var metadata = Path.ChangeExtension(path, null) + ".metadata.json";
            if (!File.Exists(metadata)) throw new InvalidOperationException("SCREENSHOT_UPLOAD_NOT_APPROVED: capture metadata is missing.");
            var artifact = JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(File.ReadAllText(metadata));
            if (artifact == null || artifact.ReviewStatus != "approved" || !artifact.CloudSendApproved ||
                !string.Equals(Path.GetFullPath(artifact.Path ?? string.Empty), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(artifact.ApprovedContentHash, ContentHash(path), StringComparison.Ordinal))
                throw new InvalidOperationException("SCREENSHOT_UPLOAD_NOT_APPROVED: approve review and explicitly allow the unchanged image upload.");
        }

        internal static string ContentHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
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

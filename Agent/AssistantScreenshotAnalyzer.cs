using System;
using System.IO;
using System.Text.RegularExpressions;

namespace BlueBrick.Agent
{
    internal static class AssistantScreenshotAnalyzer
    {
        internal const string DeleteOnSessionEndRetentionPolicy = "delete_on_session_end";

        private static readonly Regex EmailRegex = new Regex(
            @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PhoneRegex = new Regex(
            @"(?<!\d)(?:\+?1[\s.\-]?)?(?:\(?\d{3}\)?[\s.\-]?)\d{3}[\s.\-]?\d{4}(?!\d)",
            RegexOptions.Compiled);

        internal static AssistantScreenshotAnalysisResult AnalyzeMock(AssistantScreenshotAnalysisRequest request)
        {
            request = request ?? new AssistantScreenshotAnalysisRequest();
            var artifact = request.Artifact ?? new AssistantScreenshotArtifact
            {
                SessionId = request.SessionId,
                Path = request.Path,
                SourceWindowTitle = request.SourceWindowTitle,
                Width = request.Width,
                Height = request.Height,
                CapturedUtc = DateTime.UtcNow
            };

            EnsurePrivacyMetadata(artifact, request.ModelProfileId, false);

            if (artifact.CapturedUtc == default)
            {
                artifact.CapturedUtc = DateTime.UtcNow;
            }

            if (string.IsNullOrWhiteSpace(artifact.Path))
            {
                artifact.Path = request.Path;
            }

            if (string.IsNullOrWhiteSpace(artifact.SourceWindowTitle))
            {
                artifact.SourceWindowTitle = request.SourceWindowTitle;
            }

            if (artifact.Width <= 0) artifact.Width = Math.Max(0, request.Width);
            if (artifact.Height <= 0) artifact.Height = Math.Max(0, request.Height);

            AddReviewRegion(artifact);
            ExtractContactFromMetadata(artifact, request.HintText);
            WriteMetadata(artifact);

            return new AssistantScreenshotAnalysisResult
            {
                Status = "ok",
                MockMode = true,
                Message = "Mock screenshot analysis completed. Real OCR/vision extraction is still required for pixel-level contact extraction.",
                Artifact = artifact
            };
        }

        internal static void EnsurePrivacyMetadata(AssistantScreenshotArtifact artifact, string modelProfileId, bool sentToModel)
        {
            if (artifact == null) return;
            if (string.IsNullOrWhiteSpace(artifact.ArtifactId))
            {
                artifact.ArtifactId = Guid.NewGuid().ToString("N");
            }
            if (string.IsNullOrWhiteSpace(artifact.RetentionPolicy))
            {
                artifact.RetentionPolicy = DeleteOnSessionEndRetentionPolicy;
            }
            if (!string.IsNullOrWhiteSpace(modelProfileId))
            {
                artifact.ModelProfileId = modelProfileId;
            }
            if (sentToModel)
            {
                artifact.SentToModel = true;
            }
            if (string.IsNullOrWhiteSpace(artifact.SolidWorksDocumentTitle) &&
                !string.IsNullOrWhiteSpace(artifact.SourceWindowTitle) &&
                artifact.SourceWindowTitle.IndexOf("SOLIDWORKS", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                artifact.SolidWorksDocumentTitle = artifact.SourceWindowTitle;
            }
            if (artifact.SolidWorksDocumentPathHash == null)
            {
                artifact.SolidWorksDocumentPathHash = string.Empty;
            }
        }

        private static void AddReviewRegion(AssistantScreenshotArtifact artifact)
        {
            if (artifact.Annotations.Exists(a => string.Equals(a.Id, "mock-review-region", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var width = artifact.Width > 0 ? Math.Max(80, artifact.Width / 2) : 0;
            var height = artifact.Height > 0 ? Math.Max(60, artifact.Height / 3) : 0;
            artifact.Annotations.Add(new AssistantScreenshotAnnotation
            {
                Id = "mock-review-region",
                Label = "Primary review region",
                Severity = "info",
                X = artifact.Width > width ? (artifact.Width - width) / 2 : 0,
                Y = artifact.Height > height ? (artifact.Height - height) / 2 : 0,
                Width = width,
                Height = height,
                Source = "mock-metadata"
            });
        }

        private static void ExtractContactFromMetadata(AssistantScreenshotArtifact artifact, string hintText)
        {
            var text = string.Join(" ", artifact.SourceWindowTitle ?? string.Empty, artifact.Path ?? string.Empty, hintText ?? string.Empty);
            var email = EmailRegex.Match(text);
            var phone = PhoneRegex.Match(text);
            if (!email.Success && !phone.Success) return;

            if (artifact.ExtractedContacts.Exists(c =>
                    string.Equals(c.Email, email.Success ? email.Value : string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Phone, phone.Success ? phone.Value : string.Empty, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            artifact.ExtractedContacts.Add(new AssistantExtractedContact
            {
                Id = "contact-" + Guid.NewGuid().ToString("N"),
                Name = "Detected Contact",
                Company = string.Empty,
                Email = email.Success ? email.Value : string.Empty,
                Phone = phone.Success ? phone.Value : string.Empty,
                Confidence = 0.55,
                SourceAnnotationId = "mock-review-region",
                ReviewStatus = "pending",
                ReviewNote = "Requires human review before CRM, ERP, or document generation use."
            });
        }

        private static void WriteMetadata(AssistantScreenshotArtifact artifact)
        {
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.Path)) return;
            try
            {
                var dir = Path.GetDirectoryName(artifact.Path);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(artifact.Path + ".metadata.json", Newtonsoft.Json.JsonConvert.SerializeObject(artifact, Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
                // Best-effort metadata refresh; analysis response still carries the artifact.
            }
        }
    }
}

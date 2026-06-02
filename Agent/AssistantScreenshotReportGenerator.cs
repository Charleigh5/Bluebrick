using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal static class AssistantScreenshotReportGenerator
    {
        internal static AssistantToolResult GenerateReviewReport(string artifactPathOrMetadataPath, string traceId)
        {
            if (string.IsNullOrWhiteSpace(artifactPathOrMetadataPath))
            {
                return Fail("artifact path or metadata path required", traceId);
            }

            var metadataPath = ResolveMetadataPath(artifactPathOrMetadataPath);
            if (string.IsNullOrWhiteSpace(metadataPath) || !File.Exists(metadataPath))
            {
                return Fail("screenshot artifact metadata not found", traceId);
            }

            var artifact = JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(File.ReadAllText(metadataPath));
            return GenerateReviewReport(artifact, traceId);
        }

        internal static AssistantToolResult GenerateReviewReport(AssistantScreenshotArtifact artifact, string traceId)
        {
            if (artifact == null)
            {
                return Fail("screenshot artifact metadata could not be parsed", traceId);
            }

            artifact.Annotations = artifact.Annotations ?? new System.Collections.Generic.List<AssistantScreenshotAnnotation>();
            artifact.ExtractedContacts = artifact.ExtractedContacts ?? new System.Collections.Generic.List<AssistantExtractedContact>();
            AssistantScreenshotAnalyzer.EnsurePrivacyMetadata(artifact, artifact.ModelProfileId, artifact.SentToModel);
            var outputPath = BuildReportPath(artifact);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, BuildMarkdown(artifact));

            return new AssistantToolResult
            {
                ToolName = "create_screenshot_review_report",
                Status = "ok",
                Message = "Screenshot review report created.",
                ReadOnly = true,
                TraceId = traceId,
                Items =
                {
                    new AssistantToolResultItem
                    {
                        Id = artifact.ArtifactId ?? string.Empty,
                        Title = "Screenshot Review Report",
                        Subtitle = artifact.SourceWindowTitle ?? artifact.SolidWorksDocumentTitle ?? string.Empty,
                        Path = outputPath,
                        Source = "assistant-screenshot-report",
                        Metadata =
                        {
                            ["artifactId"] = artifact.ArtifactId ?? string.Empty,
                            ["annotationCount"] = artifact.Annotations.Count.ToString(),
                            ["contactCount"] = artifact.ExtractedContacts.Count.ToString(),
                            ["reviewStatus"] = "pending"
                        }
                    }
                }
            };
        }

        internal static string ResolveMetadataPath(string artifactPathOrMetadataPath)
        {
            var path = artifactPathOrMetadataPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (path.EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase)) return path;
            var modern = Path.Combine(
                Path.GetDirectoryName(path) ?? string.Empty,
                Path.GetFileNameWithoutExtension(path) + ".metadata.json");
            if (File.Exists(modern)) return modern;
            return path + ".metadata.json";
        }

        internal static string BuildMarkdown(AssistantScreenshotArtifact artifact)
        {
            artifact = artifact ?? new AssistantScreenshotArtifact();
            var builder = new StringBuilder();
            builder.AppendLine("# Screenshot Review Report");
            builder.AppendLine();
            builder.AppendLine("- Artifact ID: " + Safe(artifact.ArtifactId));
            builder.AppendLine("- Captured UTC: " + (artifact.CapturedUtc == default ? "" : artifact.CapturedUtc.ToString("o")));
            builder.AppendLine("- Source window: " + Safe(artifact.SourceWindowTitle));
            builder.AppendLine("- SolidWorks document: " + Safe(artifact.SolidWorksDocumentTitle));
            builder.AppendLine("- Capture source: " + Safe(artifact.CaptureSource));
            builder.AppendLine("- Capture target: " + Safe(artifact.CaptureTarget));
            builder.AppendLine("- Model profile: " + Safe(artifact.ModelProfileId));
            builder.AppendLine("- Privacy: " + (artifact.SentToModel ? "sent to model" : "local only"));
            builder.AppendLine("- Retention: " + Safe(artifact.RetentionPolicy));
            builder.AppendLine();

            builder.AppendLine("## Annotations");
            if (artifact.Annotations.Count == 0)
            {
                builder.AppendLine("- None");
            }
            foreach (var annotation in artifact.Annotations)
            {
                builder.AppendLine("- " + Safe(annotation.Label) + " [" + Safe(annotation.Severity) + "] " +
                                   "x=" + annotation.X + ", y=" + annotation.Y +
                                   ", w=" + annotation.Width + ", h=" + annotation.Height +
                                   ", source=" + Safe(annotation.Source));
            }
            builder.AppendLine();

            builder.AppendLine("## Extracted Contacts");
            if (artifact.ExtractedContacts.Count == 0)
            {
                builder.AppendLine("- None");
            }
            foreach (var contact in artifact.ExtractedContacts)
            {
                builder.AppendLine("- " + Safe(contact.Name) +
                                   " | " + Safe(contact.Company) +
                                   " | " + Safe(contact.Email) +
                                   " | " + Safe(contact.Phone) +
                                   " | confidence=" + contact.Confidence.ToString("0.00") +
                                   " | review=" + Safe(contact.ReviewStatus) +
                                   " | source=" + Safe(contact.SourceAnnotationId));
                if (!string.IsNullOrWhiteSpace(contact.ReviewNote))
                {
                    builder.AppendLine("  - Note: " + Safe(contact.ReviewNote));
                }
            }
            builder.AppendLine();
            builder.AppendLine("## Next Review Actions");
            builder.AppendLine("- Confirm or reject pending contacts before Salesforce, Epicor, or document generation use.");
            builder.AppendLine("- Keep CAD/PDM mutations blocked unless a preview, approval, and receipt exist.");
            return builder.ToString();
        }

        private static string BuildReportPath(AssistantScreenshotArtifact artifact)
        {
            var root = Path.Combine(AppIdentity.AssistantHistoryRoot, "reports", "screenshots");
            var id = string.IsNullOrWhiteSpace(artifact.ArtifactId) ? Guid.NewGuid().ToString("N") : artifact.ArtifactId;
            return Path.Combine(root, "screenshot-review-" + id + ".md");
        }

        private static AssistantToolResult Fail(string message, string traceId)
        {
            return new AssistantToolResult
            {
                ToolName = "create_screenshot_review_report",
                Status = "failed",
                Message = message,
                ReadOnly = true,
                TraceId = traceId
            };
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}

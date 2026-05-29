using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace BlueBrick.Agent
{
    internal sealed class DrawingCheckpointExporter
    {
        private readonly ISldWorks _swApp;

        internal DrawingCheckpointExporter(ISldWorks swApp)
        {
            _swApp = swApp;
        }

        internal List<string> GetSheetNames(DrawingDoc drawingDoc)
        {
            var values = new List<string>();
            if (drawingDoc == null)
            {
                return values;
            }

            var names = drawingDoc.GetSheetNames() as string[];
            if (names != null)
            {
                values.AddRange(names.Where(name => !string.IsNullOrWhiteSpace(name)));
            }

            return values;
        }

        internal ExportedCheckpoint ExportSheet(
            string tempRoot,
            string sheetName,
            GenerateReviewRequest request,
            GenerateReviewResult result,
            GenerationManifest manifest)
        {
            Directory.CreateDirectory(tempRoot);
            var previewPath = Path.Combine(tempRoot, SanitizeFileName(sheetName) + ".jpg");
            string error;
            if (!TryExportPreview(sheetName, previewPath, out error))
            {
                throw new InvalidOperationException(error);
            }

            var metadata = BuildMetadata(sheetName, request, result, manifest);
            var metadataHash = ComputeHash(metadata.ToString(Newtonsoft.Json.Formatting.None));
            var artifactHash = ComputeHash(File.ReadAllBytes(previewPath));
            metadata["artifact_hash"] = artifactHash;
            metadata["metadata_hash"] = metadataHash;

            return new ExportedCheckpoint
            {
                SheetId = sheetName,
                ViewId = sheetName,
                PreviewPath = previewPath,
                Metadata = metadata,
                ArtifactHash = artifactHash,
                MetadataHash = metadataHash
            };
        }

        private bool TryExportPreview(string sheetName, string previewPath, out string error)
        {
            error = null;
            try
            {
                var activeDoc = _swApp.ActiveDoc as ModelDoc2;
                var drawingDoc = activeDoc as DrawingDoc;
                if (activeDoc == null || drawingDoc == null)
                {
                    error = "Active SOLIDWORKS drawing is unavailable.";
                    return false;
                }

                drawingDoc.ActivateSheet(sheetName);
                var extension = activeDoc.Extension;
                if (extension == null)
                {
                    error = "SOLIDWORKS document extension unavailable.";
                    return false;
                }

                var saveErrors = 0;
                var saveWarnings = 0;
                var ok = extension.SaveAs(
                    previewPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    ref saveErrors,
                    ref saveWarnings);
                if (!ok || !File.Exists(previewPath))
                {
                    error = "Failed to export preview for " + sheetName + ".";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Preview export failed for " + sheetName + ": " + ex.Message;
                return false;
            }
        }

        private static JObject BuildMetadata(
            string sheetName,
            GenerateReviewRequest request,
            GenerateReviewResult result,
            GenerationManifest manifest)
        {
            return new JObject
            {
                ["job_id"] = result.JobId ?? string.Empty,
                ["trace_id"] = manifest.TraceId ?? string.Empty,
                ["customer_id"] = request.CustomerId ?? string.Empty,
                ["packet_name"] = request.PacketName ?? string.Empty,
                ["sheet_name"] = sheetName ?? string.Empty,
                ["service_pack"] = request.ServicePack ?? string.Empty,
                ["standards_baseline"] = request.StandardsBaseline ?? "ASME",
                ["standards_version"] = request.StandardsVersion ?? "Y14.100",
                ["customer_ruleset_version"] = request.CustomerRulesetVersion ?? "default",
                ["analysis_mode"] = request.AnalysisMode ?? "incremental_checkpoint",
                ["auto_action_policy"] = request.AutoActionPolicy ?? "supervised_auto_fix",
                ["source_model_path"] = manifest.SourceModelPath ?? string.Empty,
                ["captured_at_utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "sheet";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(invalid.Contains(ch) ? '_' : ch);
            }

            return builder.ToString();
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(bytes);
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string ComputeHash(string value)
        {
            return ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }
    }

    internal sealed class ExportedCheckpoint
    {
        internal string SheetId { get; set; }
        internal string ViewId { get; set; }
        internal string PreviewPath { get; set; }
        internal JObject Metadata { get; set; } = new JObject();
        internal string ArtifactHash { get; set; }
        internal string MetadataHash { get; set; }
    }
}

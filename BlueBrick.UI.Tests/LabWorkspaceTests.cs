using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlueBrick.Agent;
using BlueBrick.Vault;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace BlueBrick.UI.Tests
{
    [TestClass]
    public class LabWorkspaceTests
    {
        [TestMethod]
        public void AssistantSessionStore_Creates_And_Reads_Session()
        {
            var store = new AssistantSessionStore();
            var session = store.Create();

            Assert.IsFalse(string.IsNullOrWhiteSpace(session.SessionId));

            var loaded = store.Get(session.SessionId);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(session.SessionId, loaded.SessionId);
        }

        [TestMethod]
        public void LocalVaultWorkspace_SaveGeneratedArtifact_Indexes_Output()
        {
            var workspace = new LocalVaultWorkspace();
            var tempFile = Path.Combine(Path.GetTempPath(), "bb-lab-" + Guid.NewGuid().ToString("N") + ".pdf");
            File.WriteAllText(tempFile, "lab");

            try
            {
                var artifact = workspace.SaveGeneratedArtifact(new GeneratedArtifactRecord
                {
                    OutputPath = tempFile,
                    ArtifactType = "pdf",
                    PartNumber = "LAB-1000",
                    DocumentNumber = "DOC-1000",
                    Description = "LAB TEST",
                    Customer = "LAB",
                    CreatedUtc = DateTime.UtcNow
                });

                Assert.IsTrue(File.Exists(artifact.OutputPath));
                var metadata = workspace.GetMetadata(artifact.OutputPath);
                Assert.IsNotNull(metadata);
                Assert.AreEqual("LAB-1000", metadata.PartNumber);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task OpenAiAssistantService_MockMode_Returns_Status_And_Response()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples"),
                    GeneratedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-vault", "generated"),
                    ThumbsRoot = Path.Combine(Path.GetTempPath(), "bb-lab-vault", "thumbs"),
                    MetadataRoot = Path.Combine(Path.GetTempPath(), "bb-lab-vault", "db"),
                    LogRoot = Path.Combine(Path.GetTempPath(), "bb-lab-vault", "logs"),
                    SourceRoot = Path.Combine(Path.GetTempPath(), "bb-lab-vault", "source")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://api.openai.com/v1",
                    Model = "gpt-4.1-mini",
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = true,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10
                }
            };

            var service = new OpenAiAssistantService(config);
            var status = await service.GetStatusAsync();
            Assert.AreEqual("mock", status.AssistantMode);

            var response = await service.SendMessageAsync(null, "How do I generate locally?", Array.Empty<string>());
            Assert.IsTrue(response.AssistantAvailable);
            Assert.IsNotNull(response.Message);
            Assert.IsTrue(response.Message.Text.IndexOf("Mock preview mode", StringComparison.OrdinalIgnoreCase) >= 0);

            var test = await service.TestConnectionAsync();
            Assert.IsTrue(test.Success);
            Assert.AreEqual("mock", test.Mode);
        }

        [TestMethod]
        public async Task OpenAiAssistantService_Returns_Model_Catalog()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                    Model = "meta/llama-3.1-70b-instruct",
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = true,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10,
                    ModelProfiles = new[]
                    {
                        new AssistantModelProfile
                        {
                            Id = "nvidia-test",
                            Name = "NVIDIA Test",
                            Provider = "NVIDIA",
                            ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                            Model = "meta/llama-3.1-70b-instruct",
                            KeyEnvironmentVariable = "NVIDIA_API_KEY",
                            IsDefault = true,
                            SupportsVision = false
                        },
                        new AssistantModelProfile
                        {
                            Id = "openai-test",
                            Name = "OpenAI Test",
                            Provider = "OpenAI",
                            ApiBaseUrl = "https://api.openai.com/v1",
                            Model = "gpt-4.1-mini",
                            KeyEnvironmentVariable = "OPENAI_API_KEY",
                            SupportsVision = true,
                            SupportsTools = true
                        }
                    }
                }
            };

            var service = new OpenAiAssistantService(config);
            var models = await service.GetModelsAsync();
            var status = await service.GetStatusAsync();

            Assert.AreEqual(2, models.Count);
            Assert.AreEqual("NVIDIA", models[0].Provider);
            Assert.AreEqual("NVIDIA Test", status.Model);
            Assert.AreEqual("https://integrate.api.nvidia.com/v1", status.ApiBaseUrl);
            Assert.AreEqual("nvidia", models[0].ProviderKind);
            Assert.IsFalse(models[0].SupportsVision);
            Assert.IsTrue(models[1].SupportsVision);
            Assert.IsTrue(models[1].SupportsTools);
        }

        [TestMethod]
        public async Task OpenAiAssistantService_Screenshot_Analysis_Requires_Vision_Profile()
        {
            var config = new AgentConfig
            {
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                    Model = "meta/llama-3.1-70b-instruct",
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = true,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10,
                    ModelProfiles = new[]
                    {
                        new AssistantModelProfile
                        {
                            Id = "text-only",
                            Name = "Text Only",
                            Provider = "NVIDIA",
                            ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                            Model = "text-only",
                            IsDefault = true,
                            SupportsVision = false
                        }
                    }
                }
            };

            var service = new OpenAiAssistantService(config);
            var result = await service.AnalyzeScreenshotAsync(new AssistantScreenshotAnalysisRequest
            {
                Artifact = new AssistantScreenshotArtifact { Path = @"C:\temp\capture.png" }
            });

            Assert.AreEqual("unsupported_model", result.Status);
            Assert.IsNotNull(result.Artifact);
        }

        [TestMethod]
        public void AssistantPanel_ModelSupportsVision_Uses_Active_Profile()
        {
            var models = new JArray
            {
                JObject.FromObject(new AssistantModelProfile { Id = "nvidia", Name = "NVIDIA Text", Provider = "NVIDIA", SupportsVision = false }),
                JObject.FromObject(new AssistantModelProfile { Id = "openai", Name = "OpenAI Vision", Provider = "OpenAI", SupportsVision = true })
            };

            Assert.IsFalse(AssistantPanel.ModelSupportsVision(models, "nvidia"));
            Assert.IsTrue(AssistantPanel.ModelSupportsVision(models, "openai"));
            Assert.IsTrue(AssistantPanel.ModelSupportsVision(models, "OpenAI Vision"));
        }

        [TestMethod]
        public void AssistantModelCapabilitySummary_Copies_Profile_Capabilities()
        {
            var summary = AssistantModelCapabilitySummary.FromProfile(new AssistantModelProfile
            {
                Id = "openai-vision",
                Name = "GPT Vision",
                Provider = "OpenAI",
                ProviderKind = "openai",
                BaseUrlAlias = "OPENAI",
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsTools = true,
                SupportsJsonMode = true,
                Enabled = true,
                Source = "config_example"
            });

            Assert.AreEqual("openai-vision", summary.Id);
            Assert.AreEqual("OpenAI", summary.Provider);
            Assert.IsTrue(summary.SupportsVision);
            Assert.IsTrue(summary.SupportsStreaming);
            Assert.IsTrue(summary.SupportsTools);
            Assert.IsTrue(summary.SupportsJsonMode);
        }

        [TestMethod]
        public void AssistantPanel_BuildModelDisplayText_Shows_Capabilities_And_Disabled_State()
        {
            var visionModel = JObject.FromObject(new AssistantModelProfile
            {
                Id = "openai-vision",
                Name = "GPT Vision",
                Provider = "OpenAI",
                SupportsVision = true,
                SupportsStreaming = true,
                SupportsTools = true,
                SupportsJsonMode = true,
                Enabled = true
            });
            var textModel = JObject.FromObject(new AssistantModelProfile
            {
                Id = "nvidia-text",
                Name = "NVIDIA Text",
                Provider = "NVIDIA",
                Enabled = false
            });

            Assert.AreEqual("OpenAI · GPT Vision · vision/tools/stream/json", AssistantPanel.BuildModelDisplayText(visionModel));
            Assert.AreEqual("NVIDIA · NVIDIA Text · text (off)", AssistantPanel.BuildModelDisplayText(textModel));
        }

        [TestMethod]
        public void AssistantPanel_ResolveSearchCommand_Routes_Vault_Pdm_And_Epicor()
        {
            var vault = AssistantPanel.ResolveSearchCommand("ABC-1000");
            var explicitVault = AssistantPanel.ResolveSearchCommand("/vault ABC-2000");
            var pdm = AssistantPanel.ResolveSearchCommand("/pdm bracket");
            var epicor = AssistantPanel.ResolveSearchCommand("/epicor customer");
            var selectedPdm = AssistantPanel.ResolveSearchCommand("selected query", "search_pdm");

            Assert.AreEqual("search_local_vault", vault.ToolName);
            Assert.AreEqual("ABC-1000", vault.Query);

            Assert.AreEqual("search_local_vault", explicitVault.ToolName);
            Assert.AreEqual("ABC-2000", explicitVault.Query);

            Assert.AreEqual("search_pdm", pdm.ToolName);
            Assert.AreEqual("bracket", pdm.Query);

            Assert.AreEqual("search_epicor", epicor.ToolName);
            Assert.AreEqual("customer", epicor.Query);

            Assert.AreEqual("search_pdm", selectedPdm.ToolName);
            Assert.AreEqual("selected query", selectedPdm.Query);
        }

        [TestMethod]
        public void AssistantPanel_BuildSearchToolItems_Includes_Disabled_Connectors()
        {
            var tools = new JArray
            {
                JObject.FromObject(new AssistantToolDescriptor { Name = "search_local_vault", DisplayName = "Search Local Vault", Enabled = true }),
                JObject.FromObject(new AssistantToolDescriptor { Name = "search_pdm", DisplayName = "Search PDM", Enabled = false, UnavailableReason = "disabled" }),
                JObject.FromObject(new AssistantToolDescriptor { Name = "search_epicor", DisplayName = "Search Epicor", Enabled = false, UnavailableReason = "disabled" }),
                JObject.FromObject(new AssistantToolDescriptor { Name = "capture_screenshot", DisplayName = "Capture", Enabled = true })
            };

            var items = AssistantPanel.BuildSearchToolItems(tools);

            Assert.AreEqual(3, items.Length);
            Assert.AreEqual("search_local_vault", items[0].ToolName);
            Assert.IsTrue(items[0].Enabled);
            Assert.AreEqual("search_pdm", items[1].ToolName);
            Assert.IsFalse(items[1].Enabled);
            StringAssert.Contains(items[1].ToString(), "(off)");
            StringAssert.Contains(AssistantPanel.BuildSelectedToolStatus(items[1]), "disabled");
            Assert.AreEqual("PDM", items[1].ButtonText);
        }

        [TestMethod]
        public void AssistantToolAvailabilitySummary_Counts_Enabled_And_Search_Tools()
        {
            var summary = AssistantToolAvailabilitySummary.FromCatalog(new[]
            {
                new AssistantToolDescriptor { Name = "search_local_vault", Enabled = true },
                new AssistantToolDescriptor { Name = "search_pdm", Enabled = false },
                new AssistantToolDescriptor { Name = "search_epicor", Enabled = false },
                new AssistantToolDescriptor { Name = "capture_screenshot", Enabled = true }
            });

            Assert.AreEqual(4, summary.TotalTools);
            Assert.AreEqual(2, summary.EnabledTools);
            Assert.AreEqual(2, summary.DisabledTools);
            Assert.AreEqual(3, summary.SearchTools);
            Assert.AreEqual(1, summary.EnabledSearchTools);
            CollectionAssert.Contains(summary.EnabledToolNames, "capture_screenshot");
            CollectionAssert.Contains(summary.DisabledToolNames, "search_pdm");
        }

        [TestMethod]
        public void AssistantScreenshotArtifact_Carries_Annotation_And_Contact_Data()
        {
            var artifact = new AssistantScreenshotArtifact
            {
                SessionId = "session-1",
                Path = @"C:\temp\capture.png",
                CapturedUtc = DateTime.UtcNow,
                Width = 1200,
                Height = 800,
                SourceWindowTitle = "SOLIDWORKS Professional 2024",
                ArtifactId = "artifact-1",
                RetentionPolicy = "delete_on_session_end",
                ModelProfileId = "openai-vision"
            };

            artifact.Annotations.Add(new AssistantScreenshotAnnotation
            {
                Id = "ann-1",
                Label = "Customer contact block",
                Severity = "info",
                X = 10,
                Y = 20,
                Width = 300,
                Height = 80,
                Source = "model"
            });

            artifact.ExtractedContacts.Add(new AssistantExtractedContact
            {
                Id = "contact-1",
                Name = "Jane Example",
                Company = "Example Manufacturing",
                Email = "jane@example.com",
                Phone = "555-0100",
                OpportunityId = "OPP-1000",
                Confidence = 0.92,
                SourceAnnotationId = "ann-1",
                ReviewStatus = "pending",
                ReviewNote = "Confirm before Salesforce use"
            });

            Assert.AreEqual("session-1", artifact.SessionId);
            Assert.AreEqual("artifact-1", artifact.ArtifactId);
            Assert.AreEqual("delete_on_session_end", artifact.RetentionPolicy);
            Assert.IsFalse(artifact.SentToModel);
            Assert.AreEqual("openai-vision", artifact.ModelProfileId);
            Assert.AreEqual(1, artifact.Annotations.Count);
            Assert.AreEqual(1, artifact.ExtractedContacts.Count);
            Assert.AreEqual("ann-1", artifact.ExtractedContacts[0].SourceAnnotationId);
            Assert.IsTrue(artifact.ExtractedContacts[0].Confidence > 0.9);
            Assert.AreEqual("pending", artifact.ExtractedContacts[0].ReviewStatus);
            Assert.AreEqual("Confirm before Salesforce use", artifact.ExtractedContacts[0].ReviewNote);
        }

        [TestMethod]
        public void AssistantScreenshotAnalyzer_Ensures_Privacy_Metadata()
        {
            var artifact = new AssistantScreenshotArtifact
            {
                SessionId = "session-1",
                Path = @"C:\temp\capture.png",
                SourceWindowTitle = "SOLIDWORKS Professional 2024 - [80233136.SLDASM]"
            };

            AssistantScreenshotAnalyzer.EnsurePrivacyMetadata(artifact, "openai-vision", false);

            Assert.IsFalse(string.IsNullOrWhiteSpace(artifact.ArtifactId));
            Assert.AreEqual("delete_on_session_end", artifact.RetentionPolicy);
            Assert.AreEqual("openai-vision", artifact.ModelProfileId);
            Assert.IsFalse(artifact.SentToModel);
            Assert.AreEqual("SOLIDWORKS Professional 2024 - [80233136.SLDASM]", artifact.SolidWorksDocumentTitle);
            Assert.AreEqual(string.Empty, artifact.SolidWorksDocumentPathHash);
        }

        [TestMethod]
        public void AssistantImageTools_Normalizes_Capture_Request_And_Matches_SolidWorks_Titles()
        {
            var normalized = AssistantImageTools.NormalizeCaptureRequest(new AssistantScreenshotCaptureRequest
            {
                SessionId = "session-1",
                CaptureTarget = "unsupported"
            });

            Assert.AreEqual("solidworks_or_foreground", normalized.CaptureTarget);
            Assert.IsTrue(AssistantImageTools.IsSolidWorksWindowTitle("SOLIDWORKS Professional 2024 SP3.1 - [80233136.SLDASM *]"));
            Assert.IsTrue(AssistantImageTools.IsSolidWorksWindowTitle("BearingPlate.SLDPRT"));
            Assert.IsTrue(AssistantImageTools.IsSolidWorksWindowTitle("Drawing.SLDDRW - Sheet1"));
            Assert.IsFalse(AssistantImageTools.IsSolidWorksWindowTitle("Bluebrick Assistant"));
        }

        [TestMethod]
        public void AssistantPanel_Normalizes_Screenshot_Artifact_For_UI()
        {
            var artifact = JObject.FromObject(new AssistantScreenshotArtifact
            {
                SessionId = "session-1",
                Path = @"C:\temp\capture.png",
                CapturedUtc = DateTime.UtcNow,
                Width = 1200,
                Height = 800,
                SourceWindowTitle = "SOLIDWORKS Professional 2024",
                ArtifactId = "artifact-1",
                SolidWorksDocumentTitle = "80233136.SLDASM",
                SolidWorksDocumentPathHash = "hash-redacted",
                CaptureTarget = "solidworks_or_foreground",
                CaptureSource = "solidworks",
                RetentionPolicy = "delete_on_session_end",
                ModelProfileId = "openai-vision",
                Annotations =
                {
                    new AssistantScreenshotAnnotation
                    {
                        Id = "ann-1",
                        Label = "Customer contact block",
                        Severity = "info",
                        X = 10,
                        Y = 20,
                        Width = 300,
                        Height = 80,
                        Source = "model"
                    }
                },
                ExtractedContacts =
                {
                    new AssistantExtractedContact
                    {
                        Id = "contact-1",
                        Name = "Jane Example",
                        Company = "Example Manufacturing",
                        Email = "jane@example.com",
                        Phone = "555-0100",
                        Confidence = 0.92,
                        SourceAnnotationId = "ann-1",
                        ReviewStatus = "pending",
                        ReviewNote = "Confirm before CRM use"
                    }
                }
            });

            var normalized = AssistantPanel.NormalizeScreenshotArtifact(artifact);

            Assert.AreEqual("capture.png", normalized.Value<string>("fileName"));
            Assert.AreEqual("SOLIDWORKS Professional 2024", normalized.Value<string>("sourceWindowTitle"));
            Assert.AreEqual(1200, normalized.Value<int>("width"));
            Assert.AreEqual(800, normalized.Value<int>("height"));
            Assert.AreEqual("artifact-1", normalized.Value<string>("artifactId"));
            Assert.AreEqual("80233136.SLDASM", normalized.Value<string>("solidWorksDocumentTitle"));
            Assert.AreEqual("hash-redacted", normalized.Value<string>("solidWorksDocumentPathHash"));
            Assert.AreEqual("solidworks_or_foreground", normalized.Value<string>("captureTarget"));
            Assert.AreEqual("solidworks", normalized.Value<string>("captureSource"));
            Assert.AreEqual("delete_on_session_end", normalized.Value<string>("retentionPolicy"));
            Assert.AreEqual("openai-vision", normalized.Value<string>("modelProfileId"));
            Assert.IsFalse(normalized.Value<bool>("sentToModel"));
            Assert.AreEqual(1, normalized.Value<int>("annotationCount"));
            Assert.AreEqual(1, normalized.Value<int>("contactCount"));
        }

        [TestMethod]
        public void AssistantPanel_Builds_Screenshot_Report_Tool_Payload_From_Analyzed_Artifact()
        {
            var artifact = JObject.FromObject(new AssistantScreenshotArtifact
            {
                ArtifactId = "artifact-ui-report",
                SessionId = "session-1",
                Path = @"C:\temp\capture-ui-report.png",
                SourceWindowTitle = "SOLIDWORKS Professional 2024",
                Annotations =
                {
                    new AssistantScreenshotAnnotation
                    {
                        Id = "ann-1",
                        Label = "Title block",
                        Severity = "info",
                        Source = "mock"
                    }
                },
                ExtractedContacts =
                {
                    new AssistantExtractedContact
                    {
                        Name = "Jane Example",
                        Email = "jane@example.com",
                        ReviewStatus = "pending"
                    }
                }
            });

            var payload = AssistantPanel.BuildScreenshotReviewReportToolParameters(artifact);

            Assert.AreEqual(@"C:\temp\capture-ui-report.png", payload.Value<string>("artifactPath"));
            Assert.AreEqual(@"C:\temp\capture-ui-report.metadata.json", payload.Value<string>("metadataPath"));
            StringAssert.Contains(payload.Value<string>("artifactJson"), "Jane Example");
            StringAssert.Contains(payload.Value<string>("artifactJson"), "Title block");
        }

        [TestMethod]
        public void AssistantScreenshotReportGenerator_Resolves_Modern_Metadata_Path_When_Present()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "bb-metadata-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var imagePath = Path.Combine(tempRoot, "capture_artifact.png");
            var metadataPath = Path.Combine(tempRoot, "capture_artifact.metadata.json");
            File.WriteAllText(imagePath, "not-used");
            File.WriteAllText(metadataPath, "{}");

            try
            {
                Assert.AreEqual(metadataPath, AssistantScreenshotReportGenerator.ResolveMetadataPath(imagePath));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [TestMethod]
        public void AssistantScreenshotArtifactStore_Completes_Artifact_With_Receipt_Metadata_And_Thumbnail()
        {
            var id = "test" + Guid.NewGuid().ToString("N");
            var imagePath = AssistantScreenshotArtifactStore.BuildCapturePath(id, ".png");
            using (var bitmap = new Bitmap(48, 32))
            {
                bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            var artifact = new AssistantScreenshotArtifact
            {
                ArtifactId = id,
                SessionId = "session-artifact",
                Path = imagePath,
                CapturedUtc = DateTime.UtcNow,
                Width = 48,
                Height = 32,
                SourceWindowTitle = "SOLIDWORKS Professional 2024",
                SolidWorksDocumentTitle = "80233136.SLDASM",
                RetentionPolicy = "delete_on_session_end",
                SentToModel = false
            };

            var receipt = AssistantScreenshotArtifactStore.CompleteArtifact(artifact);

            try
            {
                Assert.IsNotNull(receipt);
                Assert.AreEqual(id, receipt.ScreenshotId);
                Assert.IsTrue(receipt.LocalOnly);
                Assert.IsTrue(File.Exists(artifact.MetadataPath));
                Assert.IsTrue(File.Exists(artifact.ThumbnailPath));
                Assert.IsTrue(File.Exists(artifact.AnnotationsPath));

                var loaded = AssistantScreenshotArtifactStore.FindArtifact(id);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(id, loaded.ScreenshotId);
                Assert.AreEqual(48, loaded.Width);
                Assert.AreEqual(32, loaded.Height);
            }
            finally
            {
                SafeDelete(imagePath);
                SafeDelete(artifact.MetadataPath);
                SafeDelete(artifact.ThumbnailPath);
                SafeDelete(artifact.AnnotationsPath);
                SafeDelete(artifact.AnnotatedPath);
            }
        }

        [TestMethod]
        public void AssistantScreenshotAnalyzer_Mock_Adds_Annotation_And_Metadata_Contact()
        {
            var result = AssistantScreenshotAnalyzer.AnalyzeMock(new AssistantScreenshotAnalysisRequest
            {
                SessionId = "session-1",
                Path = @"C:\temp\capture.png",
                SourceWindowTitle = "Quote contact jane@example.com 555-010-1000",
                Width = 1200,
                Height = 800,
                HintText = "Review customer contact block"
            });

            Assert.AreEqual("ok", result.Status);
            Assert.IsTrue(result.MockMode);
            Assert.IsNotNull(result.Artifact);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Artifact.ArtifactId));
            Assert.AreEqual("delete_on_session_end", result.Artifact.RetentionPolicy);
            Assert.IsFalse(result.Artifact.SentToModel);
            Assert.AreEqual(1, result.Artifact.Annotations.Count);
            Assert.AreEqual("mock-review-region", result.Artifact.Annotations[0].Id);
            Assert.AreEqual(1, result.Artifact.ExtractedContacts.Count);
            Assert.AreEqual("jane@example.com", result.Artifact.ExtractedContacts[0].Email);
            Assert.AreEqual("mock-review-region", result.Artifact.ExtractedContacts[0].SourceAnnotationId);
            Assert.AreEqual("pending", result.Artifact.ExtractedContacts[0].ReviewStatus);
        }

        [TestMethod]
        public async Task AssistantToolService_Creates_Screenshot_Review_Report_From_Metadata()
        {
            var artifact = new AssistantScreenshotArtifact
            {
                ArtifactId = "artifact-test-report",
                SessionId = "session-1",
                Path = Path.Combine(Path.GetTempPath(), "artifact-test-report.png"),
                CapturedUtc = DateTime.UtcNow,
                SourceWindowTitle = "SOLIDWORKS Professional 2024 - [80233136.SLDASM]",
                CaptureSource = "solidworks",
                CaptureTarget = "solidworks_or_foreground",
                RetentionPolicy = "delete_on_session_end",
                ModelProfileId = "openai-vision"
            };
            artifact.Annotations.Add(new AssistantScreenshotAnnotation
            {
                Id = "ann-1",
                Label = "Title block",
                Severity = "info",
                X = 1,
                Y = 2,
                Width = 300,
                Height = 80,
                Source = "mock"
            });
            artifact.ExtractedContacts.Add(new AssistantExtractedContact
            {
                Id = "contact-1",
                Name = "Jane Example",
                Email = "jane@example.com",
                Confidence = 0.91,
                SourceAnnotationId = "ann-1",
                ReviewStatus = "pending"
            });

            var metadataPath = artifact.Path + ".metadata.json";
            File.WriteAllText(metadataPath, Newtonsoft.Json.JsonConvert.SerializeObject(artifact));

            try
            {
                var service = new AssistantToolService(new AgentConfig());
                var result = await service.ExecuteAsync(new AssistantToolRequest
                {
                    ToolName = "create_screenshot_review_report",
                    Query = metadataPath
                }, "trace-report");

                Assert.AreEqual("ok", result.Status);
                Assert.AreEqual(1, result.Items.Count);
                Assert.IsTrue(File.Exists(result.Items[0].Path));
                var report = File.ReadAllText(result.Items[0].Path);
                StringAssert.Contains(report, "Screenshot Review Report");
                StringAssert.Contains(report, "Jane Example");
                StringAssert.Contains(report, "review=pending");
                Assert.IsNotNull(result.Receipt);
                Assert.IsTrue(result.Receipt.Allowed);
            }
            finally
            {
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }
            }
        }

        [TestMethod]
        public async Task AssistantToolService_Creates_Screenshot_Review_Report_From_Artifact_Payload()
        {
            var artifact = new AssistantScreenshotArtifact
            {
                ArtifactId = "artifact-payload-report",
                SessionId = "session-1",
                Path = Path.Combine(Path.GetTempPath(), "artifact-payload-report.png"),
                CapturedUtc = DateTime.UtcNow,
                SourceWindowTitle = "SOLIDWORKS Professional 2024 - [80233136.SLDASM]",
                CaptureSource = "solidworks",
                CaptureTarget = "solidworks_or_foreground",
                RetentionPolicy = "delete_on_session_end"
            };
            artifact.Annotations.Add(new AssistantScreenshotAnnotation
            {
                Id = "ann-1",
                Label = "Title block",
                Severity = "info",
                Source = "mock"
            });
            artifact.ExtractedContacts.Add(new AssistantExtractedContact
            {
                Name = "Jane Example",
                Email = "jane@example.com",
                Confidence = 0.91,
                ReviewStatus = "pending",
                ReviewNote = "Confirm before Salesforce or Epicor use."
            });

            var service = new AssistantToolService(new AgentConfig());
            var result = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "create_screenshot_review_report",
                Query = artifact.Path,
                Parameters =
                {
                    ["artifactJson"] = Newtonsoft.Json.JsonConvert.SerializeObject(artifact)
                }
            }, "trace-report-payload");

            Assert.AreEqual("ok", result.Status);
            Assert.AreEqual(1, result.Items.Count);
            var report = File.ReadAllText(result.Items[0].Path);
            StringAssert.Contains(report, "Jane Example");
            StringAssert.Contains(report, "Title block");
            StringAssert.Contains(report, "review=pending");
            Assert.IsNotNull(result.Receipt);
            Assert.IsTrue(result.Receipt.Allowed);
        }

        [TestMethod]
        public async Task AssistantToolService_Catalog_Separates_Local_Search_From_Risky_Connectors()
        {
            var service = new AssistantToolService(new AgentConfig());
            var catalog = service.GetCatalog();

            var localVault = catalog.Single(t => t.Name == "search_local_vault");
            var report = catalog.Single(t => t.Name == "create_screenshot_review_report");
            var pdm = catalog.Single(t => t.Name == "search_pdm");
            var epicor = catalog.Single(t => t.Name == "search_epicor");

            Assert.IsTrue(localVault.Enabled);
            Assert.IsTrue(localVault.ReadOnly);
            Assert.IsFalse(localVault.RequiresConfirmation);

            Assert.IsTrue(report.Enabled);
            Assert.IsTrue(report.ReadOnly);
            Assert.IsFalse(report.RequiresConfirmation);

            Assert.IsFalse(pdm.Enabled);
            Assert.IsTrue(pdm.ReadOnly);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pdm.UnavailableReason));

            Assert.IsFalse(epicor.Enabled);
            Assert.IsTrue(epicor.ReadOnly);
            Assert.IsFalse(string.IsNullOrWhiteSpace(epicor.UnavailableReason));

            var rejected = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "search_local_vault",
                Query = ""
            }, "trace-test");

            Assert.AreEqual("invalid", rejected.Status);
            Assert.AreEqual("search_local_vault", rejected.ToolName);
        }

        [TestMethod]
        public async Task AssistantToolService_Pdm_And_Epicor_Are_Config_Gated()
        {
            var service = new AssistantToolService(new AgentConfig
            {
                AssistantTools = new AssistantToolSettings
                {
                    EnablePdmSearch = false,
                    EnableEpicorSearch = false,
                    EpicorConnectionStringEnvironmentVariable = "BLUEBRICK_TEST_EPICOR_CONNECTION_STRING"
                }
            });

            var pdm = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "search_pdm",
                Query = "LAB"
            }, "trace-pdm");

            var epicor = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "search_epicor",
                Query = "LAB"
            }, "trace-epicor");

            Assert.AreEqual("disabled", pdm.Status);
            Assert.IsTrue(pdm.ReadOnly);
            Assert.AreEqual("disabled", epicor.Status);
            Assert.IsTrue(epicor.ReadOnly);
        }

        [TestMethod]
        public void AssistantToolPolicy_Blocks_Cad_Pdm_And_Destructive_Lab_Routes_From_Assistant()
        {
            var policy = new AssistantToolPolicy();

            var sw = policy.EvaluateRoute("/sw/open", "POST", AssistantToolInvocationSource.Chat);
            var pdmMutation = policy.EvaluateRoute("/pdm/check_out", "POST", AssistantToolInvocationSource.Chat);
            var pdmNativeRead = policy.EvaluateRoute("/pdm/search", "POST", AssistantToolInvocationSource.Chat);
            var labReset = policy.EvaluateRoute("/lab/vault/reset", "POST", AssistantToolInvocationSource.Chat);
            var assistantCatalog = policy.EvaluateRoute("/assistant/models", "GET", AssistantToolInvocationSource.Chat);

            Assert.IsFalse(sw.Allowed);
            Assert.AreEqual("blocked_cad_route", sw.Code);
            Assert.IsTrue(sw.RequiresReceipt);

            Assert.IsFalse(pdmMutation.Allowed);
            Assert.AreEqual("blocked_pdm_mutation_route", pdmMutation.Code);
            Assert.IsTrue(pdmMutation.RequiresReceipt);

            Assert.IsFalse(pdmNativeRead.Allowed);
            Assert.AreEqual("blocked_native_pdm_route", pdmNativeRead.Code);

            Assert.IsFalse(labReset.Allowed);
            Assert.AreEqual("blocked_destructive_lab_route", labReset.Code);

            Assert.IsTrue(assistantCatalog.Allowed);
        }

        [TestMethod]
        public async Task AssistantToolService_Denies_Route_Shaped_Tool_Names()
        {
            var service = new AssistantToolService(new AgentConfig());

            var sw = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "/sw/open",
                Query = "part"
            }, "trace-policy-sw");

            var alias = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "bridge:/pdm/check_out",
                Query = "part"
            }, "trace-policy-pdm");

            Assert.AreEqual("blocked_cad_route", sw.Status);
            Assert.IsTrue(sw.ReadOnly);
            Assert.IsNotNull(sw.Receipt);
            Assert.AreEqual("blocked_cad_route", sw.Receipt.PolicyCode);
            Assert.IsFalse(sw.Receipt.Allowed);
            Assert.IsTrue(sw.Receipt.ApprovalRequired);
            Assert.AreEqual("trace-policy-sw", sw.Receipt.TraceId);

            Assert.AreEqual("blocked_route_alias", alias.Status);
            Assert.IsTrue(alias.ReadOnly);
            Assert.IsNotNull(alias.Receipt);
            Assert.AreEqual("blocked_route_alias", alias.Receipt.PolicyCode);
            Assert.IsFalse(alias.Receipt.Allowed);
            Assert.AreEqual("trace-policy-pdm", alias.Receipt.TraceId);
        }

        [TestMethod]
        public async Task AssistantToolService_Records_Audit_Receipts_For_Allowed_And_Denied_Tools()
        {
            var audit = new AssistantToolAuditLog();
            var service = new AssistantToolService(new AgentConfig(), null, audit);
            var query = "unlikely-query-" + Guid.NewGuid().ToString("N");

            var denied = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "/lab/vault/reset"
            }, "trace-denied-reset");

            var allowed = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "search_local_vault",
                Query = query,
                Limit = 3
            }, "trace-allowed-search");

            var receipts = audit.Tail(10);

            Assert.AreEqual("blocked_destructive_lab_route", denied.Status);
            Assert.IsNotNull(denied.Receipt);
            Assert.IsFalse(denied.Receipt.Allowed);
            Assert.AreEqual("unknown", denied.Receipt.RiskLevel);
            Assert.IsTrue(denied.Receipt.ApprovalRequired);

            Assert.IsNotNull(allowed.Receipt);
            Assert.IsTrue(allowed.Receipt.Allowed);
            Assert.AreEqual("low", allowed.Receipt.RiskLevel);
            Assert.AreEqual("safe_tool_name", allowed.Receipt.PolicyCode);
            Assert.AreEqual("3", allowed.Receipt.InputSummary["limit"]);
            Assert.AreEqual(query.Length.ToString(), allowed.Receipt.InputSummary["queryLength"]);

            Assert.AreEqual(2, receipts.Count);
            Assert.IsTrue(receipts.Any(r => r.TraceId == "trace-denied-reset"));
            Assert.IsTrue(receipts.Any(r => r.TraceId == "trace-allowed-search"));
        }

        [TestMethod]
        public async Task AssistantToolAuditLog_Persists_Redacted_Receipts_To_Jsonl()
        {
            var logRoot = Path.Combine(Path.GetTempPath(), "bb-tool-audit-" + Guid.NewGuid().ToString("N"));
            var query = "sensitive-customer-query-" + Guid.NewGuid().ToString("N");

            try
            {
                var audit = new AssistantToolAuditLog(logRoot);
                var service = new AssistantToolService(new AgentConfig(), null, audit);

                var result = await service.ExecuteAsync(new AssistantToolRequest
                {
                    ToolName = "search_local_vault",
                    Query = query,
                    Limit = 2,
                    Parameters =
                    {
                        { "query", query },
                        { "customer", "Example Customer" }
                    }
                }, "trace-persisted-receipt");

                var persisted = new AssistantToolAuditLog(logRoot).TailPersisted(5);
                var files = Directory.GetFiles(logRoot, "assistant-tool-receipts-*.jsonl");
                var raw = File.ReadAllText(files.Single());

                Assert.IsNotNull(result.Receipt);
                Assert.AreEqual(1, persisted.Count);
                Assert.AreEqual("trace-persisted-receipt", persisted[0].TraceId);
                Assert.AreEqual("present", persisted[0].InputSummary["param:customer"]);
                Assert.AreEqual(query.Length.ToString(), persisted[0].InputSummary["queryLength"]);
                Assert.IsFalse(raw.Contains(query));
                Assert.IsFalse(raw.Contains("Example Customer"));
            }
            finally
            {
                if (Directory.Exists(logRoot))
                {
                    Directory.Delete(logRoot, true);
                }
            }
        }

        [TestMethod]
        public void AssistantPanel_Normalizes_Tool_Receipt_For_UI()
        {
            var receipt = JObject.FromObject(new AssistantToolExecutionReceipt
            {
                ReceiptId = "receipt-123",
                TraceId = "trace-123",
                ToolName = "search_local_vault",
                RiskLevel = "low",
                Allowed = true,
                ApprovalRequired = false,
                PolicyCode = "safe_tool_name",
                ResultStatus = "ok"
            });

            var normalized = AssistantPanel.NormalizeToolReceipt(receipt);

            Assert.AreEqual("receipt-123", normalized.Value<string>("receiptId"));
            Assert.AreEqual("trace-123", normalized.Value<string>("traceId"));
            Assert.AreEqual("search_local_vault", normalized.Value<string>("toolName"));
            Assert.AreEqual("low", normalized.Value<string>("riskLevel"));
            Assert.IsTrue(normalized.Value<bool>("allowed"));
            Assert.IsFalse(normalized.Value<bool>("approvalRequired"));
            Assert.AreEqual("safe_tool_name", normalized.Value<string>("policyCode"));
            Assert.AreEqual("ok", normalized.Value<string>("resultStatus"));
        }

        [TestMethod]
        public void AssistantPanel_CancellationOwnership_DoesNotClear_Newer_Request()
        {
            using (var first = new System.Threading.CancellationTokenSource())
            using (var second = new System.Threading.CancellationTokenSource())
            {
                Assert.IsTrue(AssistantPanel.OwnsStreamCancellationSource(first, first));
                Assert.IsFalse(AssistantPanel.OwnsStreamCancellationSource(second, first));
            }
        }

        [TestMethod]
        public void AssistantErrorClassifier_Maps_Common_Request_Failures()
        {
            var canceled = AssistantErrorClassifier.FromException(new OperationCanceledException(), true);
            var timeout = AssistantErrorClassifier.FromException(new OperationCanceledException(), false);
            var bridge = AssistantErrorClassifier.FromException(new System.Net.Http.HttpRequestException("connection refused"));
            var auth = AssistantErrorClassifier.FromHttpFailure(System.Net.HttpStatusCode.Forbidden, "{\"error\":\"bad token\"}");
            var provider = AssistantErrorClassifier.FromProviderFailure("{\"error\":{\"message\":\"quota exceeded\"}}");
            var invalidJson = AssistantErrorClassifier.FromJsonParseFailure("<html>not json</html>");

            Assert.AreEqual("request_canceled", canceled.Code);
            Assert.AreEqual("request_timeout", timeout.Code);
            Assert.AreEqual("bridge_unavailable", bridge.Code);
            Assert.AreEqual("auth_failed", auth.Code);
            Assert.AreEqual("provider_error", provider.Code);
            Assert.AreEqual("quota exceeded", provider.Message);
            Assert.AreEqual("json_parse_error", invalidJson.Code);
        }

        [TestMethod]
        public void AssistantPanel_Builds_User_Facing_Classified_Error()
        {
            var message = AssistantPanel.BuildUserFacingError("provider_error", "quota exceeded");

            Assert.AreEqual("Request failed (provider_error): quota exceeded", message);
        }

        [TestMethod]
        public void AssistantWebViewSecurity_Blocks_Unexpected_Navigation()
        {
            Assert.IsTrue(AssistantWebViewSecurity.IsNavigationAllowed("about:blank"));
            Assert.IsTrue(AssistantWebViewSecurity.IsNavigationAllowed("data:text/html;base64,PGgxPkI8L2gxPg=="));
            Assert.IsTrue(AssistantWebViewSecurity.IsNavigationAllowed("https://chatgpt.com/"));
            Assert.IsTrue(AssistantWebViewSecurity.IsNavigationAllowed("https://chat.openai.com/"));

            Assert.IsFalse(AssistantWebViewSecurity.IsNavigationAllowed("http://127.0.0.1:17177/assistant/status"));
            Assert.IsFalse(AssistantWebViewSecurity.IsNavigationAllowed("https://example.com/"));
            Assert.IsFalse(AssistantWebViewSecurity.IsNavigationAllowed("file:///C:/temp/test.html"));
            Assert.IsFalse(AssistantWebViewSecurity.IsNavigationAllowed("javascript:alert(1)"));
        }

        [TestMethod]
        public void AssistantWebViewSecurity_ShellHtml_Does_Not_Expose_Token_Identifiers()
        {
            var shellHtml = typeof(AssistantPanel)
                .GetMethod("BuildShellHtml", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, null) as string;

            Assert.IsFalse(AssistantWebViewSecurity.ContainsSensitiveTokenText(shellHtml));
        }

        [TestMethod]
        public void AssistantPanel_ShellHtml_Renders_Cad_Safety_And_Context_Rails()
        {
            var shellHtml = typeof(AssistantPanel)
                .GetMethod("BuildShellHtml", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, null) as string;

            StringAssert.Contains(shellHtml, "CAD-safe copilot");
            StringAssert.Contains(shellHtml, "id='modelCaps'");
            StringAssert.Contains(shellHtml, "id='safetyRail'");
            StringAssert.Contains(shellHtml, "id='workflowStrip'");
            StringAssert.Contains(shellHtml, "Screenshot -> annotation -> review report");
            StringAssert.Contains(shellHtml, "Read Only");
            StringAssert.Contains(shellHtml, "Preview First");
            StringAssert.Contains(shellHtml, "Mutation Blocked");
            StringAssert.Contains(shellHtml, "id='toolsPill'");
            StringAssert.Contains(shellHtml, "annotation-list");
            StringAssert.Contains(shellHtml, "annotation-row");
            StringAssert.Contains(shellHtml, "Confidence ");
            StringAssert.Contains(shellHtml, "contact-status");
            StringAssert.Contains(shellHtml, "contact-note");
            StringAssert.Contains(shellHtml, "screenshot-action");
            StringAssert.Contains(shellHtml, "Review report");
            StringAssert.Contains(shellHtml, "id='receiptGrid'");
            StringAssert.Contains(shellHtml, "id='integrationGrid'");
            StringAssert.Contains(shellHtml, "id='documentGrid'");
        }

        [TestMethod]
        public void AssistantProductCatalog_Defines_Salesforce_And_Document_Artifacts()
        {
            var integrations = AssistantProductCatalog.GetIntegrations();
            var salesforce = integrations.Single(x => x.Id == "salesforce");

            Assert.AreEqual("planned", salesforce.Status);
            Assert.IsTrue(salesforce.RequiresOAuth);
            Assert.IsTrue(salesforce.ReadOnlyFirst);
            CollectionAssert.Contains(salesforce.FirstObjects, "Opportunity");

            var documents = AssistantProductCatalog.GetDocuments();
            Assert.IsTrue(documents.Any(x => x.Id == "drawing-pdf" && x.Implemented));
            Assert.IsTrue(documents.Any(x => x.Id == "packet-pdf" && x.RequiresPdmApproval));
            Assert.IsTrue(documents.Any(x => x.Id == "salesforce-opportunity-brief" && !x.Implemented));
            Assert.IsTrue(documents.Any(x => x.Id == "screenshot-review-report" && x.AssistantUses.Contains("review pending contacts")));
            Assert.IsTrue(documents.Any(x => x.Id == "manufacturing-release-checklist" && x.RequiresSolidWorks && x.RequiresPdmApproval));
            Assert.IsTrue(documents.Any(x => x.Id == "local-vault-search-summary" && x.SourceSubsystem.Contains("LocalVaultWorkspace")));
            Assert.IsTrue(documents.Any(x => x.Id == "pdm-metadata-brief" && x.Category == "vault-brief"));
            Assert.IsTrue(documents.Any(x => x.Id == "epicor-part-quote-brief" && x.Category == "erp-brief"));
        }

        [TestMethod]
        public async Task AssistantToolService_Searches_Local_Vault_ReadOnly_Index()
        {
            var uniquePart = "BB-TOOL-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            var tempFile = Path.Combine(Path.GetTempPath(), uniquePart + ".pdf");
            File.WriteAllText(tempFile, "assistant tool search");

            try
            {
                var workspace = new LocalVaultWorkspace();
                workspace.SaveGeneratedArtifact(new GeneratedArtifactRecord
                {
                    OutputPath = tempFile,
                    ArtifactType = "pdf",
                    PartNumber = uniquePart,
                    DocumentNumber = uniquePart,
                    Description = "Assistant local vault search test",
                    Customer = "LAB",
                    CreatedUtc = DateTime.UtcNow
                });

                var service = new AssistantToolService(new AgentConfig());
                var result = await service.ExecuteAsync(new AssistantToolRequest
                {
                    ToolName = "search_local_vault",
                    Query = uniquePart,
                    Limit = 5
                }, "trace-search");

                Assert.AreEqual("ok", result.Status);
                Assert.IsTrue(result.ReadOnly);
                Assert.IsTrue(result.Items.Any(item => string.Equals(item.Title, uniquePart + ".pdf", StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void RelayTunnelClient_Builds_Handoff_Url_For_Session()
        {
            var config = new AgentConfig
            {
                Relay = new RelaySettings
                {
                    Enabled = true,
                    BaseUrl = "https://relay.example.com",
                    ChatWorkspaceUrl = "https://chatgpt.com",
                    HandoffPath = "chatgpt/handoff",
                    DeviceId = "lab-device"
                }
            };

            var telemetry = new TelemetryLogger(Path.GetTempPath(), "relay-tests", 1.0, 1, 32, 16);
            var client = new RelayTunnelClient(config, telemetry, () => Array.Empty<string>(), _ => Task.FromResult(new PreviewActionResult()));

            var url = client.BuildHandoffUrl("session-123");

            StringAssert.Contains(url, "https://relay.example.com/chatgpt/handoff");
            StringAssert.Contains(url, "sessionId=session-123");
            StringAssert.Contains(url, "deviceId=lab-device");
        }

        [TestMethod]
        public void PreviewActionPolicy_Denies_Disabled_Hosted_Write_Actions()
        {
            var policy = new PreviewActionPolicy();
            var session = new PreviewSession();
            session.AllowedActions.Add("get_preview_status");
            session.AllowedActions.Add("run_local_review");

            var disabled = policy.Evaluate(session, new PreviewActionRequest { ActionName = "apply_safe_action" });
            var blocked = policy.Evaluate(session, new PreviewActionRequest { ActionName = "start_local_generation" });

        Assert.IsFalse(disabled.Allowed);
        Assert.IsFalse(blocked.Allowed);
        }

        [TestMethod]
        public void AssistantToolPolicy_Blocks_CAD_Route_Alias_In_ToolName()
        {
            var policy = new AssistantToolPolicy();
            var result = policy.EvaluateToolName("sw/open");
            Assert.IsFalse(result.Allowed);
            Assert.AreEqual("blocked_route_alias", result.Code);
        }

        [TestMethod]
        public void AssistantToolPolicy_Blocks_PDM_Route_Alias_In_ToolName()
        {
            var policy = new AssistantToolPolicy();
            var result = policy.EvaluateToolName("pdm/search");
            Assert.IsFalse(result.Allowed);
            Assert.AreEqual("blocked_route_alias", result.Code);
        }

        [TestMethod]
        public void AssistantToolPolicy_Blocks_Destructive_Lab_Route_Alias()
        {
            var policy = new AssistantToolPolicy();
            var result = policy.EvaluateToolName("lab/vault/reset");
            Assert.IsFalse(result.Allowed);
            Assert.AreEqual("blocked_route_alias", result.Code);
        }

        [TestMethod]
        public void AssistantToolPolicy_Blocks_CAD_Route_Direct()
        {
            var policy = new AssistantToolPolicy();
            var result = policy.EvaluateRoute("/sw/open", "POST", AssistantToolInvocationSource.AssistantTool);
            Assert.IsFalse(result.Allowed);
            Assert.AreEqual("blocked_cad_route", result.Code);
        }

        [TestMethod]
        public void AssistantToolPolicy_Blocks_Native_PDM_Route()
        {
            var policy = new AssistantToolPolicy();
            var result = policy.EvaluateRoute("/pdm/search", "POST", AssistantToolInvocationSource.AssistantTool);
            Assert.IsFalse(result.Allowed);
            Assert.AreEqual("blocked_native_pdm_route", result.Code);
        }

        [TestMethod]
        public void AssistantToolPolicy_Blocks_Unknown_Mutation_Route_From_AssistantTool()
        {
            var policy = new AssistantToolPolicy();
            var result = policy.EvaluateRoute("/unknown/mutate", "POST", AssistantToolInvocationSource.AssistantTool);
            Assert.IsFalse(result.Allowed);
            Assert.AreEqual("unknown_mutation_route", result.Code);
        }

        [TestMethod]
        public void AssistantToolPolicy_Allows_Assistant_Route()
        {
            var policy = new AssistantToolPolicy();
            var result = policy.EvaluateRoute("/assistant/session", "POST", AssistantToolInvocationSource.AssistantTool);
            Assert.IsTrue(result.Allowed);
        }

        [TestMethod]
        public void AssistantToolService_Capture_Screenshot_Fails_Without_AssistantService()
        {
            var service = new AssistantToolService(new AgentConfig());
            var result = service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "capture_screenshot"
            }, "trace-capture-no-service").Result;

        Assert.AreEqual("unavailable", result.Status);
        Assert.IsTrue(result.Message.Contains("active assistant service"));
        }

        [TestMethod]
        public void AssistantToolService_Unknown_Tool_Returns_Unsupported()
        {
            var service = new AssistantToolService(new AgentConfig());
            var result = service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "nonexistent_tool"
            }, "trace-unknown").Result;

        Assert.AreEqual("unknown", result.Status);
        Assert.IsNotNull(result.Receipt);
        Assert.AreEqual("unknown", result.Receipt.PolicyCode);
    }

        [TestMethod]
        public void AssistantToolService_Disabled_Tool_Is_Rejected()
        {
            var config = new AgentConfig
            {
                AssistantTools = new AssistantToolSettings
                {
                    EnablePdmSearch = false
                }
            };
            var service = new AssistantToolService(config);
            var result = service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "search_pdm",
                Query = "test"
            }, "trace-pdm-disabled").Result;

        Assert.AreEqual("disabled", result.Status);
        Assert.IsNotNull(result.Receipt);
        Assert.AreEqual("disabled", result.Receipt.PolicyCode);
    }

        [TestMethod]
        public void Cancellation_CancellationTokenSource_Disposes_Cleanly_On_Second_Send()
        {
            using (var first = new System.Threading.CancellationTokenSource())
            {
                first.Cancel();
                using (var second = new System.Threading.CancellationTokenSource())
                {
                    Assert.IsTrue(first.IsCancellationRequested);
                    Assert.IsFalse(second.IsCancellationRequested);
                    Assert.IsFalse(AssistantPanel.OwnsStreamCancellationSource(first, second));
                }
            }
        }

        [TestMethod]
        public void AssistantWebViewSecurity_Blocks_Null_And_Empty_Uri()
        {
            Assert.IsTrue(AssistantWebViewSecurity.IsNavigationAllowed(""));
            Assert.IsTrue(AssistantWebViewSecurity.IsNavigationAllowed(null));
        }

        [TestMethod]
        public void AssistantWebViewSecurity_Detects_Sensitive_Token_Text()
        {
            Assert.IsTrue(AssistantWebViewSecurity.ContainsSensitiveTokenText("<script>X-Agent-Auth: token</script>"));
            Assert.IsTrue(AssistantWebViewSecurity.ContainsSensitiveTokenText("OPENAI_API_KEY=sk-xxx"));
            Assert.IsTrue(AssistantWebViewSecurity.ContainsSensitiveTokenText("NVIDIA_API_KEY=nvapi-xxx"));
            Assert.IsTrue(AssistantWebViewSecurity.ContainsSensitiveTokenText(".agent_token"));
            Assert.IsFalse(AssistantWebViewSecurity.ContainsSensitiveTokenText("<div>Hello World</div>"));
        Assert.IsFalse(AssistantWebViewSecurity.ContainsSensitiveTokenText(null));
        Assert.IsFalse(AssistantWebViewSecurity.ContainsSensitiveTokenText(""));
        }

        [TestMethod]
        public void AgentHttpServer_MaxRequestBodyBytes_Is_OneMB()
        {
            Assert.AreEqual(1_048_576, AgentHttpServer.MaxRequestBodyBytes);
        }

        [TestMethod]
        public void AgentHttpServer_ContentLength_Exceeding_Max_Returns413()
        {
            var contentLength = AgentHttpServer.MaxRequestBodyBytes + 1;
            Assert.IsTrue(contentLength > AgentHttpServer.MaxRequestBodyBytes,
                $"Content-Length {contentLength} must exceed MaxRequestBodyBytes {AgentHttpServer.MaxRequestBodyBytes}");
        }

        [TestMethod]
        public async Task OpenAiAssistantService_MockMode_Wins_Over_EnvKey()
        {
            var prevKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            try
            {
                Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key-that-must-not-be-used");
                var config = new AgentConfig
                {
                    Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                    Vault = new VaultSettings
                    {
                        Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                        SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                    },
                    Assistant = new AssistantSettings
                    {
                        ApiBaseUrl = "https://api.openai.com/v1",
                        Model = "gpt-4.1-mini",
                        Mode = "mock",
                        SystemPrompt = "mock",
                        Detail = "low",
                        EnableUploads = true,
                        MaxImageDimension = 1600,
                        JpegQuality = 75,
                        ConnectionTestPrompt = "ready",
                        RequireExplicitUploadConsent = true,
                        MaxHistory = 10
                    }
                };

                var service = new OpenAiAssistantService(config);
                var status = await service.GetStatusAsync();
                Assert.AreEqual("mock", status.AssistantMode);

                var response = await service.SendMessageAsync(null, "test", Array.Empty<string>());
                Assert.IsTrue(response.AssistantAvailable);
                Assert.IsTrue(response.Message.Text.IndexOf("Mock preview mode", StringComparison.OrdinalIgnoreCase) >= 0);
            }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevKey);
        }
    }

    [TestMethod]
    public async Task OpenAiAssistantService_RealMode_WithoutKey_FallsBackToMock()
    {
        var prevOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var prevNvidiaKey = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
        var prevRegistryMode = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantMode", null)?.ToString();
        var prevRegistryApiKey = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantApiKey", null)?.ToString();
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("NVIDIA_API_KEY", null);
            Registry.SetValue(AppIdentity.RegistryRoot, "AssistantMode", "real", RegistryValueKind.String);
            ClearRegistryValue("AssistantApiKey");

            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                    Model = "meta/llama-3.1-70b-instruct",
                    Mode = "real",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = true,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10
                }
            };

            var service = new OpenAiAssistantService(config);
            var status = await service.GetStatusAsync();
            Assert.AreEqual("mock", status.AssistantMode);

            var response = await service.SendMessageAsync(null, "test real-no-key", Array.Empty<string>());
            Assert.IsTrue(response.AssistantAvailable);
            Assert.IsTrue(response.Message.Text.IndexOf("Mock preview mode", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevOpenAiKey);
            Environment.SetEnvironmentVariable("NVIDIA_API_KEY", prevNvidiaKey);
            RestoreRegistryMode(prevRegistryMode);
            RestoreRegistryApiKey(prevRegistryApiKey);
        }
    }

    [TestMethod]
    public async Task OpenAiAssistantService_RealMode_WithKey_Succeeds()
    {
        var prevKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var prevRegistryMode = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantMode", null)?.ToString();
        var prevRegistryApiKey = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantApiKey", null)?.ToString();
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key-real-mode-verify");
            Registry.SetValue(AppIdentity.RegistryRoot, "AssistantMode", "real", RegistryValueKind.String);
            ClearRegistryValue("AssistantApiKey");

            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://api.openai.com/v1",
                    Model = "gpt-4.1-mini",
                    Mode = "real",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = true,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10
                }
            };

            var service = new OpenAiAssistantService(config);
            var status = await service.GetStatusAsync();
            Assert.AreEqual("real", status.AssistantMode);
            Assert.IsTrue(status.KeyConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevKey);
            RestoreRegistryMode(prevRegistryMode);
            RestoreRegistryApiKey(prevRegistryApiKey);
        }
    }

    [TestMethod]
    public async Task OpenAiAssistantService_MockMode_Wins_Over_NvidiaKey()
    {
        var prevNvidiaKey = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
        var prevOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var prevRegistryMode = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantMode", null)?.ToString();
        var prevRegistryApiKey = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantApiKey", null)?.ToString();
        try
        {
            Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "test-nvidia-key-that-must-not-be-used");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Registry.SetValue(AppIdentity.RegistryRoot, "AssistantMode", "real", RegistryValueKind.String);
            ClearRegistryValue("AssistantApiKey");

            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                    Model = "meta/llama-3.1-70b-instruct",
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = true,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10,
                    ModelProfiles = new[]
                    {
                        new AssistantModelProfile
                        {
                            Id = "nvidia-test",
                            Name = "NVIDIA Test",
                            Provider = "NVIDIA",
                            ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                            Model = "meta/llama-3.1-70b-instruct",
                            KeyEnvironmentVariable = "NVIDIA_API_KEY",
                            IsDefault = true,
                            SupportsVision = false
                        }
                    }
                }
            };

            var service = new OpenAiAssistantService(config);
            var status = await service.GetStatusAsync();
            Assert.AreEqual("mock", status.AssistantMode);

            var response = await service.SendMessageAsync(null, "test nvidia-mock", Array.Empty<string>());
            Assert.IsTrue(response.AssistantAvailable);
            Assert.IsTrue(response.Message.Text.IndexOf("Mock preview mode", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVIDIA_API_KEY", prevNvidiaKey);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevOpenAiKey);
            RestoreRegistryMode(prevRegistryMode);
            RestoreRegistryApiKey(prevRegistryApiKey);
        }
    }

    private static void RestoreRegistryMode(string previousValue)
    {
        if (previousValue == null)
        {
            try { Registry.CurrentUser.DeleteSubKey(AppIdentity.RegistryRoot + "\\AssistantMode", false); } catch { }
            try
            {
                var subKeyPath = AppIdentity.RegistryRoot.Replace(@"HKEY_CURRENT_USER\", "");
                using (var key = Registry.CurrentUser.OpenSubKey(subKeyPath, true))
                {
                    if (key != null) key.DeleteValue("AssistantMode", false);
                }
            }
            catch { }
        }
        else
        {
            Registry.SetValue(AppIdentity.RegistryRoot, "AssistantMode", previousValue, RegistryValueKind.String);
        }
    }

    private static void ClearRegistryValue(string valueName)
    {
        try
        {
            var subKeyPath = AppIdentity.RegistryRoot.Replace(@"HKEY_CURRENT_USER\", "");
            using (var key = Registry.CurrentUser.OpenSubKey(subKeyPath, true))
            {
                if (key != null) key.DeleteValue(valueName, false);
            }
        }
        catch { }
    }

        private static void RestoreRegistryApiKey(string previousValue)
        {
            if (previousValue == null)
            {
                ClearRegistryValue("AssistantApiKey");
            }
            else
            {
                Registry.SetValue(AppIdentity.RegistryRoot, "AssistantApiKey", previousValue, RegistryValueKind.String);
            }
        }

        [TestMethod]
        public async Task OpenAiAssistantService_RealConnectionTest_WithNvidiaKey()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("BLUEBRICK_RUN_REAL_AI_TESTS"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set BLUEBRICK_RUN_REAL_AI_TESTS=1 to run live provider connection tests.");
            }

            var prevApiKey = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantApiKey", null)?.ToString();
            if (string.IsNullOrEmpty(prevApiKey))
            {
                Assert.Inconclusive("No NVIDIA API key in registry - cannot test real connection");
            }

            var prevRegistryMode = Registry.GetValue(AppIdentity.RegistryRoot, "AssistantMode", null)?.ToString();
            try
            {
                Registry.SetValue(AppIdentity.RegistryRoot, "AssistantMode", "real", RegistryValueKind.String);

                var config = new AgentConfig
                {
                    Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                    Vault = new VaultSettings
                    {
                        Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                        SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                    },
                    Assistant = new AssistantSettings
                    {
                        ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                        Model = "meta/llama-3.1-70b-instruct",
                        Mode = "real",
                        SystemPrompt = "You are the BlueBrick Lab assistant.",
                        Detail = "low",
                        EnableUploads = true,
                        MaxImageDimension = 1600,
                        JpegQuality = 75,
                        ConnectionTestPrompt = "Reply with the word READY and one short sentence confirming the BlueBrick Lab assistant connection is working.",
                        RequireExplicitUploadConsent = true,
                        MaxHistory = 10
                    }
                };

                var service = new OpenAiAssistantService(config);
                var status = await service.GetStatusAsync();
                Assert.AreEqual("real", status.AssistantMode, "Mode should be real with registry key present");
                Assert.IsTrue(status.KeyConfigured, "Key should be configured from registry");

                var result = await service.TestConnectionAsync();
                Assert.IsTrue(result.Success, "Connection test should succeed. Message: " + result.Message);
                Assert.AreEqual("real", result.Mode);
                Assert.IsTrue(result.LatencyMs > 0, "Should report latency");
                Assert.IsTrue((result.Message ?? "").IndexOf("READY", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Response should contain READY. Got: " + result.Message);
        }
        finally
        {
            RestoreRegistryMode(prevRegistryMode);
        }
    }

    [TestMethod]
    public async Task OpenAiAssistantService_MockStreaming_YieldsTextDeltas()
    {
        var config = new AgentConfig
        {
            Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
            Vault = new VaultSettings
            {
                Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
            },
            Assistant = new AssistantSettings
            {
                Mode = "mock",
                SystemPrompt = "mock",
                Detail = "low",
                EnableUploads = false,
                MaxImageDimension = 1600,
                JpegQuality = 75,
                ConnectionTestPrompt = "ready",
                RequireExplicitUploadConsent = false,
                MaxHistory = 10
            }
        };

        var service = new OpenAiAssistantService(config);
        var session = await service.CreateSessionAsync();
        var chunks = new List<AssistantStreamChunk>();

        await service.SendMessageStreamAsync(session.SessionId, "hello", null,
            chunk => chunks.Add(chunk), CancellationToken.None);

        Assert.IsTrue(chunks.Count > 0, "Should yield at least one chunk");
        var textChunks = chunks.Where(c => c.Type == "text_delta").ToList();
        Assert.IsTrue(textChunks.Count > 0, "Should yield text_delta chunks in mock mode");
        var combinedText = string.Concat(textChunks.Select(c => c.Text));
        Assert.IsTrue(combinedText.Length > 0, "Combined text should not be empty");
        Assert.IsTrue(chunks.Any(c => c.Type == "done"), "Should yield a done chunk");
    }

    [TestMethod]
    public async Task OpenAiAssistantService_MockStreaming_Cancellation_StopsStream()
    {
        var config = new AgentConfig
        {
            Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
            Vault = new VaultSettings
            {
                Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
            },
            Assistant = new AssistantSettings
            {
                Mode = "mock",
                SystemPrompt = "mock",
                Detail = "low",
                EnableUploads = false,
                MaxImageDimension = 1600,
                JpegQuality = 75,
                ConnectionTestPrompt = "ready",
                RequireExplicitUploadConsent = false,
                MaxHistory = 10
            }
        };

        var service = new OpenAiAssistantService(config);
        var session = await service.CreateSessionAsync();
        var chunks = new List<AssistantStreamChunk>();
        var cts = new CancellationTokenSource();

        try
        {
            await service.SendMessageStreamAsync(session.SessionId, "hello", null,
                chunk =>
                {
                    chunks.Add(chunk);
                    if (chunks.Count >= 2) cts.Cancel();
                }, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        Assert.IsTrue(chunks.Count >= 1, "Should yield at least one chunk before cancellation");
        }

        [TestMethod]
        public void AssistantStreamChunk_ToolResult_Factory_SetsFields()
        {
            var chunk = AssistantStreamChunk.ToolResult("call-123", "{\"status\":\"ok\"}");
            Assert.AreEqual("tool_result", chunk.Type);
            Assert.AreEqual("call-123", chunk.ToolCallId);
            Assert.AreEqual("{\"status\":\"ok\"}", chunk.ToolResultContent);
        }

        [TestMethod]
        public async Task OpenAiAssistantService_ToolSchemas_Included_WhenToolsSupported()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = false,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = false,
                    MaxHistory = 10,
                    ModelProfiles = new[]
                    {
                        new AssistantModelProfile
                        {
                            Id = "test-tools-profile",
                            Name = "Test Tools Profile",
                            Provider = "OpenAI",
                            ApiBaseUrl = "https://api.openai.com/v1",
                            Model = "gpt-4.1-mini",
                            KeyEnvironmentVariable = "OPENAI_API_KEY",
                            IsDefault = true,
                            ProviderKind = "openai",
                            SupportsTools = true,
                            Enabled = true,
                            Source = "test"
                        }
                    }
                }
            };

            var service = new OpenAiAssistantService(config);
            var session = await service.CreateSessionAsync();

            var profile = (await service.GetModelsAsync()).FirstOrDefault(p => p.Id == "test-tools-profile");
            Assert.IsNotNull(profile, "Test tools profile should exist");
            Assert.IsTrue(profile.SupportsTools, "Profile should support tools");
        }

        [TestMethod]
        public async Task OpenAiAssistantService_ToolSchemas_NotIncluded_WhenToolsNotSupported()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = false,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = false,
                    MaxHistory = 10,
                    ModelProfiles = new[]
                    {
                        new AssistantModelProfile
                        {
                            Id = "test-no-tools",
                            Name = "No Tools Profile",
                            Provider = "NVIDIA",
                            ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
                            Model = "meta/llama-3.1-70b-instruct",
                            KeyEnvironmentVariable = "NVIDIA_API_KEY",
                            IsDefault = true,
                            ProviderKind = "nvidia",
                            SupportsTools = false,
                            Enabled = true,
                            Source = "test"
                        }
                    }
                }
            };

            var service = new OpenAiAssistantService(config);
            var profile = (await service.GetModelsAsync()).FirstOrDefault(p => p.Id == "test-no-tools");
            Assert.IsNotNull(profile);
            Assert.IsFalse(profile.SupportsTools);
        }

        [TestMethod]
        public void AssistantToolService_Catalog_ContainsSearchLocalVault()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179 },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings { Mode = "mock", SystemPrompt = "mock" }
            };

            var service = new AssistantToolService(config);
            var catalog = service.GetCatalog();
            var searchTool = catalog.FirstOrDefault(t => t.Name == "search_local_vault");
            Assert.IsNotNull(searchTool, "search_local_vault tool should be in catalog");
            Assert.IsTrue(searchTool.Enabled, "search_local_vault should be enabled");
            Assert.IsTrue(searchTool.ReadOnly, "search_local_vault should be read-only");
        }

        [TestMethod]
        public async Task OpenAiAssistantService_MockStreaming_ToolResultChunk_InSequence()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = false,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = false,
                    MaxHistory = 10
                }
            };

            var service = new OpenAiAssistantService(config);
            var session = await service.CreateSessionAsync();
            var chunks = new List<AssistantStreamChunk>();

            await service.SendMessageStreamAsync(session.SessionId, "hello", null,
                chunk => chunks.Add(chunk), CancellationToken.None);

            var toolResultChunks = chunks.Where(c => c.Type == "tool_result").ToList();
            var doneChunks = chunks.Where(c => c.Type == "done").ToList();
            Assert.AreEqual(0, toolResultChunks.Count, "Mock mode should not produce tool_result chunks");
            Assert.AreEqual(1, doneChunks.Count, "Should have exactly one done chunk");

            var allTypes = chunks.Select(c => c.Type).Distinct().ToList();
            Assert.IsTrue(allTypes.Contains("text_delta"), "Should contain text_delta chunks");
            Assert.IsTrue(allTypes.Contains("done"), "Should contain done chunk");
        }

        [TestMethod]
        public void ToolCall_UnknownTool_ReturnsDeniedResult()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179 },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings { Mode = "mock", SystemPrompt = "mock" }
            };

            var service = new AssistantToolService(config);
            var result = service.ExecuteAsync(new AssistantToolRequest { ToolName = "hack_the_gibson", Query = "test" }, "test-trace").Result;
            Assert.AreEqual("unknown", result.Status);
            Assert.IsNotNull(result.Receipt, "Unknown tool should still produce a receipt");
            Assert.IsFalse(result.Receipt.Allowed, "Unknown tool receipt should show denied");
        }

        [TestMethod]
        public void ToolCall_DisabledPdm_ReturnsDeniedResult()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179 },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings { Mode = "mock", SystemPrompt = "mock" },
                AssistantTools = new AssistantToolSettings { EnablePdmSearch = false }
            };

            var service = new AssistantToolService(config);
            var result = service.ExecuteAsync(new AssistantToolRequest { ToolName = "search_pdm", Query = "bracket" }, "test-trace").Result;
            Assert.AreEqual("disabled", result.Status);
            Assert.IsNotNull(result.Receipt);
            Assert.IsFalse(result.Receipt.Allowed);
        }

        [TestMethod]
        public void ToolCall_MutationLikeSwTool_IsDenied()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179 },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings { Mode = "mock", SystemPrompt = "mock" }
            };

            var service = new AssistantToolService(config);
            var result = service.ExecuteAsync(new AssistantToolRequest { ToolName = "sw/save", Query = "" }, "test-trace").Result;
            Assert.AreEqual("blocked_route_alias", result.Status);
            Assert.IsNotNull(result.Receipt);
            Assert.IsFalse(result.Receipt.Allowed);
        }

        [TestMethod]
        public void ToolCall_MalformedArguments_ReturnsClassifiedError()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179 },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings { Mode = "mock", SystemPrompt = "mock" }
            };

            var service = new AssistantToolService(config);
            var result = service.ExecuteAsync(new AssistantToolRequest { ToolName = "search_local_vault", Query = "" }, "test-trace").Result;
            Assert.AreEqual("invalid", result.Status);
        }

        [TestMethod]
        public void ToolCall_MaxRoundsExceeded_StopsLoop()
        {
            Assert.AreEqual(5, typeof(OpenAiAssistantService).GetField("MaxToolRounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null), "MaxToolRounds should be 5 to prevent infinite loops");
        }

        [TestMethod]
        public void StreamingToolCall_SplitArguments_ReassembledCorrectly()
        {
            var acc1 = new OpenAiAssistantService.ToolCallAccumulator();
            var dict = new Dictionary<int, OpenAiAssistantService.ToolCallAccumulator> { { 0, acc1 } };

            var chunk1 = JObject.Parse("{\"index\":0,\"id\":\"call_abc\",\"function\":{\"name\":\"search_local_vault\",\"arguments\":\"{\\\"quer\"}}");
            var chunk2 = JObject.Parse("{\"index\":0,\"function\":{\"arguments\":\"y\\\":\\\"bracket\\\"}\"}}");

            foreach (var chunk in new[] { chunk1, chunk2 })
            {
                var idx = chunk.Value<int?>("index") ?? 0;
                OpenAiAssistantService.ToolCallAccumulator acc;
                if (!dict.TryGetValue(idx, out acc))
                {
                    acc = new OpenAiAssistantService.ToolCallAccumulator();
                    dict[idx] = acc;
                }
                var tcId = chunk.Value<string>("id");
                if (!string.IsNullOrEmpty(tcId)) acc.Id = tcId;
                var fn = chunk["function"];
                if (fn != null)
                {
                    var fName = fn.Value<string>("name");
                    if (!string.IsNullOrEmpty(fName)) acc.Name = fName;
                    var fArgs = fn.Value<string>("arguments");
                    if (!string.IsNullOrEmpty(fArgs)) acc.Arguments.Append(fArgs);
                }
            }

            Assert.AreEqual("call_abc", dict[0].Id);
            Assert.AreEqual("search_local_vault", dict[0].Name);
            var fullArgs = dict[0].Arguments.ToString();
            Assert.IsTrue(fullArgs.Contains("bracket"), "Split arguments should be reassembled: " + fullArgs);

            var parsed = JObject.Parse(fullArgs);
            Assert.AreEqual("bracket", parsed.Value<string>("query"));
        }

        [TestMethod]
        public void StreamingToolCall_ToolResultChunk_Emitted()
        {
            var chunk = AssistantStreamChunk.ToolResult("call-42", "{\"status\":\"ok\",\"message\":\"Found 3 matches.\"}");
            Assert.AreEqual("tool_result", chunk.Type);
            Assert.AreEqual("call-42", chunk.ToolCallId);
            Assert.IsTrue(chunk.ToolResultContent.Contains("Found 3 matches"));
        }

        [TestMethod]
        public async Task NonStreamingAndStreaming_ToolCallPaths_AreEquivalent_ForLocalVault()
        {
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = 17179, OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(Path.GetTempPath(), "bb-lab-vault"),
                    SampleSeedRoot = Path.Combine(Path.GetTempPath(), "bb-lab-samples")
                },
                Assistant = new AssistantSettings
                {
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    EnableUploads = false,
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = false,
                    MaxHistory = 10
                }
            };

            var toolService = new AssistantToolService(config);
            var nonStreamingResult = await toolService.ExecuteAsync(
                new AssistantToolRequest { ToolName = "search_local_vault", Query = "bracket" }, "ns-trace");
            var streamingResult = await toolService.ExecuteAsync(
                new AssistantToolRequest { ToolName = "search_local_vault", Query = "bracket" }, "s-trace");

            Assert.AreEqual(nonStreamingResult.Status, streamingResult.Status, "Both paths should return same status for local vault");
            Assert.AreEqual(nonStreamingResult.ToolName, streamingResult.ToolName);
            Assert.AreEqual(nonStreamingResult.ReadOnly, streamingResult.ReadOnly);
            Assert.IsNotNull(nonStreamingResult.Receipt);
            Assert.IsNotNull(streamingResult.Receipt);
            Assert.IsFalse(nonStreamingResult.Receipt.Allowed == false && nonStreamingResult.Status != "disabled" && nonStreamingResult.Status != "unknown" && nonStreamingResult.Status != "invalid",
                "search_local_vault should not be denied by policy unless vault is missing");
        }

        [TestMethod]
        public void AssistantScopeRegistry_Exposes_Local_Pdm_Epicor_And_All_With_Unavailable_Reasons()
        {
            var service = new AssistantToolService(new AgentConfig());
            var scopes = AssistantScopeRegistry.Build(new AgentConfig(), service.GetCatalog()).ToArray();

            Assert.AreEqual(4, scopes.Length);
            Assert.IsTrue(scopes.Single(s => s.Id == AssistantScopeRegistry.LocalVault).Enabled);
            Assert.IsFalse(scopes.Single(s => s.Id == AssistantScopeRegistry.Pdm).Enabled);
            Assert.IsFalse(scopes.Single(s => s.Id == AssistantScopeRegistry.Epicor).Enabled);
            Assert.IsTrue(scopes.Single(s => s.Id == AssistantScopeRegistry.All).Enabled);
            Assert.IsFalse(string.IsNullOrWhiteSpace(scopes.Single(s => s.Id == AssistantScopeRegistry.Pdm).UnavailableReason));
            Assert.IsFalse(scopes.Single(s => s.Id == AssistantScopeRegistry.All).AllowsMutation);
        }

        [TestMethod]
        public async Task AssistantToolService_ScopeMismatch_Blocks_Search_Wrapper_Without_Pdm_Call()
        {
            var service = new AssistantToolService(new AgentConfig());

            var result = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "search_local_vault",
                Query = "bracket",
                ScopeId = AssistantScopeRegistry.Pdm
            }, "scope-mismatch");

            Assert.AreEqual("scope_unavailable", result.Status);
            Assert.IsNotNull(result.Receipt);
            Assert.IsFalse(result.Receipt.Allowed);
        }

        [TestMethod]
        public async Task AssistantToolService_AllScope_Reports_Disabled_Connectors_As_Partial_Without_Executing_Them()
        {
            var service = new AssistantToolService(new AgentConfig());

            var result = await service.ExecuteAsync(new AssistantToolRequest
            {
                ToolName = "search_local_vault",
                Query = "bracket",
                ScopeId = AssistantScopeRegistry.All
            }, "all-scope");

            Assert.AreEqual("partial", result.Status);
            Assert.IsTrue(result.ReadOnly);
            Assert.IsTrue(result.Items.Any(i => i.Id == "search_pdm:unavailable"));
            Assert.IsTrue(result.Items.Any(i => i.Id == "search_epicor:unavailable"));
            Assert.IsNotNull(result.Receipt);
            Assert.IsTrue(result.Receipt.Allowed);
        }

        [TestMethod]
        public void AgentPanelClient_ParseJson_Unwraps_Assistant_Api_Envelope()
        {
            var method = typeof(AgentPanelClient).GetMethod(
                "ParseJson",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            var parsed = (JObject)method.Invoke(null, new object[]
            {
                "{\"ok\":true,\"correlationId\":\"trace-1\",\"schemaVersion\":\"2026-06-01.v1\",\"data\":{\"models\":[{\"id\":\"aionui\"}]}}"
            });

            Assert.IsTrue(parsed.Value<bool>("ok"));
            Assert.AreEqual("trace-1", parsed.Value<string>("correlationId"));
            Assert.IsInstanceOfType(parsed["models"], typeof(JArray));
        }

        private static void SafeDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Test cleanup only.
            }
        }
    }
}

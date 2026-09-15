using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class RecoveryContractTests
    {
        private string _root;
        [TestInitialize] public void Init() { _root = Path.Combine(Path.GetTempPath(), "BlueBrick-RecoveryTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(_root, "config")); }
        [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        private AgentConfig Load(string json)
        {
            File.WriteAllText(AppIdentity.ConfigPath(_root), json);
            return AgentConfig.LoadFrom(_root);
        }

        [TestMethod]
        public void EveryNestedSectionBindsSyntheticValuesAndRoundTrips()
        {
            var expected = JObject.Parse(@"{
              'ConfigSchemaVersion':'1',
              'Pdm':{'VaultRoot':'pdm-root','VaultName':'sentinel-vault','AllowAssistantReadOnlySearch':true,'EngineeringDbRoot':'engineering-root','ProjectFolders':['a','b']},
              'Templates':{'Root':'templates','Defaults':{'Assembly':'assembly','Drawing':'drawing','SheetFormat':'sheet','Part':'part'},'MaterialFolders':['material']},
              'Agent':{'BridgePort':23456,'OverlayColor':'#112233'},
              'UI':{'Fonts':{'SpaceGroteskPath':'space','IbmPlexSansPath':'plex','FontsPath':'fonts'}},
              'Memory':{'LocalPath':'local','PdmSyncPath':'sync'},
              'Scripts':{'ManifestPath':'manifest','QaRoot':'qa','ReportRoot':'reports'},
              'Baselines':{'Root':'baseline'},
              'Vault':{'Root':'vault','SourceRoot':'source','GeneratedRoot':'generated','ThumbsRoot':'thumbs','MetadataRoot':'metadata','LogRoot':'logs','SampleSeedRoot':'seed'},
              'Assistant':{'ApiBaseUrl':'https://fixture.invalid/v1','Model':'fixture-model','Mode':'real','SystemPrompt':'synthetic','Detail':'high','EnableUploads':false,'MaxImageDimension':1234,'JpegQuality':67,'ConnectionTestPrompt':'sentinel-test','RequireExplicitUploadConsent':true,'MaxHistory':7,'MaxTotalAttachmentBytes':123456,'ModelProfiles':[{'Id':'disabled','Model':'fixture','ApiBaseUrl':'https://fixture.invalid/v1','Enabled':false}],'UseReactWebView':true,'EnableReactDevServer':false,'ReactDevServerUrl':'http://127.0.0.1:23456'},
              'AssistantTools':{'EnablePdmSearch':false,'EnableEpicorSearch':false,'PdmMaxResults':9,'EpicorMaxResults':8,'EpicorConnectionStringEnvironmentVariable':'SYNTHETIC_CONNECTION_REFERENCE'},
              'Relay':{'Enabled':true,'BaseUrl':'https://relay.invalid','ChatWorkspaceUrl':'https://chat.invalid','DeviceId':'fixture-device','DeviceName':'fixture-name','RegistrationToken':'synthetic-nonsecret','HandoffPath':'fixture-handoff','HeartbeatIntervalSeconds':19}
            }");
            var config = Load(expected.ToString());
            var actual = JObject.Parse(JsonConvert.SerializeObject(config));
            AssertSubset(expected, actual);
            AssertSubset(expected, JObject.Parse(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<AgentConfig>(actual.ToString()))));
        }

        private static void AssertSubset(JToken expected, JToken actual)
        {
            if (expected is JObject obj)
                foreach (var property in obj.Properties()) AssertSubset(property.Value, actual?[property.Name]);
            else if (expected is JArray array)
            {
                Assert.AreEqual(array.Count, (actual as JArray)?.Count, expected.Path);
                for (var i = 0; i < array.Count; i++) AssertSubset(array[i], actual[i]);
            }
            else Assert.IsTrue(JToken.DeepEquals(expected, actual), "Binding mismatch: " + expected.Path);
        }

        [TestMethod] public void InvalidNestedValueFailsAndEmptyObjectsGetSafeDefaults()
        {
            Assert.ThrowsException<AgentConfigurationException>(() => Load("{\"Pdm\":{\"AllowAssistantReadOnlySearch\":\"not-a-bool\"}}"));
            var c = Load("{\"UI\":{},\"Templates\":{},\"Assistant\":{\"ModelProfiles\":[],\"EnableUploads\":false}}");
            Assert.IsNotNull(c.UI.Fonts); Assert.IsNotNull(c.Templates.Defaults);
            Assert.AreEqual(0, c.Assistant.ModelProfiles.Length); Assert.IsFalse(c.Assistant.EnableUploads);
        }

        private OpenAiAssistantService Service(AgentConfig c) => new OpenAiAssistantService(c, new AssistantToolService(c, null), new AssistantSessionStore(Path.Combine(_root, "sessions")));

        [TestMethod] public async Task DisabledAndUnknownModelsNeverBecomeSuccessfulSelections()
        {
            var c = Load("{\"Assistant\":{\"ModelProfiles\":[{\"Id\":\"disabled\",\"Model\":\"fixture\",\"ApiBaseUrl\":\"https://fixture.invalid\",\"Enabled\":false}]}}");
            var service = Service(c);
            Assert.IsFalse((await service.GetModelsAsync())[0].Enabled);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.SetModelAsync("disabled"));
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.SetModelAsync("unknown"));
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.SetModelAsync(""));
        }

        [TestMethod] public async Task EmptyCatalogReturnsUnavailableStatusAndTerminalSendResults()
        {
            var c = Load("{\"Assistant\":{\"ModelProfiles\":[]}}"); var service = Service(c);
            Assert.AreEqual(0, (await service.GetModelsAsync()).Count);
            var status = await service.GetStatusAsync();
            Assert.IsFalse(status.Configured); Assert.AreEqual("unavailable", status.AssistantMode);
            Assert.AreEqual(AppIdentity.ConfigPath(_root), status.ConfigPath);
            var response = await service.SendMessageAsync(null, "synthetic", new List<string>());
            Assert.AreEqual("model_unavailable", response.ErrorCode); Assert.IsFalse(response.AssistantAvailable);
            var chunks = new List<AssistantStreamChunk>();
            await service.SendMessageStreamAsync(null, "synthetic", new List<string>(), chunks.Add, CancellationToken.None);
            StringAssert.Contains(JsonConvert.SerializeObject(chunks), "model_unavailable");
        }

        [TestMethod] public void MissingProviderKeyBindingNeverFallsBackToOpenAiCredential()
        {
            var service = Service(Load("{}"));
            var method = typeof(OpenAiAssistantService).GetMethod("ResolveApiKeyInfo", BindingFlags.NonPublic | BindingFlags.Instance);
            var info = method.Invoke(service, new object[] { new AssistantModelProfile { ApiBaseUrl = "https://fixture.invalid" } });
            var source = info.GetType().GetProperty("KeySource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(info);
            Assert.AreEqual("missing_explicit_key_binding", source);
        }

        private AssistantScreenshotArtifact CaptureFixture()
        {
            var id = Guid.NewGuid().ToString("N"); var path = Path.Combine(_root, "capture_" + id + ".png");
            using (var bitmap = new Bitmap(2, 2)) bitmap.Save(path, ImageFormat.Png);
            var a = new AssistantScreenshotArtifact { ArtifactId = id, ScreenshotId = id, Path = path, Width = 2, Height = 2, CapturedUtc = DateTime.UtcNow };
            AssistantScreenshotArtifactStore.CompleteArtifact(a); return a;
        }

        [TestMethod] public void ReviewRequiresSeparateUploadConsentAndRejectionRevokesIt()
        {
            var a = CaptureFixture(); var id = a.ScreenshotId;
            Assert.ThrowsException<InvalidOperationException>(() => AssistantScreenshotArtifactStore.EnsureAttachmentTransmissionAllowed(a.Path, _root));
            AssistantScreenshotArtifactStore.Review(id, "screenshot", id, "approved", _root);
            Assert.ThrowsException<InvalidOperationException>(() => AssistantScreenshotArtifactStore.EnsureAttachmentTransmissionAllowed(a.Path, _root));
            var approved = AssistantScreenshotArtifactStore.Review(id, "screenshot-upload", id, "approved", _root);
            AssistantScreenshotArtifactStore.EnsureAttachmentTransmissionAllowed(a.Path, _root);
            Assert.AreEqual("approved", approved.Receipt.ReviewStatus);
            AssistantScreenshotArtifactStore.Review(id, "screenshot", id, "rejected", _root);
            // Simulate a stale annotation save after rejection; it must not restore consent.
            AssistantScreenshotArtifactStore.CompleteArtifact(approved);
            Assert.AreEqual("rejected", JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(File.ReadAllText(a.MetadataPath)).Receipt.ReviewStatus);
            Assert.ThrowsException<InvalidOperationException>(() => AssistantScreenshotArtifactStore.EnsureAttachmentTransmissionAllowed(a.Path, _root));
        }

        [TestMethod] public void ChangedImageCannotUseOldConsent()
        {
            var a = CaptureFixture(); var id = a.ScreenshotId;
            AssistantScreenshotArtifactStore.Review(id, "screenshot", id, "approved", _root);
            AssistantScreenshotArtifactStore.Review(id, "screenshot-upload", id, "approved", _root);
            File.AppendAllText(a.Path, "changed");
            Assert.ThrowsException<InvalidOperationException>(() => AssistantScreenshotArtifactStore.EnsureAttachmentTransmissionAllowed(a.Path, _root));
        }

        [TestMethod] public void MalformedMissingAndFailedPersistenceNeverAcknowledgeApproval()
        {
            var a = CaptureFixture(); var id = a.ScreenshotId;
            Assert.ThrowsException<ArgumentException>(() => AssistantScreenshotArtifactStore.Review("../bad", "screenshot", "../bad", "approved", _root));
            Assert.ThrowsException<ArgumentException>(() => AssistantScreenshotArtifactStore.Review(id, "screenshot", "wrong", "approved", _root));
            Assert.ThrowsException<ArgumentException>(() => AssistantScreenshotArtifactStore.Review(id, "screenshot", id, "done", _root));
            var missing = Guid.NewGuid().ToString("N");
            Assert.ThrowsException<FileNotFoundException>(() => AssistantScreenshotArtifactStore.Review(missing, "screenshot", missing, "approved", _root));
            using (File.Open(a.MetadataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.ThrowsException<IOException>(() => AssistantScreenshotArtifactStore.Review(id, "screenshot", id, "approved", _root));
            Assert.AreEqual("pending", JsonConvert.DeserializeObject<AssistantScreenshotArtifact>(File.ReadAllText(a.MetadataPath)).ReviewStatus);
        }

        [TestMethod] public void ActualMessageSerializationChecksHistoryConsentBeforeReadingImage()
        {
            var a = CaptureFixture(); var id = a.ScreenshotId;
            var service = Service(Load("{\"Assistant\":{\"EnableUploads\":true}}"));
            var message = new AssistantMessage { Role = "user", Text = "synthetic", AttachmentPaths = new List<string> { a.Path } };
            var convertedRoot = Path.Combine(_root, "converted");
            Assert.ThrowsException<InvalidOperationException>(() => service.BuildMessageContent(message, convertedRoot, _root));
            AssistantScreenshotArtifactStore.Review(id, "screenshot", id, "approved", _root);
            AssistantScreenshotArtifactStore.Review(id, "screenshot-upload", id, "approved", _root);
            StringAssert.Contains(service.BuildMessageContent(message, convertedRoot, _root).ToString(), "data:image/jpeg;base64,");
            AssistantScreenshotArtifactStore.Review(id, "screenshot", id, "rejected", _root);
            Assert.ThrowsException<InvalidOperationException>(() => service.BuildMessageContent(message, convertedRoot, _root));
        }
    }
}

using System;
using System.IO;
using System.Drawing;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class ScreenshotConversationTests
    {
        private string root;
        private AssistantSessionStore store;
        [TestInitialize] public void Init()
        {
            root = Path.Combine(Path.GetTempPath(), "bb-vision-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            store = new AssistantSessionStore(Path.Combine(root, "sessions"), root);
        }
        [TestCleanup] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        private AssistantScreenshotArtifact Capture(string session)
        {
            var id = Guid.NewGuid().ToString("N");
            var path = Path.Combine(root, "capture_" + id + ".png");
            using (var bitmap = new Bitmap(800, 400)) bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            var artifact = new AssistantScreenshotArtifact { ArtifactId = id, SessionId = session, Path = path, Width = 800, Height = 400, CapturedUtc = DateTime.UtcNow, CaptureSource = "synthetic-test" };
            AssistantScreenshotArtifactStore.CompleteArtifact(artifact);
            return artifact;
        }
        [TestMethod] public void AutoApprovalIsPersistedThenAttachedByIdentity()
        {
            var session = store.Create(); var artifact = Capture(session.SessionId);
            store.AttachScreenshot(artifact, new AssistantScreenshotSettings { AutoAttachToChat = true, AutoApproveLocalCaptureForContext = true }, "synthetic-R04");
            var saved = AssistantScreenshotArtifactStore.FindArtifact(artifact.ArtifactId, root);
            Assert.AreEqual("approved", saved.ReviewStatus); Assert.AreEqual("AUTO", saved.ApprovalMode);
            Assert.AreEqual("synthetic-R04", saved.RuntimeBuildId); Assert.IsFalse(saved.CloudSendApproved);
            Assert.IsTrue(artifact.AttachedToConversation);
            CollectionAssert.Contains(store.Get(session.SessionId).PendingScreenshotIds, artifact.ArtifactId);
            Assert.AreEqual(artifact.Path, store.ResolvePendingScreenshotPaths(store.Get(session.SessionId))[0]);
            Assert.AreEqual(AssistantScreenshotArtifactStore.ContentHash(artifact.Path), saved.Sha256);
            using (var image = Image.FromFile(saved.ThumbnailPath)) { Assert.AreEqual(360, image.Width); Assert.AreEqual(180, image.Height); }
        }
        [TestMethod] public void SafeDefaultDoesNotApproveOrAttach()
        {
            var session = store.Create(); var artifact = Capture(session.SessionId);
            store.AttachScreenshot(artifact, new AssistantScreenshotSettings(), "synthetic-R04");
            Assert.AreEqual("pending", artifact.ReviewStatus); Assert.IsFalse(artifact.AttachedToConversation);
            Assert.AreEqual(0, store.Get(session.SessionId).PendingScreenshotIds.Count);
        }
        [TestMethod] public void MissingConversationCannotBecomeAnAttachment()
        {
            var artifact = Capture(Guid.NewGuid().ToString("N"));
            Assert.ThrowsException<InvalidOperationException>(() => store.AttachScreenshot(artifact, new AssistantScreenshotSettings { AutoAttachToChat = true }, "synthetic"));
            Assert.IsFalse(artifact.AttachedToConversation);
        }
        [TestMethod] public void ChangedScreenshotFailsBeforeConversationContextResolution()
        {
            var session = store.Create(); var artifact = Capture(session.SessionId);
            store.AttachScreenshot(artifact, new AssistantScreenshotSettings { AutoAttachToChat = true, AutoApproveLocalCaptureForContext = true }, "synthetic");
            File.AppendAllText(artifact.Path, "changed");
            Assert.ThrowsException<InvalidDataException>(() => store.ResolvePendingScreenshotPaths(store.Get(session.SessionId)));
        }
        [TestMethod] public void TraversalIsRejectedBySessionStore()
        { Assert.ThrowsException<ArgumentException>(() => store.Get("../outside")); }
        [TestMethod] public void CaptureDuringSendSurvivesOlderSessionCompletion()
        {
            var session = store.Create(); var first = Capture(session.SessionId);
            var policy = new AssistantScreenshotSettings { AutoAttachToChat = true, AutoApproveLocalCaptureForContext = true };
            store.AttachScreenshot(first, policy, "synthetic");
            var inFlight = store.Get(session.SessionId);
            var second = Capture(session.SessionId); store.AttachScreenshot(second, policy, "synthetic");
            var message = new AssistantMessage { ResolvedModelId = "fixture" };
            message.AttachmentArtifactIds.Add(first.ArtifactId);
            store.ConsumePendingScreenshots(inFlight, message);
            CollectionAssert.AreEqual(new[] { second.ArtifactId }, store.Get(session.SessionId).PendingScreenshotIds);
        }
        [TestMethod] public void AttemptedTransmissionDoesNotReportLocalOnly()
        {
            var session = store.Create(); var artifact = Capture(session.SessionId);
            AssistantScreenshotArtifactStore.RecordTransmissionAttempt(artifact.ArtifactId, "fixture", root);
            var saved = AssistantScreenshotArtifactStore.FindArtifact(artifact.ArtifactId, root);
            Assert.IsFalse(saved.Receipt.LocalOnly); Assert.IsFalse(saved.SentToModel);
            Assert.AreEqual("ATTEMPTED_OUTCOME_UNKNOWN", saved.Receipt.TransmissionState);
        }
        [TestMethod] public void StaleMessageSaveCannotResurrectDetachedScreenshot()
        {
            var session = store.Create(); var artifact = Capture(session.SessionId);
            store.AttachScreenshot(artifact, new AssistantScreenshotSettings { AutoAttachToChat = true, AutoApproveLocalCaptureForContext = true }, "synthetic");
            var stale = store.Get(session.SessionId);
            store.DetachScreenshot(session.SessionId, artifact.ArtifactId);
            store.Save(stale);
            Assert.AreEqual(0, store.Get(session.SessionId).PendingScreenshotIds.Count);
        }
    }
}

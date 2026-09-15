using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class VisionRoutingTests
    {
        private static AssistantModelProfile Profile(string id, bool vision = false, bool enabled = true)
            => new AssistantModelProfile { Id = id, Name = id, Model = id, ApiBaseUrl = "https://fixture.invalid/v1",
                SupportsVision = vision, Enabled = enabled, SupportsText = true };

        [TestMethod] public void CompatiblePinnedModelIsRetained()
        {
            var selected = Profile("selected", true); var preferred = Profile("preferred", true);
            Assert.AreSame(selected, OpenAiAssistantService.ResolveImageProfile(selected,
                new[] { selected, preferred }, true, preferred.Id, null));
        }
        [TestMethod] public void TextModelUsesOnlyExplicitPreferredThenFallback()
        {
            var selected = Profile("text"); var preferred = Profile("preferred", true);
            var fallback = Profile("fallback", true); var unrelated = Profile("first-in-catalog", true);
            var catalog = new[] { unrelated, selected, preferred, fallback };
            Assert.AreSame(preferred, OpenAiAssistantService.ResolveImageProfile(selected, catalog, true, preferred.Id, fallback.Id));
            preferred.Enabled = false;
            Assert.AreSame(fallback, OpenAiAssistantService.ResolveImageProfile(selected, catalog, true, preferred.Id, fallback.Id));
            Assert.ThrowsException<InvalidOperationException>(() => OpenAiAssistantService.ResolveImageProfile(selected, catalog, true, null, null));
        }
        [TestMethod] public void VisionLookingNameDoesNotGrantCapability()
        {
            var selected = Profile("vision-image-super-model");
            Assert.ThrowsException<InvalidOperationException>(() => OpenAiAssistantService.ResolveImageProfile(selected,
                new[] { selected }, true, selected.Id, null));
            Assert.AreSame(selected, OpenAiAssistantService.ResolveImageProfile(selected, new[] { selected }, false, null, null));
        }
        [TestMethod] public void DisabledAndNonTextCandidatesFailClosed()
        {
            var selected = Profile("text"); var candidate = Profile("vision", true, false);
            Assert.ThrowsException<InvalidOperationException>(() => OpenAiAssistantService.ResolveImageProfile(selected,
                new[] { selected, candidate }, true, candidate.Id, null));
            candidate.Enabled = true; candidate.SupportsText = false;
            Assert.ThrowsException<InvalidOperationException>(() => OpenAiAssistantService.ResolveImageProfile(selected,
                new[] { selected, candidate }, true, candidate.Id, null));
        }

        [TestMethod] public async Task ImagesNeverReceiveMockSuccessWhenCredentialsMissing()
        {
            var root = Path.Combine(Path.GetTempPath(), "BlueBrick-Vision-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            try
            {
                File.WriteAllText(AppIdentity.ConfigPath(root), @"{'Assistant':{'EnableUploads':true,'Mode':'mock','ModelProfiles':[{'Id':'synthetic-vision','Model':'synthetic-vision','ApiBaseUrl':'https://fixture.invalid/v1','IsDefault':true,'SupportsVision':true,'KeyEnvironmentVariable':''}]}}".Replace('\'', '"'));
                var config = AgentConfig.LoadFrom(root);
                var store = new AssistantSessionStore(Path.Combine(root, "sessions"));
                var service = new OpenAiAssistantService(config, new AssistantToolService(config, null), store);
                var imagePath = Path.Combine(root, "synthetic.png"); File.WriteAllBytes(imagePath, new byte[] { 1 });
                var session = await service.CreateSessionAsync();
                var result = await service.SendMessageAsync(session.SessionId, "synthetic", new[] { imagePath });
                Assert.IsFalse(result.AssistantAvailable); Assert.AreEqual("provider_dependency", result.ErrorCode);
                Assert.AreEqual("synthetic-vision", result.Message.ResolvedModelId);
                var chunks = new List<AssistantStreamChunk>();
                await service.SendMessageStreamAsync(session.SessionId, "synthetic", new[] { imagePath }, chunks.Add, CancellationToken.None);
                Assert.IsTrue(chunks.Exists(c => c.Type == "model_resolved" && c.Text == "synthetic-vision"));
                Assert.IsTrue(chunks.Exists(c => c.ErrorCode == "provider_dependency"));
                Assert.IsFalse(chunks.Exists(c => c.Type == "text_delta"));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestMethod] public void PayloadUsesPassedProfileAndRejectsImageForTextOnlyModel()
        {
            var root = Path.Combine(Path.GetTempPath(), "BlueBrick-Vision-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            try
            {
                File.WriteAllText(AppIdentity.ConfigPath(root), "{}");
                var config = AgentConfig.LoadFrom(root);
                var service = new OpenAiAssistantService(config, new AssistantToolService(config, null),
                    new AssistantSessionStore(Path.Combine(root, "sessions")));
                var session = new AssistantSession { SessionId = "synthetic" };
                var resolved = Profile("explicit-body-model", true);
                Assert.AreEqual(resolved.Model, JObject.Parse(service.BuildChatRequestBody(session, resolved)).Value<string>("model"));
                session.Messages.Add(new AssistantMessage { Role = "user", AttachmentPaths = new List<string> { "synthetic.png" } });
                Assert.ThrowsException<InvalidOperationException>(() => service.BuildChatRequestBody(session, Profile("text-only")));
                Assert.ThrowsException<InvalidOperationException>(() => service.BuildMessageContent(session.Messages[0], session.SessionId));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal sealed class AssistantSessionStore
    {
        private static readonly object SessionLock = new object();
        private readonly string _root;
        private readonly string _artifactRoot;

        internal AssistantSessionStore(string root = null, string artifactRoot = null)
        {
            _root = root ?? AppIdentity.AssistantHistoryRoot;
            _artifactRoot = artifactRoot ?? AssistantScreenshotArtifactStore.Root;
            Directory.CreateDirectory(_root);
        }

        internal AssistantSession Create()
        {
            var session = new AssistantSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                CreatedUtc = DateTime.UtcNow
            };
            Save(session);
            return session;
        }

        internal AssistantSession Get(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return null;
            var path = GetPath(sessionId);
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<AssistantSession>(File.ReadAllText(path));
        }

        internal void Save(AssistantSession session)
        {
            lock (SessionLock)
            {
                var current = Get(session.SessionId);
                session.PendingScreenshotIds = current?.PendingScreenshotIds ?? session.PendingScreenshotIds ?? new List<string>();
                WriteSession(session);
            }
        }

        private void WriteSession(AssistantSession session)
        {
            var path = GetPath(session.SessionId);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonConvert.SerializeObject(session, Formatting.Indented));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal void AttachScreenshot(AssistantScreenshotArtifact artifact, AssistantScreenshotSettings policy, string runtimeBuildId)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            var session = Get(artifact.SessionId);
            if (session == null) throw new InvalidOperationException("Screenshot conversation does not exist.");
            artifact.RuntimeBuildId = runtimeBuildId;
            AssistantScreenshotArtifactStore.CompleteArtifact(artifact);
            if (policy?.AutoApproveLocalCaptureForContext == true)
                AssistantScreenshotArtifactStore.Review(artifact.ArtifactId, "screenshot", artifact.ArtifactId, "approved", _artifactRoot,
                    "AUTO", "Assistant.Screenshots.AutoApproveLocalCaptureForContext", runtimeBuildId);
            var persisted = AssistantScreenshotArtifactStore.FindArtifact(artifact.ArtifactId, _artifactRoot);
            if (persisted == null) throw new InvalidDataException("Screenshot persistence could not be re-read.");
            artifact.ReviewStatus = persisted.ReviewStatus;
            artifact.ApprovalMode = persisted.ApprovalMode;
            artifact.ApprovalPolicySource = persisted.ApprovalPolicySource;
            artifact.ReviewedUtc = persisted.ReviewedUtc;
            artifact.ReviewedBy = persisted.ReviewedBy;
            artifact.ReviewNote = persisted.ReviewNote;
            artifact.CloudSendApproved = persisted.CloudSendApproved;
            artifact.Receipt = persisted.Receipt;
            if (policy?.AutoAttachToChat != true || persisted.ReviewStatus != "approved") return;
            if (persisted.SessionId != session.SessionId || persisted.Sha256 != AssistantScreenshotArtifactStore.ContentHash(persisted.Path))
                throw new InvalidDataException("Screenshot identity changed before attachment.");
            lock (SessionLock)
            {
                var latest = Get(session.SessionId);
                if (latest == null) throw new IOException("Conversation disappeared before attachment.");
                if (!latest.PendingScreenshotIds.Contains(artifact.ArtifactId)) latest.PendingScreenshotIds.Add(artifact.ArtifactId);
                WriteSession(latest);
                if (Get(session.SessionId)?.PendingScreenshotIds.Contains(artifact.ArtifactId) != true)
                    throw new IOException("Conversation attachment did not persist.");
            }
            artifact.AttachedToConversation = true;
        }

        internal List<string> ResolvePendingScreenshotPaths(AssistantSession session)
        {
            return (session.PendingScreenshotIds ?? new List<string>()).Select(id =>
            {
                var artifact = AssistantScreenshotArtifactStore.FindArtifact(id, _artifactRoot);
                if (artifact == null || artifact.SessionId != session.SessionId || artifact.ReviewStatus != "approved" ||
                    artifact.Sha256 != AssistantScreenshotArtifactStore.ContentHash(artifact.Path))
                    throw new InvalidDataException("Pending screenshot is missing, changed, rejected or belongs to another conversation.");
                return artifact.Path;
            }).ToList();
        }

        internal void ConsumePendingScreenshots(AssistantSession session, AssistantMessage message)
        {
            lock (SessionLock)
            {
                var current = Get(session.SessionId);
                session.PendingScreenshotIds = (current?.PendingScreenshotIds ?? session.PendingScreenshotIds)
                    .Where(id => !message.AttachmentArtifactIds.Contains(id)).ToList();
                WriteSession(session);
                foreach (var id in session.Messages.SelectMany(m => m.AttachmentArtifactIds ?? new List<string>())
                    .Concat(message.AttachmentArtifactIds).Distinct())
                    AssistantScreenshotArtifactStore.RecordProviderSuccess(id, message.ResolvedModelId, _artifactRoot);
            }
        }

        internal void RecordTransmissionAttempt(AssistantSession session, string profileId)
        {
            foreach (var id in session.Messages.SelectMany(m => m.AttachmentArtifactIds ?? new List<string>()).Distinct())
                AssistantScreenshotArtifactStore.RecordTransmissionAttempt(id, profileId, _artifactRoot);
        }

        internal void DetachScreenshot(string sessionId, string artifactId)
        {
            lock (SessionLock)
            {
                var session = Get(sessionId);
                if (session == null) return;
                session.PendingScreenshotIds.RemoveAll(id => id == artifactId);
                WriteSession(session);
            }
        }

        private string GetPath(string sessionId)
        {
            if (!Guid.TryParseExact(sessionId, "N", out _)) throw new ArgumentException("Invalid conversation identity.");
            return Path.Combine(_root, sessionId + ".json");
        }
    }
}

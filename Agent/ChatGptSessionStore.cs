using System;
using System.IO;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal sealed class ChatGptSessionStore
    {
        private readonly string _root;

        internal ChatGptSessionStore()
        {
            _root = Path.Combine(AppIdentity.AssistantHistoryRoot, "chatgpt-sessions");
            Directory.CreateDirectory(_root);
        }

        internal PreviewSession Create(PreviewSession session)
        {
            session.SessionId = string.IsNullOrWhiteSpace(session.SessionId)
                ? Guid.NewGuid().ToString("N")
                : session.SessionId;
            session.CreatedUtc = session.CreatedUtc == default ? DateTime.UtcNow : session.CreatedUtc;
            Save(session);
            return session;
        }

        internal PreviewSession Get(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return null;
            var path = GetPath(sessionId);
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<PreviewSession>(File.ReadAllText(path));
        }

        internal void Save(PreviewSession session)
        {
            var path = GetPath(session.SessionId);
            File.WriteAllText(path, JsonConvert.SerializeObject(session, Formatting.Indented));
        }

        private string GetPath(string sessionId)
        {
            return Path.Combine(_root, sessionId + ".json");
        }
    }
}

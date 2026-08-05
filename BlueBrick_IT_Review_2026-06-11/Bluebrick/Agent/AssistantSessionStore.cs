using System;
using System.IO;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal sealed class AssistantSessionStore
    {
        private readonly string _root;

        internal AssistantSessionStore()
        {
            _root = AppIdentity.AssistantHistoryRoot;
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
            var path = GetPath(session.SessionId);
            File.WriteAllText(path, JsonConvert.SerializeObject(session, Formatting.Indented));
        }

        private string GetPath(string sessionId)
        {
            return Path.Combine(_root, sessionId + ".json");
        }
    }
}

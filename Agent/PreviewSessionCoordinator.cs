using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace BlueBrick.Agent
{
    internal sealed class PreviewSessionCoordinator
    {
        private readonly ChatGptSessionStore _store;
        private readonly RelayTunnelClient _relayTunnel;
        private readonly ISldWorks _swApp;
        private readonly AgentConfig _config;

        internal PreviewSessionCoordinator(ChatGptSessionStore store, RelayTunnelClient relayTunnel, ISldWorks swApp, AgentConfig config)
        {
            _store = store;
            _relayTunnel = relayTunnel;
            _swApp = swApp;
            _config = config;
        }

        internal PreviewSession CreateSession(string lastScreenshotPath)
        {
            var activeDoc = _swApp.IActiveDoc2 as ModelDoc2;
            var session = _store.Create(new PreviewSession
            {
                ActiveDocumentPath = activeDoc?.GetPathName(),
                ActiveDocumentTitle = activeDoc?.GetTitle(),
                LocalVaultRoot = _config.Vault.Root,
                WorkingFolder = AppIdentity.DefaultWorkingFolder,
                LastScreenshotPath = lastScreenshotPath,
                RelayState = _relayTunnel.State,
                AllowedActions = new List<string>
                {
                    "get_preview_status",
                    "get_active_context",
                    "search_local_vault",
                    "get_review_findings",
                    "capture_preview_screenshot",
                    "open_output_folder",
                    "run_local_review",
                    "get_session_history",
                    "reindex_local_vault",
                    "reset_local_vault"
                }
            });

            session.HandoffUrl = _relayTunnel.BuildHandoffUrl(session.SessionId);
            _store.Save(session);
            return session;
        }

        internal PreviewSession Get(string sessionId)
        {
            return _store.Get(sessionId);
        }

        internal void Save(PreviewSession session)
        {
            _store.Save(session);
        }

        internal IEnumerable<string> GetKnownSessionIds()
        {
            var root = Path.Combine(AppIdentity.AssistantHistoryRoot, "chatgpt-sessions");
            if (!Directory.Exists(root))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(root, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        internal PreviewConfirmationRequest QueueConfirmation(PreviewSession session, PreviewActionRequest request)
        {
            var confirmation = new PreviewConfirmationRequest
            {
                SessionId = session.SessionId,
                ConfirmationId = Guid.NewGuid().ToString("N"),
                ActionName = request.ActionName,
                Parameters = request.Parameters,
                Approved = false
            };
            session.PendingConfirmations.Add(confirmation);
            _store.Save(session);
            return confirmation;
        }

        internal PreviewConfirmationRequest ResolveConfirmation(PreviewSession session, string confirmationId)
        {
            return session.PendingConfirmations.FirstOrDefault(x => x.ConfirmationId == confirmationId);
        }
    }
}

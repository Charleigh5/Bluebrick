using BlueBrick.Relay.Models;

namespace BlueBrick.Relay.Services;

public sealed class McpToolCatalog
{
    private static readonly IReadOnlyList<McpToolDescriptor> Tools = new[]
    {
        new McpToolDescriptor { Name = "get_preview_status", Description = "Get current BlueBrick Lab preview status.", ReadOnlyHint = true },
        new McpToolDescriptor { Name = "get_active_context", Description = "Get the active SOLIDWORKS document context.", ReadOnlyHint = true },
        new McpToolDescriptor { Name = "search_local_vault", Description = "Search the BlueBrick Lab local vault.", ReadOnlyHint = true },
        new McpToolDescriptor { Name = "get_review_findings", Description = "Get review findings for the preview session.", ReadOnlyHint = true },
        new McpToolDescriptor { Name = "capture_preview_screenshot", Description = "Capture the active preview as a screenshot.", ReadOnlyHint = false, DestructiveHint = false },
        new McpToolDescriptor { Name = "open_output_folder", Description = "Open the local working/output folder.", ReadOnlyHint = false, DestructiveHint = false },
        new McpToolDescriptor { Name = "run_local_review", Description = "Queue a local review run for the active preview session.", ReadOnlyHint = false, DestructiveHint = false },
        new McpToolDescriptor { Name = "get_session_history", Description = "Get action history for the preview session.", ReadOnlyHint = true },
        new McpToolDescriptor { Name = "reindex_local_vault", Description = "Reindex the local BlueBrick Lab vault.", ReadOnlyHint = false, DestructiveHint = false },
        new McpToolDescriptor { Name = "reset_local_vault", Description = "Reset the local BlueBrick Lab vault.", ReadOnlyHint = false, DestructiveHint = false },
        new McpToolDescriptor { Name = "start_local_generation", Description = "Defined but disabled in the first hosted validation pass.", Disabled = true },
        new McpToolDescriptor { Name = "apply_safe_action", Description = "Defined but disabled in the first hosted validation pass.", Disabled = true }
    };

    public IReadOnlyList<McpToolDescriptor> GetAll()
    {
        return Tools;
    }
}

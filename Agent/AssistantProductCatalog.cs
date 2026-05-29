using System;
using System.Collections.Generic;

namespace BlueBrick.Agent
{
    internal static class AssistantProductCatalog
    {
        internal static IReadOnlyList<AssistantIntegrationDescriptor> GetIntegrations()
        {
            return new[]
            {
                new AssistantIntegrationDescriptor
                {
                    Id = "salesforce",
                    Name = "Salesforce",
                    Category = "crm",
                    Status = "planned",
                    Summary = "Feasible through a new OAuth-backed read-only integration. The archived legacy class should not be revived directly.",
                    ReadOnlyFirst = true,
                    RequiresOAuth = true,
                    RequiresSecrets = true,
                    RecommendedScopes = new[] { "api", "refresh_token", "offline_access" },
                    FirstObjects = new[] { "Account", "Contact", "Opportunity", "Task", "ContentDocumentLink" },
                    Blockers = new[]
                    {
                        "Connected App client id and redirect URI are not configured.",
                        "Token storage policy must be moved out of repo/config files.",
                        "Object-level permissions and SOQL allowlist need approval."
                    },
                    NextSteps = new[]
                    {
                        "Create a Salesforce Connected App with least-privilege read scopes.",
                        "Store refresh/access tokens in Windows credential storage or another approved secret store.",
                        "Add read-only SOQL wrappers for Account, Contact, Opportunity, Task, and document links.",
                        "Add tests that prove SOQL input is parameterized or allowlisted."
                    }
                },
                new AssistantIntegrationDescriptor
                {
                    Id = "pdm",
                    Name = "SOLIDWORKS PDM",
                    Category = "vault",
                    Status = "config-gated",
                    Summary = "Read-only filename search wrapper exists, but live execution is disabled by default.",
                    ReadOnlyFirst = true,
                    RequiresOAuth = false,
                    RequiresSecrets = false,
                    FirstObjects = new[] { "File", "Folder", "Data card metadata" },
                    Blockers = new[] { "Live PDM search requires explicit local operator validation." },
                    NextSteps = new[] { "Enable AssistantTools.EnablePdmSearch only for approved read-only validation." }
                },
                new AssistantIntegrationDescriptor
                {
                    Id = "epicor",
                    Name = "Epicor",
                    Category = "erp",
                    Status = "config-gated",
                    Summary = "Parameterized read-only part search wrapper exists behind an environment connection string gate.",
                    ReadOnlyFirst = true,
                    RequiresOAuth = false,
                    RequiresSecrets = true,
                    FirstObjects = new[] { "Part", "Opportunity", "Task", "Quote attachment" },
                    Blockers = new[] { "Live Epicor validation needs a non-repo environment connection string." },
                    NextSteps = new[] { "Validate part search, then add opportunity/task wrappers with parameterized queries." }
                }
            };
        }

        internal static IReadOnlyList<AssistantDocumentDescriptor> GetDocuments()
        {
            return new[]
            {
                new AssistantDocumentDescriptor
                {
                    Id = "drawing-pdf",
                    Name = "Drawing PDF",
                    Category = "generated-output",
                    Purpose = "Customer/vendor review, drawing QA, and packet collation.",
                    SourceSubsystem = "DocGenerator",
                    OutputFormats = new[] { "PDF" },
                    Implemented = true,
                    RequiresSolidWorks = true,
                    RequiresPdmApproval = false,
                    AssistantUses = new[] { "summarize drawing", "review visible title block", "attach to packet", "local vault search" }
                },
                new AssistantDocumentDescriptor
                {
                    Id = "packet-pdf",
                    Name = "PDF Packet",
                    Category = "generated-output",
                    Purpose = "Combined packet of generated drawing PDFs for review or handoff.",
                    SourceSubsystem = "GenerateReviewJobManager / DocGenerator",
                    OutputFormats = new[] { "PDF" },
                    Implemented = true,
                    RequiresSolidWorks = true,
                    RequiresPdmApproval = true,
                    AssistantUses = new[] { "review packet status", "explain gate findings", "prepare customer handoff summary" }
                },
                new AssistantDocumentDescriptor
                {
                    Id = "step-export",
                    Name = "STEP Export",
                    Category = "cad-export",
                    Purpose = "Neutral CAD model exchange.",
                    SourceSubsystem = "DocGenerator",
                    OutputFormats = new[] { "STEP", "STP" },
                    Implemented = true,
                    RequiresSolidWorks = true,
                    RequiresPdmApproval = true,
                    AssistantUses = new[] { "confirm export path", "include in release checklist", "search generated artifacts" }
                },
                new AssistantDocumentDescriptor
                {
                    Id = "dxf-export",
                    Name = "DXF Export",
                    Category = "cad-export",
                    Purpose = "Flat pattern/manufacturing export.",
                    SourceSubsystem = "DocGenerator",
                    OutputFormats = new[] { "DXF" },
                    Implemented = true,
                    RequiresSolidWorks = true,
                    RequiresPdmApproval = true,
                    AssistantUses = new[] { "confirm manufacturing output", "validate export checklist", "local vault lookup" }
                },
                new AssistantDocumentDescriptor
                {
                    Id = "assistant-screenshot-artifact",
                    Name = "Assistant Screenshot Artifact",
                    Category = "assistant-evidence",
                    Purpose = "Visual evidence with annotations and extracted contacts.",
                    SourceSubsystem = "AssistantImageTools / AssistantScreenshotAnalyzer",
                    OutputFormats = new[] { "PNG", "JSON metadata" },
                    Implemented = true,
                    RequiresSolidWorks = false,
                    RequiresPdmApproval = false,
                    AssistantUses = new[] { "visual troubleshooting", "contact extraction", "UI review", "traceable chat context" }
                },
                new AssistantDocumentDescriptor
                {
                    Id = "salesforce-opportunity-brief",
                    Name = "Salesforce Opportunity Brief",
                    Category = "crm-brief",
                    Purpose = "Read-only summary of opportunity, account, contacts, tasks, and linked documents.",
                    SourceSubsystem = "Planned Salesforce OAuth integration",
                    OutputFormats = new[] { "JSON", "Markdown" },
                    Implemented = false,
                    RequiresSolidWorks = false,
                    RequiresPdmApproval = false,
                    AssistantUses = new[] { "prepare context before generation", "contact lookup", "customer handoff planning" }
                }
            };
        }
    }
}

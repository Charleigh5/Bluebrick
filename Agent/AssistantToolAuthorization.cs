using System;

namespace BlueBrick.Agent
{
    internal class AssistantToolAuthorization
    {
        public string ApprovalId { get; set; }
        public string ApprovedBy { get; set; }
        public string Reason { get; set; }
        public DateTime? ApprovedUtc { get; set; }
        public bool Granted { get; set; }

        internal static AssistantToolAuthorization None()
        {
            return new AssistantToolAuthorization
            {
                Granted = false
            };
        }
    }
}

using System;

namespace BlueBrick.Audit.Contracts
{
    /// <summary>
    /// Typed partial errors for an audit run. Per BB-M001 packet section 10
    /// and section 17 (requirement 10: "Return typed partial errors when
    /// individual property reads fail"), partial errors are surfaced in
    /// <see cref="AuditExecutionReceipt.Errors"/> and in
    /// <see cref="AuditRunResult.Errors"/> instead of throwing an exception
    /// that bubbles out of the audit run.
    /// </summary>
    [Serializable]
    public sealed class AuditError
    {
        /// <summary>Discriminator code. Must be one of the <see cref="AuditErrorCodes"/> constants.</summary>
        public string Code { get; set; }

        /// <summary>Correlation ID matching the parent <see cref="AuditExecutionReceipt.CorrelationId"/>.</summary>
        public string CorrelationId { get; set; }

        /// <summary>Human-readable message safe for receipts; never contains a secret or full path.</summary>
        public string Message { get; set; }

        /// <summary>Optional subscope (property name or configuration) where the error happened.</summary>
        public string Scope { get; set; }
    }

    /// <summary>Stable codes used by <see cref="AuditError.Code"/>. Constant strings, not an enum, so receipts stay JSON-serializable without enum-name coupling.</summary>
    public static class AuditErrorCodes
    {
        /// <summary>SOLIDWORKS COM call attempted off the proven UI/main thread.</summary>
        public const string COM_THREAD_VIOLATION = "COM_THREAD_VIOLATION";

        /// <summary>No active document open in the SOLIDWORKS session at audit time.</summary>
        public const string NO_ACTIVE_DOCUMENT = "NO_ACTIVE_DOCUMENT";

        /// <summary>One or more individual property reads failed; partial result returned.</summary>
        public const string READ_FAILURE = "READ_FAILURE";

        /// <summary>SOLIDWORKS runtime version could not be classified; forced read-only limited.</summary>
        public const string UNKNOWN_RUNTIME = "UNKNOWN_RUNTIME";

        /// <summary>Installed interop does not expose a desired API member; safest compatible read path was used and the limitation recorded.</summary>
        public const string INTEROP_LIMITATION = "INTEROP_LIMITATION";

        /// <summary>Caller passed an invalid operation mode for the requested scope.</summary>
        public const string INVALID_MODE = "INVALID_MODE";

        /// <summary>Bounded all-config limit was reached; remaining configurations were not read.</summary>
        public const string CONFIG_LIMIT_REACHED = "CONFIG_LIMIT_REACHED";
    }
}

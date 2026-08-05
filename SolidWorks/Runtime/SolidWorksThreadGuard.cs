using System;
using System.Threading;
using BlueBrick.Audit.Contracts;

namespace BlueBrick.SolidWorks.Runtime
{
    /// <summary>
    /// Records the managed thread ID of the proven SOLIDWORKS/UI thread
    /// and enforces main-thread access for SOLIDWORKS COM calls. Per
    /// BB-M001 packet §15: <c>VerifyAccess()</c> throws a typed
    /// <see cref="AuditError"/> with code
    /// <see cref="AuditErrorCodes.COM_THREAD_VIOLATION"/> when called
    /// from a thread other than the proven UI thread.
    /// </summary>
    public sealed class SolidWorksThreadGuard : ISolidWorksMainThreadDispatcher
    {
        private readonly int _mainThreadId;

        /// <summary>
        /// Construct the guard from the current managed thread ID. The
        /// caller is expected to invoke this from the SOLIDWORKS/UI
        /// thread. Captures <see cref="Thread.CurrentThread"/>
        /// <c>ManagedThreadId</c> exactly once.
        /// </summary>
        public SolidWorksThreadGuard()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>Test-friendly constructor allowing an explicit thread ID; used by <c>ThreadGuard_WrongThread_ThrowsTypedViolation</c>.</summary>
        public SolidWorksThreadGuard(int mainThreadId)
        {
            _mainThreadId = mainThreadId;
        }

        /// <inheritdoc />
        public int MainThreadId => _mainThreadId;

        /// <inheritdoc />
        public bool CheckAccess()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        /// <inheritdoc />
        public void VerifyAccess()
        {
            if (!CheckAccess())
            {
                throw new SolidWorksThreadViolationException(
                    "SOLIDWORKS COM call attempted off the proven UI/main thread. " +
                    "Proven main thread id=" + _mainThreadId + ", caller thread id=" + Thread.CurrentThread.ManagedThreadId + ". " +
                    "Per BB-M001 packet §15 no Task.Run/background-thread COM access is permitted.");
            }
        }
    }

    /// <summary>
    /// Typed exception surfaced for COM thread violations. Per BB-M001
    /// packet §15 (requirement: "<c>VerifyAccess()</c> throws a typed
    /// <c>COM_THREAD_VIOLATION</c>") and §17 (typed partial errors).
    /// The classifier label is the literal string
    /// <see cref="AuditErrorCodes.COM_THREAD_VIOLATION"/> so the audit
    /// receipts can record it directly.
    /// </summary>
    public sealed class SolidWorksThreadViolationException : Exception
    {
        /// <summary>The stable audit error code matching <see cref="AuditErrorCodes.COM_THREAD_VIOLATION"/>.</summary>
        public string ErrorCode => AuditErrorCodes.COM_THREAD_VIOLATION;

        /// <summary>Create a new violation with a descriptive message.</summary>
        public SolidWorksThreadViolationException(string message) : base(message)
        {
        }

        /// <summary>Create a new violation chaining an inner exception.</summary>
        public SolidWorksThreadViolationException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}

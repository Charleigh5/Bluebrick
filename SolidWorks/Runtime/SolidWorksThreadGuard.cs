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
        private readonly SynchronizationContext _syncContext;
        private readonly Func<Action, bool> _mainThreadInvoker;

        /// <summary>
        /// Construct the guard from the current managed thread ID. The
        /// caller is expected to invoke this from the SOLIDWORKS/UI
        /// thread. Captures <see cref="Thread.CurrentThread"/>
        /// <c>ManagedThreadId</c> exactly once and captures
        /// <see cref="SynchronizationContext.Current"/> for STA dispatch.
        /// </summary>
        public SolidWorksThreadGuard()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _syncContext = SynchronizationContext.Current;
        }

        /// <summary>Test-friendly constructor allowing an explicit thread ID; used by <c>ThreadGuard_WrongThread_ThrowsTypedViolation</c>.</summary>
        public SolidWorksThreadGuard(int mainThreadId)
        {
            _mainThreadId = mainThreadId;
            _syncContext = SynchronizationContext.Current;
        }

        /// <summary>Constructor that captures an explicit <see cref="SynchronizationContext"/> for deterministic tests or Control.Invoke wiring.</summary>
        public SolidWorksThreadGuard(int mainThreadId, SynchronizationContext syncContext)
        {
            _mainThreadId = mainThreadId;
            _syncContext = syncContext;
        }

        /// <summary>
        /// Construct a guard with an explicit synchronous UI-thread invoker.
        /// The invoker must return <c>true</c> only after the action has run on
        /// the proven SOLIDWORKS/UI thread. A WinForms <c>Control.Invoke</c>
        /// bridge captured during <c>ConnectToSW</c> is the production use.
        /// </summary>
        public SolidWorksThreadGuard(int mainThreadId, Func<Action, bool> mainThreadInvoker)
        {
            _mainThreadId = mainThreadId;
            _syncContext = null;
            _mainThreadInvoker = mainThreadInvoker;
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

        /// <inheritdoc />
        public bool TryInvoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (CheckAccess())
            {
                action();
                return true;
            }
            if (_mainThreadInvoker != null)
            {
                return _mainThreadInvoker(action);
            }
            if (_syncContext != null)
            {
                Exception dispatchEx = null;
                _syncContext.Send(_ =>
                {
                    try { action(); } catch (Exception ex) { dispatchEx = ex; }
                }, null);
                if (dispatchEx != null) throw dispatchEx;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (TryInvoke(action)) return;
            throw new SolidWorksThreadViolationException(
                "Cannot marshal to proven main thread id=" + _mainThreadId + ". No usable SynchronizationContext or Control.Invoke dispatcher was captured at ConnectToSW.");
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

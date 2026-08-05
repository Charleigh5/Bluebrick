using System;

namespace BlueBrick.SolidWorks.Runtime
{
    /// <summary>
    /// Main-thread dispatcher contract for SOLIDWORKS COM access. Per
    /// BB-M001 packet §15:
    /// <list type="bullet">
    /// <item>does not execute arbitrary background COM calls;</item>
    /// <item>no <c>Task.Run</c> around SOLIDWORKS API access;</item>
    /// <item>snapshots complete before asynchronous/report work;</item>
    /// <item>unknown runtime version forces read-only limited status, not mutation.</item>
    /// </list>
    /// </summary>
    public interface ISolidWorksMainThreadDispatcher
    {
        /// <summary>The proven managed thread ID of the SOLIDWORKS UI/COM thread. Recorded at add-in startup; never mutate from background threads.</summary>
        int MainThreadId { get; }

        /// <summary>True iff the caller's managed thread ID equals <see cref="MainThreadId"/>.</summary>
        bool CheckAccess();

        /// <summary>Throws a typed <see cref="BlueBrick.Audit.Contracts.AuditError"/>-backing COM_THREAD_VIOLATION when called from a non-UI thread.</summary>
        void VerifyAccess();
    }
}

namespace Vira.Next.Engine.Hardware;

// A converter still may own or recreate its output. Callers must retain the entire
// invocation directory, and chains must not attempt another converter on these paths.
public sealed class CadChildExitUnconfirmedException : InvalidOperationException
{
    public const string ErrorCode = "OCCT_STOP_FAILED";
    public CadChildExitUnconfirmedException()
        : base("OCCT_STOP_FAILED: Child exit could not be confirmed; output may still be in use.") { }
}

using System.Threading.Tasks;

namespace BlueBrick.Agent
{
    /// <summary>
    /// Narrow, testable completion seam for the legacy fallback navigation.
    /// Dispatching NavigateToString is not proof that the fallback rendered.
    /// </summary>
    internal enum AssistantWebViewFallbackNavigationOutcome
    {
        Success,
        Failure,
        Timeout
    }

    internal sealed class AssistantWebViewFallbackNavigationCoordinator
    {
        private readonly TaskCompletionSource<AssistantWebViewFallbackNavigationOutcome>
            _completion = new TaskCompletionSource<AssistantWebViewFallbackNavigationOutcome>();

        internal Task<AssistantWebViewFallbackNavigationOutcome> Completion => _completion.Task;

        internal void RecordCompleted(bool isSuccess, string status)
        {
            _completion.TrySetResult(isSuccess
                ? AssistantWebViewFallbackNavigationOutcome.Success
                : AssistantWebViewFallbackNavigationOutcome.Failure);
        }

        internal void RecordTimeout()
        {
            _completion.TrySetResult(AssistantWebViewFallbackNavigationOutcome.Timeout);
        }
    }
}

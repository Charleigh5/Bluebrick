using System;
using System.Threading.Tasks;

namespace BlueBrick.Agent
{
    /// <summary>
    /// Shares one initialization attempt between concurrent callers without
    /// creating a background worker or invoking any provider/session path.
    /// </summary>
    internal sealed class AssistantInitializationGate
    {
        private readonly object _sync = new object();
        private Task _inFlight;

        internal Task GetOrStart(Func<Task> initialize)
        {
            if (initialize == null)
                throw new ArgumentNullException(nameof(initialize));

            lock (_sync)
            {
                if (_inFlight != null)
                    return _inFlight;

                var completion = new TaskCompletionSource<bool>();
                var inFlight = completion.Task;
                _inFlight = inFlight;
                RunInitializeAsync(initialize, completion);
                // The initializer is permitted to complete synchronously.  Return
                // the task captured before its finally block clears _inFlight.
                return inFlight;
            }
        }

        private async void RunInitializeAsync(
            Func<Task> initialize,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                await initialize().ConfigureAwait(false);
                completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_inFlight, completion.Task))
                        _inFlight = null;
                }
            }
        }
    }
}

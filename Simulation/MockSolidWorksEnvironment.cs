using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlueBrick.Simulation
{
    public class ApiCallEventArgs : EventArgs
    {
        public ApiCallEventArgs(string name, IReadOnlyDictionary<string, object?> parameters)
        {
            Name = name;
            Parameters = parameters;
        }

        public string Name { get; }

        public IReadOnlyDictionary<string, object?> Parameters { get; }
    }

    public class MockSolidWorksEnvironment
    {
        private readonly BindingList<MockDocument> _documents = new BindingList<MockDocument>();
        private readonly MockApiLogger _logger = new MockApiLogger();

        public MockSolidWorksEnvironment()
        {
            Status = "Idle";
        }

        public event EventHandler<ApiCallEventArgs>? ApiCalled;

        public BindingList<MockDocument> Documents => _documents;

        public MockApiLogger Logger => _logger;

        public string Status { get; private set; }

        public MockDocument CreateDocument(MockDocumentType type, string name)
        {
            var doc = new MockDocument(type, name);
            _documents.Add(doc);
            RaiseApiCall("CreateDocument", new Dictionary<string, object?>
            {
                ["Type"] = type,
                ["Name"] = name
            });
            _logger.Log($"Document created: {doc}");
            return doc;
        }

        public void CloseDocument(MockDocument document)
        {
            if (!_documents.Contains(document))
            {
                return;
            }

            _documents.Remove(document);
            RaiseApiCall("CloseDocument", new Dictionary<string, object?>
            {
                ["Name"] = document.Name,
                ["Type"] = document.DocumentType
            });
            _logger.Log($"Document closed: {document.Name}");
        }

        public void ChangeState(MockDocument document, string state)
        {
            if (!_documents.Contains(document))
            {
                return;
            }

            document.UpdateState(state);
            RaiseApiCall("ChangeState", new Dictionary<string, object?>
            {
                ["Name"] = document.Name,
                ["State"] = state
            });
            _logger.Log($"Document state changed: {document.Name} -> {state}");
        }

        public async Task<ApiExecutionResult> ExecuteAsync(string commandName, Func<CancellationToken, Task> action, CancellationToken token)
        {
            Status = $"Executing {commandName}";
            RaiseApiCall(commandName, new Dictionary<string, object?>());
            _logger.Log($"Started {commandName}");
            try
            {
                await action(token);
                Status = "Idle";
                _logger.Log($"Completed {commandName}");
                return ApiExecutionResult.Success(commandName);
            }
            catch (OperationCanceledException)
            {
                Status = "Cancelled";
                _logger.Log($"Cancelled {commandName}");
                return ApiExecutionResult.Cancelled(commandName);
            }
            catch (Exception ex)
            {
                Status = "Error";
                _logger.Log($"Error during {commandName}: {ex.Message}");
                return ApiExecutionResult.Failed(commandName, ex.Message);
            }
        }

        public MockDocument? FindDocument(string name)
        {
            return _documents.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private void RaiseApiCall(string name, IReadOnlyDictionary<string, object?> parameters)
        {
            ApiCalled?.Invoke(this, new ApiCallEventArgs(name, parameters));
        }
    }

    public class ApiExecutionResult
    {
        private ApiExecutionResult(string command, bool cancelled, bool success, string? message)
        {
            Command = command;
            Cancelled = cancelled;
            Success = success;
            Message = message;
        }

        public string Command { get; }

        public bool Cancelled { get; }

        public bool Success { get; }

        public string? Message { get; }

        public static ApiExecutionResult Success(string command) => new ApiExecutionResult(command, false, true, null);

        public static ApiExecutionResult Cancelled(string command) => new ApiExecutionResult(command, true, false, "Cancelled by user");

        public static ApiExecutionResult Failed(string command, string error) => new ApiExecutionResult(command, false, false, error);
    }
}

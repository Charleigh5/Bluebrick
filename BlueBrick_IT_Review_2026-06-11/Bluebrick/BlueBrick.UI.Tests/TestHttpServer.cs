using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BlueBrick.UI.Tests
{
    /// <summary>
    /// In-process HTTP server for testing WinForms controls that make HTTP requests.
    /// Uses HttpListener on a random port to avoid conflicts with other services.
    /// </summary>
    public class TestHttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<string, Func<HttpListenerRequest, object>> _endpoints;
        private readonly ConcurrentDictionary<string, int> _callCounts;
        private Task _listenerTask;

        /// <summary>
        /// Gets the base URL of the test server (e.g., "http://localhost:12345/").
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Gets the port the server is listening on.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Creates and starts a new test HTTP server on a random available port.
        /// </summary>
        public TestHttpServer()
        {
            _endpoints = new ConcurrentDictionary<string, Func<HttpListenerRequest, object>>();
            _callCounts = new ConcurrentDictionary<string, int>();
            _cts = new CancellationTokenSource();

            // Find an available port by binding to port 0
            var tempListener = new TcpPortFinder();
            Port = tempListener.FindAvailablePort();

            BaseUrl = $"http://localhost:{Port}/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(BaseUrl);
        }

        /// <summary>
        /// Starts the HTTP server and begins accepting requests.
        /// </summary>
        public void Start()
        {
            _listener.Start();
            _listenerTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));
        }

        /// <summary>
        /// Registers an endpoint handler that returns a JSON response.
        /// </summary>
        /// <param name="path">The URL path (e.g., "/agent/telemetry/summary")</param>
        /// <param name="handler">Function that takes the request and returns an object to serialize as JSON</param>
        public void RegisterEndpoint(string path, Func<HttpListenerRequest, object> handler)
        {
            var normalizedPath = path.StartsWith("/") ? path : "/" + path;
            _endpoints[normalizedPath] = handler;
            _callCounts[normalizedPath] = 0;
        }

        /// <summary>
        /// Registers a simple endpoint that always returns the same response.
        /// </summary>
        /// <param name="path">The URL path</param>
        /// <param name="response">The object to return (will be serialized as JSON)</param>
        public void RegisterEndpoint(string path, object response)
        {
            RegisterEndpoint(path, _ => response);
        }

        /// <summary>
        /// Gets the number of times an endpoint was called.
        /// </summary>
        /// <param name="path">The URL path</param>
        /// <returns>Number of calls to this endpoint</returns>
        public int GetCallCount(string path)
        {
            var normalizedPath = path.StartsWith("/") ? path : "/" + path;
            return _callCounts.TryGetValue(normalizedPath, out var count) ? count : 0;
        }

        /// <summary>
        /// Resets the call count for all endpoints.
        /// </summary>
        public void ResetCallCounts()
        {
            foreach (var key in _callCounts.Keys)
            {
                _callCounts[key] = 0;
            }
        }

        private async Task ProcessRequestsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context), ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    // Expected when shutting down
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var path = request.Url.AbsolutePath;

                // Increment call count
                _callCounts.AddOrUpdate(path, 1, (_, count) => count + 1);

                if (_endpoints.TryGetValue(path, out var handler))
                {
                    var result = handler(request);
                    var json = JsonConvert.SerializeObject(result);
                    var buffer = Encoding.UTF8.GetBytes(json);

                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    // Return 404 for unregistered endpoints
                    var errorJson = JsonConvert.SerializeObject(new { error = "Not found", path });
                    var buffer = Encoding.UTF8.GetBytes(errorJson);

                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 404;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                // Return 500 for handler errors
                var errorJson = JsonConvert.SerializeObject(new { error = ex.Message });
                var buffer = Encoding.UTF8.GetBytes(errorJson);

                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 500;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                response.Close();
            }
        }

        /// <summary>
        /// Stops the server and releases resources.
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
            
            try
            {
                _listenerTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions
            }

            _cts.Dispose();
        }
    }

    /// <summary>
    /// Helper class to find an available TCP port.
    /// </summary>
    internal class TcpPortFinder
    {
        public int FindAvailablePort()
        {
            // Use port 0 to let the OS assign an available port
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}

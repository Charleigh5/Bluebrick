using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BlueBrick.UI.Tests.Stubs
{
    public class TestHttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Dictionary<string, Func<HttpListenerContext, string>> _handlers = new Dictionary<string, Func<HttpListenerContext, string>>();
        private readonly Dictionary<string, Func<HttpListenerContext, Task>> _rawHandlers = new Dictionary<string, Func<HttpListenerContext, Task>>();
        private bool _running;

        public string BaseUrl { get; }

        public TestHttpServer(int port = 17177)
        {
            BaseUrl = $"http://localhost:{port}/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(BaseUrl);
        }

        public void Start()
        {
            _listener.Start();
            _running = true;
            Task.Run(HandleRequests);
        }

        public void Stop()
        {
            _running = false;
            _listener.Stop();
        }

        public void RegisterHandler(string path, Func<HttpListenerContext, string> handler)
        {
            _handlers[path.ToLower()] = handler;
        }

        /// <summary>
        /// Registers a raw handler that owns the HttpListenerContext, enabling streamed,
        /// stalled, or withheld responses (e.g., SSE cancellation regression tests).
        /// Register before Start; handlers run off the listener loop and close the
        /// response themselves.
        /// </summary>
        public void RegisterRawHandler(string path, Func<HttpListenerContext, Task> handler)
        {
            _rawHandlers[path.ToLower()] = handler;
        }

        private async Task HandleRequests()
        {
            while (_running)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var path = context.Request.Url.AbsolutePath.ToLower();

                    if (_rawHandlers.TryGetValue(path, out var rawHandler))
                    {
                        // Raw handlers own the response; client aborts mid-stream are expected.
                        _ = Task.Run(async () =>
                        {
                            try { await rawHandler(context); }
                            catch (HttpListenerException) { }
                            catch (ObjectDisposedException) { }
                            finally { try { context.Response.Close(); } catch { } }
                        });
                        continue;
                    }

                    if (_handlers.TryGetValue(path, out var handler))
                    {
                        var response = handler(context);
                        var buffer = Encoding.UTF8.GetBytes(response);
                        context.Response.ContentLength64 = buffer.Length;
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    context.Response.Close();
                }
                catch (HttpListenerException) when (!_running) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in TestHttpServer: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
            ((IDisposable)_listener).Dispose();
        }
    }
}

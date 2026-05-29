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

        private async Task HandleRequests()
        {
            while (_running)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var path = context.Request.Url.AbsolutePath.ToLower();
                    
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

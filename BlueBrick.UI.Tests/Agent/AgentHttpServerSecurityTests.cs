using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    // P0.6 bridge security tests (PLAN_P0_GATES_AND_BUG_FIXES.md Step 13) plus the
    // /qa/run self-proxy regression test (roadmap Step 0: "Fix or remove route").
    // These run the real AgentHttpServer on a random loopback port with temp-dir
    // vault settings and mock assistant mode; no registry, no C:\BlueBrick, no Lab
    // runtime state. The bridge auth token is read via reflection and never printed.
    [TestClass]
    public class AgentHttpServerSecurityTests
    {
        private AgentHttpServer _server;
        private string _baseUrl;
        private string _authToken;

        [TestInitialize]
        public void Setup()
        {
            var vaultRoot = Path.Combine(Path.GetTempPath(), "bb-bridge-sec-" + Guid.NewGuid().ToString("N"));
            var config = new AgentConfig
            {
                Agent = new AgentSettings { BridgePort = FindAvailablePort(), OverlayColor = "#D9FF5A" },
                Vault = new VaultSettings
                {
                    Root = Path.Combine(vaultRoot, "vault"),
                    SampleSeedRoot = Path.Combine(vaultRoot, "samples"),
                    GeneratedRoot = Path.Combine(vaultRoot, "vault", "generated"),
                    ThumbsRoot = Path.Combine(vaultRoot, "vault", "thumbs"),
                    MetadataRoot = Path.Combine(vaultRoot, "vault", "db"),
                    LogRoot = Path.Combine(vaultRoot, "vault", "logs"),
                    SourceRoot = Path.Combine(vaultRoot, "vault", "source")
                },
                Assistant = new AssistantSettings
                {
                    ApiBaseUrl = "https://api.openai.com/v1",
                    Model = "gpt-4.1-mini",
                    Mode = "mock",
                    SystemPrompt = "mock",
                    Detail = "low",
                    MaxImageDimension = 1600,
                    JpegQuality = 75,
                    ConnectionTestPrompt = "ready",
                    RequireExplicitUploadConsent = true,
                    MaxHistory = 10
                },
                Relay = new RelaySettings()
            };

            _server = new AgentHttpServer(null, config, null);
            _server.Start();
            _baseUrl = "http://127.0.0.1:" + config.Agent.BridgePort;
            _authToken = ReadAuthTokenViaReflection();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _server?.Stop();
            _server = null;
            _authToken = null;
        }

        [TestMethod]
        public async Task Bridge_AuthRequired_Returns403_WhenTokenMissing()
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/agent/knowledge_base/refresh"))
            {
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode,
                    "The bridge must return 403 (not 401) when X-Agent-Auth is missing or invalid.");
                Assert.IsTrue(content.Contains("Invalid or missing authentication token"));
            }
        }

        [TestMethod]
        public async Task Bridge_BodySizeLimit_Returns413_WhenBodyExceedsMax()
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/agent/knowledge_base/refresh"))
            {
                request.Headers.Add("X-Agent-Auth", _authToken);
                request.Content = new StringContent(new string('x', 2 * 1024 * 1024), Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode,
                    "POST bodies above MaxRequestBodyBytes (1 MiB) must be rejected with 413.");
                Assert.IsTrue(content.Contains("Request body too large"));
            }
        }

        [TestMethod]
        public async Task Bridge_BodySizeLimit_AllowsBodyWithinMax()
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/agent/knowledge_base/refresh"))
            {
                request.Headers.Add("X-Agent-Auth", _authToken);
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                    "A small authenticated POST must pass the body-size gate.");
            }
        }

        [TestMethod]
        public async Task QaRun_Post_Returns404_RouteRemoved_NoSelfProxy()
        {
            // The /qa/run route was removed (roadmap Step 0: "Fix or remove route").
            // The removed handler proxied to its own bridge URL and looped forever;
            // a re-introduced self-proxy must fail this test on timeout, not hang it.
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/qa/run"))
            {
                request.Headers.Add("X-Agent-Auth", _authToken);
                request.Content = new StringContent("{\"scriptId\":\"smoke\"}", Encoding.UTF8, "application/json");

                var sendTask = client.SendAsync(request);
                var completed = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(15)));

                Assert.IsTrue(completed == sendTask, "/qa/run did not respond within 15s - self-proxy loop suspected.");

                using (var response = await sendTask)
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode,
                        "POST /qa/run must return 404: the self-proxying route was removed.");
                }
            }
        }

        private static int FindAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string ReadAuthTokenViaReflection()
        {
            var method = typeof(AgentHttpServer).GetMethod(
                "EnsureAuthToken",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "EnsureAuthToken must exist on AgentHttpServer.");

            var tokenPath = method.Invoke(null, null) as string;
            Assert.IsFalse(string.IsNullOrWhiteSpace(tokenPath));

            var token = File.ReadAllText(tokenPath).Trim();
            Assert.IsFalse(string.IsNullOrWhiteSpace(token));
            return token;
        }
    }
}

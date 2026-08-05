using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Integration
{
    [TestClass]
    public class AgentHttpServerTests
    {
        private TestHttpServer _server;
        private HttpClient _client;

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer();
            _server.Start();
            _client = new HttpClient();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _client?.Dispose();
            _server?.Dispose();
        }

        [TestMethod]
        public async Task Server_ShouldHandleRegisteredEndpoints()
        {
            // Arrange
            _server.RegisterEndpoint("/test/endpoint", new { message = "hello" });

            // Act
            var response = await _client.GetAsync($"{_server.BaseUrl}test/endpoint");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.IsTrue(content.Contains("hello"));
        }

        [TestMethod]
        public async Task Server_ShouldReturn404_ForUnregisteredEndpoints()
        {
            // Act
            var response = await _client.GetAsync($"{_server.BaseUrl}unknown/endpoint");

            // Assert
            Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }

        [TestMethod]
        public async Task Server_ShouldCaptureCallCounts()
        {
            // Arrange
            _server.RegisterEndpoint("/count/me", new { });

            // Act
            await _client.GetAsync($"{_server.BaseUrl}count/me");
            await _client.GetAsync($"{_server.BaseUrl}count/me");

            // Assert
            Assert.AreEqual(2, _server.GetCallCount("/count/me"));
        }
    }
}

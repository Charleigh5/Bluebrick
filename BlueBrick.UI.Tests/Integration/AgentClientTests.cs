using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;
using System.Net.Http;

namespace BlueBrick.UI.Tests.Integration
{
    [TestClass]
    public class AgentClientTests
    {
        private TestHttpServer _server;
        private string _originalBaseUrl;

        [TestInitialize]
        public void Setup()
        {
            _server = new TestHttpServer();
            _server.Start();
            _originalBaseUrl = AgentPanelClient.BaseUrl;
            AgentPanelClient.BaseUrl = _server.BaseUrl.TrimEnd('/');
        }

        [TestCleanup]
        public void Cleanup()
        {
            AgentPanelClient.BaseUrl = _originalBaseUrl;
            _server?.Dispose();
        }

        [TestMethod]
        public async Task GetHealth_ShouldReturnTrue_WhenServerIsHealthy()
        {
            // Arrange
            _server.RegisterEndpoint("/agent/health", new { status = "healthy" });

            // Act
            var result = await AgentPanelClient.GetHealthAsync();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task GetHealth_ShouldReturnFalse_WhenServerReturnsError()
        {
            // Arrange - no endpoint registered => 404
            
            // Act
            var result = await AgentPanelClient.GetHealthAsync();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task PostPlan_ShouldReturnResult_WhenValidRequest()
        {
            // Arrange
            _server.RegisterEndpoint("/agent/plan", new 
            { 
                status = "complete", 
                result = "Plan executed successfully" 
            });

            // Act
            var response = await AgentPanelClient.PostPlanAsync("Test Query");

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual("Plan executed successfully", response.Result);
        }
    }
}

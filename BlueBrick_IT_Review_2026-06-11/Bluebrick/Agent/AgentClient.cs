using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal static class AgentClient
    {
        // Singleton HttpClient to prevent socket exhaustion (PERF-02 fix)
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        private static readonly Lazy<string> _authToken = new Lazy<string>(LoadAuthToken);
        
        /// <summary>
        /// Send query to agent service asynchronously.
        /// </summary>
        /// <param name="query">The query string to send</param>
        /// <returns>The agent's response</returns>
        /// <remarks>
        /// This method is fully async to prevent UI thread blocking (PERF-01 fix).
        /// Includes authentication token (CRITICAL-01/02 fix).
        /// </remarks>
        internal static async Task<string> SendQueryAsync(string query)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:17178/agent/plan");
            
            // Add authentication header
            request.Headers.Add("X-Agent-Auth", _authToken.Value);
            
            var payload = JsonConvert.SerializeObject(new { query });
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
        
        private static string LoadAuthToken()
        {
            var tokenPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VIRA",
                ".agent_token"
            );
            
            if (!File.Exists(tokenPath))
            {
                throw new InvalidOperationException(
                    "Authentication token not found. Please ensure the Agent Service has been started first."
                );
            }
            
            return File.ReadAllText(tokenPath).Trim();
        }
    }
}

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    internal sealed class SessionOutboxStore
    {
        private readonly string _path;

        internal SessionOutboxStore(string path)
        {
            _path = path;
        }

        internal void Save(
            ConcurrentDictionary<string, GenerateReviewResult> jobs,
            ConcurrentDictionary<string, GenerateReviewRequest> requests)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var snapshot = new
            {
                jobs = jobs.ToDictionary(pair => pair.Key, pair => pair.Value),
                requests = requests.ToDictionary(pair => pair.Key, pair => pair.Value)
            };
            File.WriteAllText(_path, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
        }
    }
}

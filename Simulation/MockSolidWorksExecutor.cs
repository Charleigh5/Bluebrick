using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlueBrick.Simulation
{
    public class BreakpointManager
    {
        private readonly Dictionary<string, HashSet<int>> _breakpoints = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<(string file, int line)>? BreakpointHit;

        public void ToggleBreakpoint(string file, int line)
        {
            if (!_breakpoints.TryGetValue(file, out var lines))
            {
                lines = new HashSet<int>();
                _breakpoints[file] = lines;
            }

            if (!lines.Add(line))
            {
                lines.Remove(line);
            }
        }

        public bool HasBreakpoint(string file, int line)
        {
            return _breakpoints.TryGetValue(file, out var lines) && lines.Contains(line);
        }

        public IReadOnlyDictionary<string, HashSet<int>> Snapshot()
        {
            return _breakpoints.ToDictionary(kvp => kvp.Key, kvp => new HashSet<int>(kvp.Value));
        }

        internal void RaiseHit(string file, int line)
        {
            BreakpointHit?.Invoke(this, (file, line));
        }
    }

    public class MockSolidWorksExecutor
    {
        private readonly MockSolidWorksEnvironment _environment;
        private readonly BreakpointManager _breakpoints;

        public MockSolidWorksExecutor(MockSolidWorksEnvironment environment, BreakpointManager breakpoints)
        {
            _environment = environment;
            _breakpoints = breakpoints;
        }

        public async Task<ApiExecutionResult> ExecuteAddInAsync(string path, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ApiExecutionResult.Failed("ExecuteAddIn", "Path is empty");
            }

            return await _environment.ExecuteAsync("ExecuteAddIn", async _ =>
            {
                await Task.Delay(350, token);
                _environment.Logger.Log($"Loaded add-in from {path}");
                _environment.Logger.Log("Simulated OnConnect and UI registration");
            }, token);
        }

        public async Task<ApiExecutionResult> RunMacroAsync(string path, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ApiExecutionResult.Failed("RunMacro", "Path is empty");
            }

            return await ExecuteScriptAsync(path, "RunMacro", token);
        }

        public async Task<ApiExecutionResult> RunVbaAsync(string path, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ApiExecutionResult.Failed("RunVba", "Path is empty");
            }

            return await ExecuteScriptAsync(path, "RunVba", token);
        }

        private async Task<ApiExecutionResult> ExecuteScriptAsync(string path, string command, CancellationToken token)
        {
            if (!File.Exists(path))
            {
                return ApiExecutionResult.Failed(command, "File not found");
            }

            var lines = await File.ReadAllLinesAsync(path, token);
            return await _environment.ExecuteAsync(command, async ct =>
            {
                for (var index = 0; index < lines.Length; index++)
                {
                    ct.ThrowIfCancellationRequested();
                    var currentLine = index + 1;
                    await Task.Delay(50, ct);
                    if (_breakpoints.HasBreakpoint(path, currentLine))
                    {
                        _environment.Logger.Log($"Breakpoint hit at line {currentLine} in {Path.GetFileName(path)}");
                        _breakpoints.RaiseHit(path, currentLine);
                        await Task.Delay(200, ct);
                    }

                    _environment.Logger.Log($"[{command}] Executed line {currentLine}: {lines[index].Trim()}");
                }
            }, token);
        }
    }
}

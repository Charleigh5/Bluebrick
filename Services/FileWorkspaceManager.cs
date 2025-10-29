using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace BlueBrick.Services
{
    /// <summary>
    ///     Provides helper operations for the IDE sandbox workspace. This class keeps track of the
    ///     sandbox root directory, exposes high level file manipulation helpers and publishes
    ///     change notifications so the UI can refresh without directly depending on <see cref="FileSystemWatcher"/>.
    /// </summary>
    public class FileWorkspaceManager : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly BindingList<string> _pendingEvents = new BindingList<string>();
        private bool _disposed;

        public FileWorkspaceManager(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot))
            {
                throw new ArgumentException("Workspace root is required", nameof(workspaceRoot));
            }

            WorkspaceRoot = workspaceRoot;
            Directory.CreateDirectory(WorkspaceRoot);

            _watcher = new FileSystemWatcher(WorkspaceRoot)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            _watcher.Created += (_, e) => RegisterChange($"Created: {RelativePath(e.FullPath)}");
            _watcher.Changed += (_, e) => RegisterChange($"Changed: {RelativePath(e.FullPath)}");
            _watcher.Deleted += (_, e) => RegisterChange($"Deleted: {RelativePath(e.FullPath)}");
            _watcher.Renamed += (_, e) => RegisterChange($"Renamed: {RelativePath(e.OldFullPath)} -> {RelativePath(e.FullPath)}");
        }

        public string WorkspaceRoot { get; }

        /// <summary>
        ///     Gets an observable list of recent file system events. The IDE surface binds to this list
        ///     to provide live terminal style feedback for file operations.
        /// </summary>
        public BindingList<string> PendingEvents => _pendingEvents;

        public void SetSynchronizingObject(ISynchronizeInvoke? synchronizingObject)
        {
            if (_disposed)
            {
                return;
            }

            _watcher.SynchronizingObject = synchronizingObject;
        }

        public IEnumerable<string> EnumerateFiles()
        {
            if (!Directory.Exists(WorkspaceRoot))
            {
                yield break;
            }

            foreach (var directory in Directory.EnumerateDirectories(WorkspaceRoot, "*", SearchOption.AllDirectories))
            {
                yield return RelativePath(directory) + Path.DirectorySeparatorChar;
            }

            foreach (var file in Directory.EnumerateFiles(WorkspaceRoot, "*", SearchOption.AllDirectories))
            {
                yield return RelativePath(file);
            }
        }

        public string ReadFile(string relativePath)
        {
            var absolute = ToAbsolutePath(relativePath);
            return File.Exists(absolute) ? File.ReadAllText(absolute) : string.Empty;
        }

        public Task SaveFileAsync(string relativePath, string content)
        {
            var absolute = ToAbsolutePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? WorkspaceRoot);
            return File.WriteAllTextAsync(absolute, content ?? string.Empty);
        }

        public void CreateDirectory(string relativePath)
        {
            var absolute = ToAbsolutePath(relativePath);
            Directory.CreateDirectory(absolute);
        }

        public void CreateFile(string relativePath, string initialContent = "")
        {
            var absolute = ToAbsolutePath(relativePath);
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolute, initialContent ?? string.Empty);
        }

        public void Delete(string relativePath)
        {
            var absolute = ToAbsolutePath(relativePath);
            if (File.Exists(absolute))
            {
                File.Delete(absolute);
            }
            else if (Directory.Exists(absolute))
            {
                Directory.Delete(absolute, true);
            }
        }

        public void Rename(string currentRelativePath, string newName)
        {
            var absolute = ToAbsolutePath(currentRelativePath);
            if (File.Exists(absolute))
            {
                var newPath = Path.Combine(Path.GetDirectoryName(absolute) ?? WorkspaceRoot, newName);
                File.Move(absolute, newPath, true);
            }
            else if (Directory.Exists(absolute))
            {
                var newPath = Path.Combine(Path.GetDirectoryName(absolute.TrimEnd(Path.DirectorySeparatorChar)) ?? WorkspaceRoot, newName);
                Directory.Move(absolute, newPath);
            }
        }

        public string ToAbsolutePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return WorkspaceRoot;
            }

            return Path.GetFullPath(Path.Combine(WorkspaceRoot, relativePath));
        }

        public string RelativePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return string.Empty;
            }

            var uriWorkspace = new Uri(WorkspaceRoot.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? WorkspaceRoot
                : WorkspaceRoot + Path.DirectorySeparatorChar);
            var uriPath = new Uri(fullPath);
            var relative = uriWorkspace.MakeRelativeUri(uriPath).ToString();
            return Uri.UnescapeDataString(relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private void RegisterChange(string description)
        {
            if (_disposed)
            {
                return;
            }

            _pendingEvents.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {description}");
            while (_pendingEvents.Count > 200)
            {
                _pendingEvents.RemoveAt(_pendingEvents.Count - 1);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _watcher.Dispose();
            _pendingEvents.Clear();
            _disposed = true;
        }
    }
}

using System.Diagnostics;
using System.IO;

namespace GameShell.Desktop.Bridge;

internal static class ProfileFileSearch
{
    private static readonly object Sync = new();
    private static readonly SemaphoreSlim ScanGate = new(1);
    private static readonly Dictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private sealed class Entry
    {
        public string[] Files = [];
        public bool Scanning = true;
        public bool Limited;
        public DateTime Created = DateTime.UtcNow;
    }
    internal sealed record Result(string[] Paths, bool Scanning, bool Limited, int IndexedCount);
    internal static Result Search(string query, string? folder)
    {
        var key = string.IsNullOrWhiteSpace(folder) ? "" : Path.GetFullPath(folder);
        if (key.Length > 0 && !Directory.Exists(key)) throw new DirectoryNotFoundException("Search folder is unavailable.");
        lock (Sync)
        {
            if (!Cache.TryGetValue(key, out var entry) || (!entry.Scanning && DateTime.UtcNow - entry.Created > TimeSpan.FromMinutes(30)))
            {
                if (Cache.Count >= 4)
                {
                    var oldest = Cache.Where(pair => !pair.Value.Scanning).OrderBy(pair => pair.Value.Created).FirstOrDefault();
                    if (oldest.Key is not null) Cache.Remove(oldest.Key);
                    else return new([], true, true, 0);
                }
                entry = new Entry();
                Cache[key] = entry;
                var target = entry;
                _ = Task.Run(() => ScanAsync(key, target));
            }
            var paths = query.Trim().Length < 2 ? [] : entry.Files
                .Where(path => Path.GetFileName(path).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path).StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => path.Length).Take(40).ToArray();
            return new(paths, entry.Scanning, entry.Limited, entry.Files.Length);
        }
    }
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".sln", ".slnx", ".csproj", ".fsproj", ".vbproj", ".vcxproj", ".code-workspace",
        ".ipynb", ".py", ".cs", ".fs", ".cpp", ".c", ".h", ".js", ".ts", ".tsx", ".jsx", ".html",
        ".xlsx", ".xls", ".xlsm", ".xlsb", ".xltx", ".xltm", ".csv", ".ods",
        ".doc", ".docx", ".docm", ".odt", ".rtf", ".ppt", ".pptx", ".pptm", ".odp", ".pdf", ".txt", ".md"
    };
    internal static bool IsSearchableFile(string path) => AllowedExtensions.Contains(Path.GetExtension(path));

    private static IEnumerable<string> SearchRoots()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Useful locations first; the subsequent drive walk is deduplicated.
        yield return Path.Combine(user, "source");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(user, "Downloads");
        yield return Path.Combine(user, "AppData", "Local", "Programs");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        foreach (var drive in DriveInfo.GetDrives())
        {
            bool ready;
            try { ready = drive.IsReady; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            if (ready) yield return drive.RootDirectory.FullName;
        }
    }

    private static async Task ScanAsync(string folder, Entry entry)
    {
        await ScanGate.WaitAsync();
        var files = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slice = Stopwatch.StartNew();
        var publish = Stopwatch.StartNew();
        var limited = false;
        try
        {
            var skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".git", ".vs", ".svn", "node_modules", "obj", "AppData", "$RECYCLE.BIN",
                "Windows", "System Volume Information", "ProgramData", "Recovery", "$WinREAgent",
                "Temp", "tmp", "cache", "caches", ".cache", ".nuget", ".venv", "venv", "__pycache__"
            };
            var roots = folder.Length > 0 ? new[] { folder } : SearchRoots();
            var queue = new Queue<string>(roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase));
            var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System };
            while (queue.TryDequeue(out var directory))
            {
                if (!visited.Add(Path.GetFullPath(directory))) continue;
                if (files.Count >= 250000 || visited.Count >= 500000) { limited = true; break; }
                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*", options))
                    {
                        if (IsSearchableFile(file)) files.Add(file);
                        if (files.Count >= 250000) { limited = true; break; }
                        if (slice.ElapsedMilliseconds >= 40) { await Task.Delay(80); slice.Restart(); }
                    }
                    foreach (var child in Directory.EnumerateDirectories(directory, "*", options))
                    {
                        if (!skipped.Contains(Path.GetFileName(child))) queue.Enqueue(child);
                        if (queue.Count >= 500000) { limited = true; break; }
                        if (slice.ElapsedMilliseconds >= 40) { await Task.Delay(80); slice.Restart(); }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { limited = true; }
                if (publish.ElapsedMilliseconds >= 1000)
                {
                    lock (Sync) entry.Files = files.ToArray();
                    publish.Restart();
                }
                if (slice.ElapsedMilliseconds >= 40) { await Task.Delay(80); slice.Restart(); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { limited = true; }
        finally
        {
            lock (Sync)
            {
                entry.Files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                entry.Limited = limited;
                entry.Scanning = false;
                entry.Created = DateTime.UtcNow;
            }
            ScanGate.Release();
        }
    }
}

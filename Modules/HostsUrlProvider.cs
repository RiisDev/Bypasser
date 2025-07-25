namespace Bypasser.Modules;

public static class HostsUrlProvider
{
    private static string[] _cachedUrls = [];
    private static DateTime _lastLoaded = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";
    private static readonly FileSystemWatcher Watcher;
    private static readonly Lock Lock = new();

    static HostsUrlProvider()
    {
        LoadUrls();

        Watcher = new FileSystemWatcher(Path.GetDirectoryName(HostsFilePath)!, Path.GetFileName(HostsFilePath));
        Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
        Watcher.Changed += (_, _) =>
        {
            Thread.Sleep(100);
            LoadUrls();
        };
        Watcher.EnableRaisingEvents = true;
    }

    public static string[] GetUrls()
    {
        lock (Lock)
        {
            if (DateTime.Now - _lastLoaded > CacheDuration)
            {
                LoadUrls();
            }

            return _cachedUrls;
        }
    }

    private static void LoadUrls()
    {
        lock (Lock)
        {
            try
            {
                string[] lines = File.ReadAllLines(HostsFilePath);

                _cachedUrls = lines
                    .Where(line => !string.IsNullOrWhiteSpace(line) && line.Contains("# Bypasser"))
                    .Select(line =>
                    {
                        string[] parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        return parts.Length > 1 ? parts[1] : "";
                    })
                    .Where(x=> !string.IsNullOrEmpty(x))
                    .ToArray();
                
                _lastLoaded = DateTime.Now;
            }
            catch
            {
                _cachedUrls = [];
                _lastLoaded = DateTime.Now;
            }
        }
    }
}
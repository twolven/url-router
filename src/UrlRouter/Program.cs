using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

return Router.Run(args);

internal static class Router
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: UrlRouter.exe [--test] <http-or-https-url>");
            return 2;
        }

        var testOnly = args.Length == 2 && args[0].Equals("--test", StringComparison.OrdinalIgnoreCase);
        var rawUrl = testOnly ? args[1] : args[0];

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            LogFailure($"Rejected invalid or unsupported URL: {Redact(rawUrl)}");
            return 2;
        }

        try
        {
            var configPath = Environment.GetEnvironmentVariable("URLROUTER_CONFIG");
            if (string.IsNullOrWhiteSpace(configPath))
                configPath = Path.Combine(AppContext.BaseDirectory, "rules.json");

            if (!File.Exists(configPath))
                throw new FileNotFoundException("Configuration not found. Create rules.json beside UrlRouter.exe.", configPath);

            var config = JsonSerializer.Deserialize<RouterConfig>(File.ReadAllText(configPath), JsonOptions)
                ?? throw new InvalidOperationException("rules.json is empty.");

            var browserName = config.Rules.FirstOrDefault(rule => rule.Matches(uri))?.Browser
                ?? config.DefaultBrowser;

            if (string.IsNullOrWhiteSpace(browserName))
                throw new InvalidOperationException("defaultBrowser is required.");

            if (!config.Browsers.TryGetValue(browserName, out var browser))
                throw new InvalidOperationException($"Browser '{browserName}' is not defined.");

            var executable = BrowserResolver.Resolve(browser.Executable);

            if (testOnly)
            {
                Console.WriteLine($"{browserName}: {executable} {string.Join(' ', browser.Arguments)}".TrimEnd());
                return 0;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false
            };

            foreach (var argument in browser.Arguments)
                startInfo.ArgumentList.Add(argument);
            startInfo.ArgumentList.Add(uri.AbsoluteUri);

            Process.Start(startInfo);
            return 0;
        }
        catch (Exception exception)
        {
            LogFailure(exception.ToString());
            return 1;
        }
    }

    private static void LogFailure(string message)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UrlRouter"
            );
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "errors.log"),
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"
            );
        }
        catch
        {
            // A routing failure must never trigger another application or network request.
        }
    }

    private static string Redact(string value) => value.Length <= 120 ? value : value[..120] + "...";
}

internal static class BrowserResolver
{
    private static readonly Dictionary<string, BrowserDefinition> KnownBrowsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = new("chrome.exe", [
            @"%PROGRAMFILES%\Google\Chrome\Application\chrome.exe",
            @"%PROGRAMFILES(X86)%\Google\Chrome\Application\chrome.exe",
            @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"
        ]),
        ["brave"] = new("brave.exe", [
            @"%PROGRAMFILES%\BraveSoftware\Brave-Browser\Application\brave.exe",
            @"%PROGRAMFILES(X86)%\BraveSoftware\Brave-Browser\Application\brave.exe",
            @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\Application\brave.exe"
        ]),
        ["edge"] = new("msedge.exe", [
            @"%PROGRAMFILES(X86)%\Microsoft\Edge\Application\msedge.exe",
            @"%PROGRAMFILES%\Microsoft\Edge\Application\msedge.exe"
        ]),
        ["firefox"] = new("firefox.exe", [
            @"%PROGRAMFILES%\Mozilla Firefox\firefox.exe",
            @"%PROGRAMFILES(X86)%\Mozilla Firefox\firefox.exe"
        ])
    };

    public static string Resolve(string configuredExecutable)
    {
        if (string.IsNullOrWhiteSpace(configuredExecutable))
            throw new InvalidOperationException("A browser executable is required.");

        if (configuredExecutable.StartsWith("auto:", StringComparison.OrdinalIgnoreCase))
        {
            var alias = configuredExecutable[5..];
            if (!KnownBrowsers.TryGetValue(alias, out var browser))
                throw new InvalidOperationException($"Unknown browser discovery alias '{configuredExecutable}'.");

            return FindKnownBrowser(alias, browser);
        }

        var expanded = Environment.ExpandEnvironmentVariables(configuredExecutable);
        if (!Path.IsPathFullyQualified(expanded))
            throw new InvalidOperationException("Browser executable paths must be absolute or use an auto: alias.");
        if (!File.Exists(expanded))
            throw new FileNotFoundException($"Browser executable not found: {expanded}", expanded);
        return expanded;
    }

    private static string FindKnownBrowser(string alias, BrowserDefinition browser)
    {
        var registryPath = FindInAppPaths(browser.ExecutableName);
        if (registryPath is not null)
            return registryPath;

        foreach (var candidate in browser.Candidates)
        {
            var expanded = Environment.ExpandEnvironmentVariables(candidate);
            if (File.Exists(expanded))
                return expanded;
        }

        throw new FileNotFoundException($"Could not discover '{alias}'. Set an absolute executable path in rules.json.");
    }

    private static string? FindInAppPaths(string executableName)
    {
        const string appPaths = @"Software\Microsoft\Windows\CurrentVersion\App Paths";
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey($@"{appPaths}\{executableName}");
                    var value = key?.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                        return value;
                }
                catch
                {
                    // Continue through the remaining registry hives and views.
                }
            }
        }

        return null;
    }

    private sealed record BrowserDefinition(string ExecutableName, string[] Candidates);
}

internal sealed class RouterConfig
{
    public string DefaultBrowser { get; init; } = "";
    public Dictionary<string, BrowserConfig> Browsers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<RouteRule> Rules { get; init; } = [];
}

internal sealed class BrowserConfig
{
    public string Executable { get; init; } = "";
    public List<string> Arguments { get; init; } = [];
}

internal sealed class RouteRule
{
    public string Scheme { get; init; } = "https";
    public string Host { get; init; } = "";
    public string PathPrefix { get; init; } = "/";
    public string Browser { get; init; } = "";

    public bool Matches(Uri uri)
    {
        if (!uri.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase) ||
            !uri.IdnHost.Equals(Host, StringComparison.OrdinalIgnoreCase))
            return false;

        if (PathPrefix == "/")
            return true;

        var normalizedPrefix = "/" + PathPrefix.Trim('/');
        return uri.AbsolutePath.Equals(normalizedPrefix, StringComparison.OrdinalIgnoreCase) ||
               uri.AbsolutePath.StartsWith(normalizedPrefix + "/", StringComparison.OrdinalIgnoreCase);
    }
}

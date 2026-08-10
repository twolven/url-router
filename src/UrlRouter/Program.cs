using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

return Router.Run(args);

internal static class Router
{
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

            var config = JsonSerializer.Deserialize(File.ReadAllText(configPath), RouterJsonContext.Default.RouterConfig)
                ?? throw new InvalidOperationException("rules.json is empty.");

            var routingUri = SafeLinkUnwrapper.GetRoutingUri(uri);
            var browserName = config.Rules.FirstOrDefault(rule => rule.Matches(routingUri))?.Browser
                ?? config.DefaultBrowser;

            if (string.IsNullOrWhiteSpace(browserName))
                throw new InvalidOperationException("defaultBrowser is required.");

            if (!config.Browsers.TryGetValue(browserName, out var browser))
                throw new InvalidOperationException($"Browser '{browserName}' is not defined.");

            var executable = BrowserResolver.Resolve(browser.Executable);

            if (testOnly)
            {
                var decision = $"{browserName}: {executable} {string.Join(' ', browser.Arguments)}".TrimEnd();
                NativeConsole.WriteLine(decision);
                WriteTestOutput(decision);
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

    private static void WriteTestOutput(string decision)
    {
        var path = Environment.GetEnvironmentVariable("URLROUTER_TEST_OUTPUT");
        if (!string.IsNullOrWhiteSpace(path))
            File.WriteAllText(path, decision);
    }
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

internal static class SafeLinkUnwrapper
{
    public static Uri GetRoutingUri(Uri outerUri)
    {
        if (!IsKnownWrapper(outerUri))
            return outerUri;

        foreach (var pair in outerUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
                continue;

            var key = Uri.UnescapeDataString(pair[..separator].Replace('+', ' '));
            if (!key.Equals("url", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            if (Uri.TryCreate(value, UriKind.Absolute, out var target) &&
                (target.Scheme == Uri.UriSchemeHttp || target.Scheme == Uri.UriSchemeHttps))
                return target;
        }

        return outerUri;
    }

    private static bool IsKnownWrapper(Uri uri)
    {
        if (uri.IdnHost.Equals("teams.public.onecdn.static.microsoft", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Equals("/evergreen-assets/safelinks/2/atp-safelinks.html", StringComparison.OrdinalIgnoreCase))
            return true;

        return uri.IdnHost.Equals("safelinks.protection.outlook.com", StringComparison.OrdinalIgnoreCase) ||
               uri.IdnHost.EndsWith(".safelinks.protection.outlook.com", StringComparison.OrdinalIgnoreCase);
    }
}

internal static partial class NativeConsole
{
    private const int StandardOutputHandle = -11;

    public static void WriteLine(string value)
    {
        AttachConsole(unchecked((uint)-1));
        var bytes = Encoding.UTF8.GetBytes(value + Environment.NewLine);
        var handle = GetStdHandle(StandardOutputHandle);
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            WriteFile(handle, bytes, bytes.Length, out _, IntPtr.Zero);
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetStdHandle(int standardHandle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteFile(IntPtr file, byte[] buffer, int bytesToWrite, out int bytesWritten, IntPtr overlapped);
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

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RouterConfig))]
internal partial class RouterJsonContext : JsonSerializerContext;

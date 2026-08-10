# URL Router

URL Router is a small, local-only Windows protocol handler that opens different links in different browsers. It runs only when another application opens an HTTP or HTTPS link, launches the configured browser, and exits immediately.

- No service, startup entry, scheduled task, telemetry, or network requests
- Route by exact scheme, hostname, and path-segment prefix
- Use any number of browsers and browser profiles
- Automatically discover Chrome, Brave, Edge, and Firefox
- Use arbitrary browser executables and command-line arguments
- Keep unmatched links in a configurable fallback browser
- Install per-user without administrator privileges

## Install

Download `UrlRouter-Setup-x64.exe` from the latest GitHub release. The installer opens `rules.json` for editing and then opens Windows Default Apps. Assign both **HTTP** and **HTTPS** to **URL Router**.

The installer is currently unsigned, so Windows SmartScreen may show an unknown-publisher warning. Release checksums are published alongside each installer.

## Configuration

The installed configuration is `%LOCALAPPDATA%\Programs\UrlRouter\rules.json`. An annotated starting point is provided in [`config/rules.example.json`](config/rules.example.json).

```json
{
  "defaultBrowser": "edge",
  "browsers": {
    "edge": { "executable": "auto:edge", "arguments": [] },
    "brave": { "executable": "auto:brave", "arguments": [] },
    "chrome-work": {
      "executable": "auto:chrome",
      "arguments": ["--profile-directory=Profile 1"]
    },
    "firefox": { "executable": "auto:firefox", "arguments": [] }
  },
  "rules": [
    {
      "scheme": "https",
      "host": "github.com",
      "pathPrefix": "/example-org",
      "browser": "chrome-work"
    },
    {
      "scheme": "https",
      "host": "portal.example.com",
      "pathPrefix": "/",
      "browser": "firefox"
    }
  ]
}
```

`browsers` is a named dictionary, so it can contain as many entries as needed. A route's `browser` value selects one entry. Use different names with different arguments to target multiple profiles in the same browser.

Built-in discovery aliases are `auto:chrome`, `auto:brave`, `auto:edge`, and `auto:firefox`. For any other browser, set `executable` to an absolute path. Environment variables such as `%LOCALAPPDATA%` are expanded.

Matching is structural rather than a raw string prefix. Hostnames must match exactly, and `/example-org` matches that path segment and descendants but not `/example-organization`. Lookalike domains such as `github.com.evil.example` do not match `github.com`.

## Test a rule

```powershell
UrlRouter.exe --test "https://github.com/example-org/project"
```

This prints the selected browser without opening it. Set `URLROUTER_CONFIG` to test another configuration file.

## Build

Requires the .NET 9 SDK:

```powershell
dotnet publish src\UrlRouter\UrlRouter.csproj -c Release -r win-x64 --self-contained true -o publish
```

The installer is built with [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
iscc installer\UrlRouter.iss
```

## Privacy and security

URL Router parses URLs locally and never contacts a server. Successful URLs are not logged. Only startup, configuration, and launch failures are written to `%LOCALAPPDATA%\UrlRouter\errors.log`.

Browser processes are launched with .NET's argument-list API rather than concatenated shell commands. Rules compare parsed URI components to prevent hostname-prefix and user-info lookalikes.

## License

[MIT](LICENSE). Copyright © 2026 [twolven](https://github.com/twolven). Redistributions must retain the copyright and license notice.

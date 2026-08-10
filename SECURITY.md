# Security Policy

Please report vulnerabilities privately through GitHub's **Report a vulnerability** feature rather than a public issue.

URL Router receives URLs from other applications and launches configured browser executables. Security-sensitive changes should preserve these properties:

- Parse URLs as URIs before matching.
- Match hostnames exactly and case-insensitively.
- Preserve path-segment boundaries.
- Pass browser arguments through a structured argument list.
- Never resolve redirects, download favicons, or send telemetry.
- Decode embedded targets only for explicitly allowlisted Safe Links wrapper hosts.
- Never log successfully routed URLs.

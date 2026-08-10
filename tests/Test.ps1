$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$router = Join-Path $projectRoot 'publish\UrlRouter.exe'
$config = Join-Path $projectRoot 'config\rules.example.json'
$env:URLROUTER_CONFIG = $config

$tests = @(
    @{ Url = 'https://github.com/example-org/project/issues/1'; Expected = 'chrome-work' }
    @{ Url = 'https://github.com/example-org'; Expected = 'chrome-work' }
    @{ Url = 'https://github.com/example-organization/project'; Expected = 'edge' }
    @{ Url = 'https://github.com.evil.example/example-org/project'; Expected = 'edge' }
    @{ Url = 'https://portal.example.com/path'; Expected = 'firefox' }
    @{ Url = 'https://example.com/path'; Expected = 'edge' }
    @{ Url = 'http://portal.example.com/path'; Expected = 'edge' }
)

$failed = $false
try {
    foreach ($test in $tests) {
        $decision = & $router --test $test.Url
        $actual = ($decision -split ':', 2)[0]
        $passed = $LASTEXITCODE -eq 0 -and $actual -eq $test.Expected
        if (-not $passed) {
            $failed = $true
        }

        [pscustomobject]@{
            Passed = $passed
            Expected = $test.Expected
            Actual = $actual
            Url = $test.Url
        }
    }
}
finally {
    Remove-Item Env:\URLROUTER_CONFIG -ErrorAction SilentlyContinue
}

if ($failed) {
    exit 1
}

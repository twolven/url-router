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
    @{ Url = 'https://teams.public.onecdn.static.microsoft/evergreen-assets/safelinks/2/atp-safelinks.html?url=https%3A%2F%2Fgithub.com%2Fexample-org%2Fproject'; Expected = 'chrome-work' }
    @{ Url = 'https://nam10.safelinks.protection.outlook.com/?url=https%3A%2F%2Fportal.example.com%2Fpath'; Expected = 'firefox' }
    @{ Url = 'https://example.com/path'; Expected = 'edge' }
    @{ Url = 'http://portal.example.com/path'; Expected = 'edge' }
)

$failed = $false
$testOutput = Join-Path $env:TEMP "urlrouter-test-$PID.txt"
$env:URLROUTER_TEST_OUTPUT = $testOutput
try {
    foreach ($test in $tests) {
        $process = Start-Process -FilePath $router -ArgumentList @('--test', $test.Url) -Wait -PassThru
        $decision = Get-Content -LiteralPath $testOutput -Raw
        $actual = ($decision -split ':', 2)[0]
        $passed = $process.ExitCode -eq 0 -and $actual -eq $test.Expected
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
    Remove-Item Env:\URLROUTER_TEST_OUTPUT -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testOutput -Force -ErrorAction SilentlyContinue
}

if ($failed) {
    exit 1
}

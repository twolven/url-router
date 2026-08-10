param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\UrlRouter')
)

$ErrorActionPreference = 'Stop'

$router = Join-Path $InstallDirectory 'UrlRouter.exe'
$testOutput = Join-Path $env:TEMP "urlrouter-installed-test-$PID.txt"
$wrappedUrl = 'https://teams.public.onecdn.static.microsoft/evergreen-assets/safelinks/2/atp-safelinks.html?url=https%3A%2F%2Fgithub.com%2Fexample-org%2Fproject'
$env:URLROUTER_TEST_OUTPUT = $testOutput

try {
    $process = Start-Process -FilePath $router -ArgumentList @('--test', $wrappedUrl) -Wait -PassThru
    $decision = Get-Content -LiteralPath $testOutput -Raw
    if ($process.ExitCode -ne 0 -or -not $decision.StartsWith('chrome-work:')) {
        throw "Installed router test failed: $decision"
    }

    Write-Host "Installed router test passed: $decision"
}
finally {
    Remove-Item Env:\URLROUTER_TEST_OUTPUT -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testOutput -Force -ErrorAction SilentlyContinue
}

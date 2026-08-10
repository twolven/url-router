$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src\UrlRouter\UrlRouter.csproj'
$publish = Join-Path $projectRoot 'publish'

dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $projectRoot 'tests\Test.ps1')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

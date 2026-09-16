$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$packageRoot = Join-Path $projectRoot ('artifacts/azure-package-' + [guid]::NewGuid().ToString('N'))
$publishPath = Join-Path $packageRoot 'publish'

Push-Location (Join-Path $projectRoot 'frontend')
try {
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
} finally { Pop-Location }

& dotnet publish (Join-Path $projectRoot 'backend/PulseLink.Api/PulseLink.Api.csproj') -c Release -o $publishPath
if ($LASTEXITCODE -ne 0) { throw 'API publish failed.' }
Copy-Item -Path (Join-Path $projectRoot 'frontend/dist') -Destination (Join-Path $publishPath 'wwwroot') -Recurse
$zipPath = Join-Path $packageRoot 'pulselink.zip'
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath
Write-Output "Deployment package: $zipPath"

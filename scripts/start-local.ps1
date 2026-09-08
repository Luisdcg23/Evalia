[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $projectRoot ".env"
$runDirectory = Join-Path $projectRoot ".run"

if (-not (Test-Path -LiteralPath $envFile)) {
    throw "Falta .env. Copie .env.example a .env y configure las credenciales locales."
}

Get-Content -LiteralPath $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#")) {
        $name, $value = $line -split "=", 2
        if ($name -and $null -ne $value) {
            [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), "Process")
        }
    }
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$minioExecutable = Join-Path $projectRoot "storage\minio\minio.exe"
$minioData = Join-Path $projectRoot "storage\data"
if (Test-Path -LiteralPath $minioExecutable) {
    New-Item -ItemType Directory -Force -Path $minioData | Out-Null
    $minio = Start-Process -FilePath $minioExecutable -ArgumentList @("server", $minioData, "--console-address", ":9001") -WorkingDirectory (Split-Path $minioExecutable) -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath (Join-Path $runDirectory "minio.pid") -Value $minio.Id
}

$api = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "backend\src\EBR.Api\EBR.Api.csproj") -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
Set-Content -LiteralPath (Join-Path $runDirectory "api.pid") -Value $api.Id

$frontend = Start-Process -FilePath "pnpm.cmd" -ArgumentList @("--dir", "frontend", "dev") -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
Set-Content -LiteralPath (Join-Path $runDirectory "frontend.pid") -Value $frontend.Id

Write-Host "API: http://localhost:5080" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:5173" -ForegroundColor Cyan
Write-Host "MinIO: http://localhost:9001" -ForegroundColor Cyan

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$minioDirectory = Join-Path $projectRoot "storage\minio"
$minioExecutable = Join-Path $minioDirectory "minio.exe"
$downloadUrl = "https://dl.min.io/server/minio/release/windows-amd64/minio.exe"

New-Item -ItemType Directory -Force -Path $minioDirectory | Out-Null

if (-not (Test-Path -LiteralPath $minioExecutable)) {
    Write-Host "Descargando MinIO desde el sitio oficial..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $downloadUrl -OutFile $minioExecutable
}

$version = & $minioExecutable --version
if ($LASTEXITCODE -ne 0 -or -not (($version -join "`n") -match "minio(?:\.exe)? version")) {
    throw "El ejecutable descargado no superó la comprobación de versión."
}

$hash = Get-FileHash -LiteralPath $minioExecutable -Algorithm SHA256
Write-Host "MinIO instalado: $($version[0])" -ForegroundColor Green
Write-Host "SHA-256: $($hash.Hash)" -ForegroundColor DarkGray

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$requirements = @(
    @{ Name = ".NET SDK 9"; Command = "dotnet"; Arguments = @("--list-sdks"); Match = "^9\.0\." },
    @{ Name = "Node.js"; Command = "node"; Arguments = @("--version"); Match = "^v(20|22|24)\." },
    @{ Name = "pnpm"; Command = "pnpm"; Arguments = @("--version"); Match = "^\d+\." }
)

$failed = $false
foreach ($requirement in $requirements) {
    try {
        $output = & $requirement.Command @($requirement.Arguments) 2>&1
        if (($output -join "`n") -match $requirement.Match) {
            Write-Host "[OK] $($requirement.Name)" -ForegroundColor Green
        }
        else {
            Write-Host "[FALTA] $($requirement.Name)" -ForegroundColor Red
            $failed = $true
        }
    }
    catch {
        Write-Host "[FALTA] $($requirement.Name)" -ForegroundColor Red
        $failed = $true
    }
}

$psqlCommand = Get-Command "psql" -ErrorAction SilentlyContinue
$postgresInstallRoot = Join-Path $env:ProgramFiles "PostgreSQL"
$installedPsql = Get-ChildItem -Path $postgresInstallRoot -Filter "psql.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]18[\\/]bin[\\/]psql\.exe$' } |
    Select-Object -First 1
$postgresService = Get-Service -Name "postgresql*18*" -ErrorAction SilentlyContinue |
    Where-Object { $_.Status -eq "Running" } |
    Select-Object -First 1
if (($psqlCommand -or $installedPsql) -and $postgresService) {
    Write-Host "[OK] PostgreSQL 18" -ForegroundColor Green
}
else {
    Write-Host "[FALTA] PostgreSQL 18 en ejecución" -ForegroundColor Red
    $failed = $true
}

$minioExecutable = Join-Path $projectRoot "storage\minio\minio.exe"
if (Test-Path -LiteralPath $minioExecutable) {
    Write-Host "[OK] MinIO local" -ForegroundColor Green
}
else {
    Write-Host "[PENDIENTE] MinIO local: ejecute scripts\install-minio.ps1" -ForegroundColor Yellow
}

if ($failed) {
    exit 1
}

Write-Host "Requisitos principales disponibles." -ForegroundColor Cyan

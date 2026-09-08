[CmdletBinding()]
param()

$projectRoot = Split-Path -Parent $PSScriptRoot
$runDirectory = Join-Path $projectRoot ".run"

foreach ($name in @("frontend", "api", "minio")) {
    $pidFile = Join-Path $runDirectory "$name.pid"
    if (Test-Path -LiteralPath $pidFile) {
        $processId = [int](Get-Content -LiteralPath $pidFile -Raw)
        Stop-Process -Id $processId -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $pidFile -Force
        Write-Host "Detenido: $name"
    }
}

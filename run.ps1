$exePath = Join-Path $PSScriptRoot "ScriptHub.exe"
if (Test-Path $exePath) {
    Start-Process -FilePath $exePath
} else {
    $dotnet = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") { "$env:USERPROFILE\.dotnet\dotnet.exe" } else { "dotnet" }
    Write-Host "[Script Hub] Zapusk cherez dotnet..." -ForegroundColor Cyan
    & $dotnet run --project (Join-Path $PSScriptRoot "ScriptHub\ScriptHub.csproj")
}
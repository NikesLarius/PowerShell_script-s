$dotnet = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") { "$env:USERPROFILE\.dotnet\dotnet.exe" } else { "dotnet" }

Write-Host "[Script Hub] Sborka ispolnyaemogo fayla ScriptHub.exe..." -ForegroundColor Cyan
& $dotnet publish (Join-Path $PSScriptRoot "ScriptHub\ScriptHub.csproj") -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $PSScriptRoot

Write-Host "[Script Hub] Sborka avtonomnogo ScriptHub.exe v publish/..." -ForegroundColor Cyan
& $dotnet publish (Join-Path $PSScriptRoot "ScriptHub\ScriptHub.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o (Join-Path $PSScriptRoot "publish")

Write-Host "`n[OK] Gotovo! Fayl sozdan: ScriptHub.exe" -ForegroundColor Green
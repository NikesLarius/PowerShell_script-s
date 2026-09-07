@echo off
setlocal
if exist "%~dp0ScriptHub.exe" (
    start "" "%~dp0ScriptHub.exe"
    exit /b 0
)
set "DOTNET_PATH=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%DOTNET_PATH%" (
    set "DOTNET_PATH=dotnet"
)
echo [Script Hub] Zapusk prilozheniya...
"%DOTNET_PATH%" run --project "%~dp0ScriptHub\ScriptHub.csproj"

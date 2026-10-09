# Auto elevation
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Admin privileges required. Elevating..."
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

# 1. Recycle Bin
Write-Info "Cleaning Recycle Bin..."
try {
    Clear-RecycleBin -Force -ErrorAction SilentlyContinue
    Write-Success "Recycle Bin cleared."
} catch {
    Write-Host "Recycle Bin error: $_" -ForegroundColor Yellow
}

# 2. User Temp
Write-Info "Cleaning User Temp ($env:TEMP)..."
Get-ChildItem -Path $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue | 
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Success "User Temp cleared."

# 3. System Temp
Write-Info "Cleaning System Temp (C:\Windows\Temp)..."
Get-ChildItem -Path "$env:windir\Temp" -Recurse -Force -ErrorAction SilentlyContinue | 
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Success "System Temp cleared."

# 4. DNS Cache
Write-Info "Flushing DNS Cache..."
Clear-DnsClientCache
Write-Success "DNS Cache flushed."

# 5. Explorer Thumbnails
Write-Info "Cleaning Thumbnail Cache..."
$thumbCache = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"
if (Test-Path $thumbCache) {
    Get-ChildItem -Path $thumbCache -Filter "thumbcache_*.db" -Force -ErrorAction SilentlyContinue | 
        Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Success "Thumbnail Cache cleared."
}

Write-Host "`nCleanup completed successfully!" -ForegroundColor Green
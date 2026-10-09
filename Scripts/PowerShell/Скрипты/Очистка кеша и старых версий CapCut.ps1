<#
.SYNOPSIS
    Универсальный скрипт глубокой очистки CapCut (удаление старых версий и кэша).
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "           ОБСЛУЖИВАНИЕ И ОЧИСТКА CAPCUT          " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$capcutRoot = "$env:LOCALAPPDATA\CapCut"
$appsPath   = "$capcutRoot\Apps"
$cachePath  = "$capcutRoot\User Data\Cache"
$logPath    = "$capcutRoot\User Data\Log"
$crashPath  = "$capcutRoot\User Data\Crash"

if (-not (Test-Path $capcutRoot)) {
    Write-Host "[!] CapCut не найден в директории текущего пользователя ($capcutRoot)." -ForegroundColor Red
    return
}

# 1. Завершение процессов CapCut
$runningProc = Get-Process -Name "CapCut" -ErrorAction SilentlyContinue
if ($runningProc) {
    Write-Host "[*] Завершение запущенных процессов CapCut..." -ForegroundColor Yellow
    Stop-Process -Name "CapCut" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Функция подсчета размера директории
function Get-DirSizeGB ($path) {
    if (-not (Test-Path $path)) { return 0 }
    $measure = Get-ChildItem -Path $path -Recurse -File -Force -ErrorAction SilentlyContinue | 
               Measure-Object -Property Length -Sum
    if ($measure.Sum) {
        return [math]::Round($measure.Sum / 1GB, 2)
    }
    return 0
}

$initialTotalSize = Get-DirSizeGB $capcutRoot
Write-Host "`n[i] Текущий общий размер папки CapCut: $initialTotalSize ГБ" -ForegroundColor White

# 2. Анализ и очистка старых версий в Apps
Write-Host "`n--- [1/2] Анализ версий CapCut ---" -ForegroundColor Cyan

if (Test-Path $appsPath) {
    # Сортируем версии корректно как объекты Version (а не просто как строки)
    $versionDirs = Get-ChildItem -Path $appsPath -Directory | ForEach-Object {
        $parsedVer = $null
        if ([System.Version]::TryParse($_.Name, [ref]$parsedVer)) {
            [PSCustomObject]@{
                Folder  = $_
                Name    = $_.Name
                Version = $parsedVer
                SizeGB  = Get-DirSizeGB $_.FullName
            }
        } else {
            [PSCustomObject]@{
                Folder  = $_
                Name    = $_.Name
                Version = [System.Version]"0.0.0.0"
                SizeGB  = Get-DirSizeGB $_.FullName
            }
        }
    } | Sort-Object Version

    if ($versionDirs.Count -gt 0) {
        Write-Host "Найденные версии:" -ForegroundColor Gray
        $versionDirs | Format-Table -Property Name, SizeGB -AutoSize

        if ($versionDirs.Count -gt 1) {
            $latestVersion = $versionDirs[-1]
            $oldVersions   = $versionDirs[0..($versionDirs.Count - 2)]

            Write-Host "[+] Будет сохранена актуальная версия: $($latestVersion.Name) ($($latestVersion.SizeGB) ГБ)" -ForegroundColor Green
            
            foreach ($old in $oldVersions) {
                Write-Host "[-] Удаление старой версии: $($old.Name) ($($old.SizeGB) ГБ)..." -ForegroundColor Yellow
                Remove-Item -Path $old.Folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
        } else {
            Write-Host "[✓] Найдена только одна версия ($($versionDirs[0].Name)). Лишних дубликатов нет." -ForegroundColor Green
        }
    } else {
        Write-Host "[!] Папка Apps пуста." -ForegroundColor Gray
    }
} else {
    Write-Host "[!] Папка Apps отсутствует." -ForegroundColor Gray
}

# 3. Очистка временного кэша превью, логов и дампов падений
Write-Host "`n--- [2/2] Очистка кэша и логов ---" -ForegroundColor Cyan

$tempTargets = @(
    @{ Name = "Кэш превью (Cache)"; Path = $cachePath },
    @{ Name = "Логи (Log)";         Path = $logPath },
    @{ Name = "Дампы падений (Crash)"; Path = $crashPath }
)

foreach ($target in $tempTargets) {
    if (Test-Path $target.Path) {
        $size = Get-DirSizeGB $target.Path
        if ($size -gt 0) {
            Write-Host "[-] Очищаю $($target.Name): $size ГБ..." -ForegroundColor Yellow
            Remove-Item -Path "$($target.Path)\*" -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Write-Host "[✓] $($target.Name) уже пуст." -ForegroundColor Gray
        }
    }
}

# 4. Итоговый отчёт
$finalTotalSize = Get-DirSizeGB $capcutRoot
$freedSpace = [math]::Round($initialTotalSize - $finalTotalSize, 2)
if ($freedSpace -lt 0) { $freedSpace = 0 }

Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host "                ИТОГИ ОЧИСТКИ                     " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Размер CapCut ДО:      $initialTotalSize ГБ" -ForegroundColor White
Write-Host "Размер CapCut ПОСЛЕ:   $finalTotalSize ГБ" -ForegroundColor White
Write-Host "УСПЕШНО ОСВОБОЖДЕНО:   $freedSpace ГБ" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan
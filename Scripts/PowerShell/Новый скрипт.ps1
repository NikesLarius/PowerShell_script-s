# Требуются права администратора
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Запустите PowerShell от имени Администратора!"
    Break
}

# 1. Сбор установленных программ из реестра
$RegPaths = @(
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$InstalledApps = Get-ItemProperty $RegPaths -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -and ($_.UninstallString -or $_.QuietUninstallString) } |
    Select-Object DisplayName, DisplayVersion, Publisher, UninstallString, QuietUninstallString, InstallLocation |
    Sort-Object DisplayName -Unique

if (-not $InstalledApps) {
    Write-Host "Программы не найдены." -ForegroundColor Yellow
    Exit
}

# 2. Выбор программы через интерактивную таблицу
$SelectedApp = $InstalledApps | Out-GridView -Title "Выберите программу для удаления и нажмите ОК" -OutputMode Single

if (-not $SelectedApp) {
    Write-Host "Отменено пользователем." -ForegroundColor Gray
    Exit
}

$AppName = $SelectedApp.DisplayName
Write-Host "`nВыбрано для удаления: $AppName" -ForegroundColor Cyan

# 3. Штатная деинсталляция
$ConfirmUninstall = Read-Host "Запустить штатный деинсталлятор? (Y/N)"
if ($ConfirmUninstall -match '^[YyДд]') {
    $UninstallCmd = if ($SelectedApp.QuietUninstallString) { $SelectedApp.QuietUninstallString } else { $SelectedApp.UninstallString }
    
    # Обработка MSI и обычных EXE
    if ($UninstallCmd -match 'msiexec(\.exe)?\s+([\/IxiIX]+)\s*(\{[\w-]+\})') {
        $Guid = $Matches[3]
        Write-Host "Запуск msiexec для $Guid..." -ForegroundColor Yellow
        Start-Process "msiexec.exe" -ArgumentList "/x $Guid" -Wait
    } else {
        # Разделение исполняемого файла и аргументов
        if ($UninstallCmd -match '^"([^"]+)"\s*(.*)$') {
            $Exe = $Matches[1]
            $Args = $Matches[2]
        } elseif ($UninstallCmd -match '^([^\s]+)\s*(.*)$') {
            $Exe = $Matches[1]
            $Args = $Matches[2]
        } else {
            $Exe = $UninstallCmd
            $Args = ""
        }
        Write-Host "Запуск $Exe $Args..." -ForegroundColor Yellow
        Start-Process $Exe -ArgumentList $Args -Wait
    }
}

# 4. Поиск оставшихся «хвостов» (кэш, логи, конфиги)
Write-Host "`nСканирование файловой системы на наличие остаточных файлов..." -ForegroundColor Cyan

# Формируем ключевые слова для поиска (название приложения и папка установки)
$SearchTokens = @()
$CleanName = ($AppName -replace '[^\w\s]', '').Trim()
$SearchTokens += $CleanName
# Берем первое значимое слово, если название длинное
$FirstWord = ($CleanName -split '\s+')[0]
if ($FirstWord.Length -ge 4) { $SearchTokens += $FirstWord }

# Директории, где скапливается мусор
$UserDirs = @(
    $env:APPDATA,                                 # Roaming (профили, конфиги)
    $env:LOCALAPPDATA,                            # Local (кэш, базы данных)
    (Join-Path $env:LOCALAPPDATA "Temp"),         # Temp пользователя
    (Join-Path $env:LOCALAPPDATA "CrashDumps"),   # Логи падений
    $env:ProgramData,                             # Общие конфиги всех пользователей
    "C:\Program Files",
    "C:\Program Files (x86)"
) | Where-Object { Test-Path $_ }

$LeftoverPaths = [System.Collections.Generic.List[PSObject]]::new()

foreach ($Dir in $UserDirs) {
    foreach ($Token in ($SearchTokens | Select-Object -Unique)) {
        Get-ChildItem -Path $Dir -Filter "*$Token*" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            # Пропускаем критические системные папки Windows
            if ($_.FullName -notmatch 'Windows|Microsoft|System32|WinSxS') {
                $LeftoverPaths.Add($_)
            }
        }
    }
}

# Исключаем дубликаты путей
$UniqueLeftovers = $LeftoverPaths | Sort-Object FullName -Unique

# 5. Вывод и подтверждение очистки
if ($UniqueLeftovers.Count -gt 0) {
    Write-Host "`nНайдены следующие остаточные папки:" -ForegroundColor Yellow
    $UniqueLeftovers | ForEach-Object { Write-Host "  [?] $($_.FullName)" -ForegroundColor White }
    
    # Даем возможность пользователю выбрать, какие именно папки стереть
    $ToKill = $UniqueLeftovers | Out-GridView -Title "Выберите папки ДЛЯ УДАЛЕНИЯ (Ctrl + Click для выбора нескольких)" -OutputMode Multiple
    
    if ($ToKill) {
        $ConfirmKill = Read-Host "`nУдалить выбранные папки навсегда? (Y/N)"
        if ($ConfirmKill -match '^[YyДд]') {
            foreach ($Folder in $ToKill) {
                try {
                    Remove-Item -Path $Folder.FullName -Recurse -Force -ErrorAction Stop
                    Write-Host "  [OK] Удалено: $($Folder.FullName)" -ForegroundColor Green
                } catch {
                    Write-Host "  [FAIL] Ошибка удаления $($Folder.FullName): $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        }
    } else {
        Write-Host "Очистка папок отменена пользователем." -ForegroundColor Gray
    }
} else {
    Write-Host "Остаточных папок не обнаружено." -ForegroundColor Green
}

Write-Host "`nОперация завершена." -ForegroundColor Cyan
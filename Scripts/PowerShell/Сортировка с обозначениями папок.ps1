# Скрипт сортировки файлов по категориям

$targetPath = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
Write-Host "[+] Обработка папки: $targetPath" -ForegroundColor Cyan
Set-Location -LiteralPath $targetPath

Get-ChildItem -File | Where-Object { $_.Name -ne "CleanHere.ps1" -and $_.Name -ne "Sort-FilesByType.ps1" } | ForEach-Object {
    $file = $_
    $name = $file.Name
    $ext  = $file.Extension.ToLower()

    $dest = switch -Regex ($name) {
        'Утилиты_Группировки'            { "Утилиты Группировки" }
        'snake|змейка'                   { "Snake Game" }
        'gemini'                         { "Gemini HTML" }
        'Ксеноморф'                      { "3D Ксеноморфы" }
        'Хищник'                         { "3D Хищники" }
        '^(AUTH|RSA|mlcert)'             { "Сертификаты и Ключи" }
        '^(Регламент_Инвентаризация|Инвентаризация|ТМЦ|ОС)' { "1С_Обработки" }
        default {
            switch ($ext) {
                { $_ -in '.epf', '.erf', '.cfe' }                  { "1С_Обработки" }
                { $_ -in '.dxf', '.stl', '.ctf' }                  { "3D_CAD" }
                { $_ -in '.rdp' }                                  { "RDP Подключения" }
                { $_ -in '.bat', '.cmd', '.ps1', '.sh' }           { "Скрипты" }
                { $_ -in '.exe', '.msi', '.apk' }                  { "Установщики" }
                { $_ -in '.html', '.htm' }                         { "HTML_Страницы" }
                { $_ -in '.xml', '.json', '.csv' }                 { "Данные_XML_JSON" }
                { $_ -in '.pdf', '.docx', '.doc', '.xlsx', '.xls', '.pptx', '.txt' } { "Документы" }
                { $_ -in '.zip', '.rar', '.7z', '.tar', '.gz', '.iso' }               { "Архивы" }
                { $_ -in '.png', '.jpg', '.jpeg', '.gif', '.webp', '.svg', '.bmp' }   { "Изображения" }
                { $_ -in '.mp4', '.mov', '.mkv', '.avi', '.webm' }                    { "Видео" }
                { $_ -in '.mp3', '.wav', '.ogg', '.flac', '.m4a' }                    { "Аудио" }
                default                                            { "Разное" }
            }
        }
    }

    if ($dest) {
        if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Name $dest | Out-Null }
        Move-Item -LiteralPath $file.FullName -Destination $dest -Force
        Write-Host "  -> Перемещен: $($file.Name) в $dest" -ForegroundColor Gray
    }
}

Write-Host "`n[OK] Сортировка успешно завершена!" -ForegroundColor Green
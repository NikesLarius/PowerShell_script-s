# Определяем текущую директорию: папку самого скрипта либо текущую рабочую директорию
$targetPath = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

Write-Host "[+] Обработка папки: $targetPath" -ForegroundColor Cyan

Set-Location $targetPath

Get-ChildItem -File | ForEach-Object {
    $file = $_
    $name = $file.Name
    $ext  = $file.Extension.ToLower()

    # Пропускаем сам выполняемый скрипт, чтобы случайно его не переместить
    if ($PSCommandPath -and ($file.FullName -eq $PSCommandPath)) { return }

    $dest = switch -Regex ($name) {
        'Утилита_группировки'           { "Утилита группировки" }
        'snake|Змейка'                  { "Snake Game" }
        'gemini'                        { "Gemini HTML" }
        'Телефоны'                      { "Базы Телефонов" }
        'Глошница'                      { "3D Модели" }
        '^(AUTH|RSA|mlcert)'            { "ЭЦП и Ключи" }
        '^(МИ_Перенос|Подбор|ЭСФ|УАГЗ)' { "1С_Обработки" }
        default {
            switch ($ext) {
                { $_ -in '.epf', '.erf', '.cfe' }                                     { "1С_Обработки" }
                { $_ -in '.dxf', '.stl', '.ctf' }                                     { "3D_CAD" }
                { $_ -in '.rdp' }                                                     { "RDP Подключения" }
                { $_ -in '.bat', '.cmd', '.ps1', '.sh' }                              { "Скрипты" }
                { $_ -in '.exe', '.msi', '.apk' }                                     { "Установщики" }
                { $_ -in '.html', '.htm' }                                            { "HTML_Страницы" }
                { $_ -in '.xml', '.json', '.csv' }                                    { "Данные_XML_JSON" }
                { $_ -in '.pdf', '.docx', '.doc', '.xlsx', '.xls', '.pptx', '.txt' } { "Документы" }
                { $_ -in '.zip', '.rar', '.7z', '.tar', '.gz', '.iso' }               { "Архивы" }
                { $_ -in '.png', '.jpg', '.jpeg', '.gif', '.webp', '.svg', '.bmp' }  { "Изображения" }
                { $_ -in '.mp4', '.mov', '.mkv', '.avi', '.webm' }                   { "Видео" }
                { $_ -in '.mp3', '.wav', '.ogg', '.flac', '.m4a' }                   { "Аудио" }
                default                                                               { "Разное" }
            }
        }
    }

    if ($dest) {
        if (-not (Test-Path -LiteralPath $dest)) { 
            New-Item -ItemType Directory -Name $dest | Out-Null 
        }
        Move-Item -LiteralPath $file.FullName -Destination $dest -Force
    }
}

Write-Host "`n[OK] Сортировка успешно завершена!" -ForegroundColor Green
pause
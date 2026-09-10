# Устанавливаем UTF-8 для корректной обработки кириллицы из консольных утилит
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "Поиск доступных обновлений через winget..." -ForegroundColor Cyan

# 1. Получаем вывод winget
$rawOutput = winget upgrade --include-unknown 2>$null

# 2. Ищем разделительную линию (например, "-----------")
$separatorIndex = -1
for ($i = 0; $i -lt $rawOutput.Count; $i++) {
    if ($rawOutput[$i] -match '^-{4,}') {
        $separatorIndex = $i
        break
    }
}

# Если таблица не найдена или обновлений нет
if ($separatorIndex -eq -1 -or ($separatorIndex + 1) -ge $rawOutput.Count) {
    Write-Host "Доступных обновлений не найдено или все программы уже обновлены." -ForegroundColor Green
    return
}

# 3. Парсим строки после разделителя
$apps = [System.Collections.Generic.List[PSCustomObject]]::new()

for ($i = $separatorIndex + 1; $i -lt $rawOutput.Count; $i++) {
    $line = $rawOutput[$i]
    
    # Пропускаем пустые строки и служебные сообщения в конце
    if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^\d+ upgrades? available' -or $line -match 'доступно обновлени') {
        continue
    }

    # Разделяем по 2 и более пробелам
    $parts = ($line -split '\s{2,}') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if ($parts.Count -ge 3) {
        $apps.Add([PSCustomObject]@{
            Name      = $parts[0].Trim()
            Id        = $parts[1].Trim()
            Version   = $parts[2].Trim()
            Available = if ($parts.Count -ge 4) { $parts[3].Trim() } else { "Unknown" }
        })
    }
}

if ($apps.Count -eq 0) {
    Write-Host "Список обновлений пуст." -ForegroundColor Green
    return
}

# 4. Выводим графическое окно с выбором
$selected = $apps | Out-GridView -Title "Выберите программы для обновления (Ctrl + ЛКМ для выбора нескольких)" -OutputMode Multiple

# 5. Запуск установки выбранных пакетов
if ($selected) {
    foreach ($app in $selected) {
        Write-Host "`nОбновление: $($app.Name) ($($app.Id))..." -ForegroundColor Yellow
        winget upgrade --id $app.Id --exact --include-unknown --accept-source-agreements --accept-package-agreements
    }
    Write-Host "`nВсе выбранные программы обработаны!" -ForegroundColor Green
} else {
    Write-Host "Обновление отменено: ничего не выбрано." -ForegroundColor Gray
}
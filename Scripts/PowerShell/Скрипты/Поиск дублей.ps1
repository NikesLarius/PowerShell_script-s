Add-Type -AssemblyName Microsoft.VisualBasic

$Path = (Get-Location).Path
Clear-Host
Write-Host "Поиск 100% дубликатов в: $Path..." -ForegroundColor Cyan

# 1. Сбор файлов (исключаем системные и скрытые)
$files = Get-ChildItem -LiteralPath $Path -File -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { 
        ($_.Attributes -band [System.IO.FileAttributes]::System) -eq 0 -and
        ($_.Attributes -band [System.IO.FileAttributes]::Hidden) -eq 0
    }

# 2. Предварительная фильтрация по размеру
$sizeGroups = $files | Group-Object -Property Length | Where-Object { $_.Count -gt 1 }

if (-not $sizeGroups) {
    Write-Host "Файлов с одинаковым размером нет." -ForegroundColor Green
    return
}

# 3. Вычисление SHA256 и определение приоритета имени
$candidateFiles = foreach ($group in $sizeGroups) {
    foreach ($file in $group.Group) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        
        # Определяем номер дубликата в конце имени: " (число)"
        # Если скобок нет — даем 0 (чистое имя, максимальный приоритет)
        $copyIndex = 0
        if ($baseName -match '\((\d+)\)$') {
            $copyIndex = [int]$matches[1]
        }

        [PSCustomObject]@{
            Name         = $file.Name
            FullName     = $file.FullName
            SizeMB       = [math]::Round($file.Length / 1MB, 2)
            LastModified = $file.LastWriteTime
            CopyIndex    = $copyIndex
            Hash         = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256 -ErrorAction SilentlyContinue).Hash
        }
    }
}

# 4. Группировка 100% клонов
$duplicateGroups = $candidateFiles |
    Where-Object { $_.Hash } |
    Group-Object -Property Hash |
    Where-Object { $_.Count -gt 1 }

if (-not $duplicateGroups) {
    Write-Host "100% идентичных копий не найдено." -ForegroundColor Green
    return
}

Write-Host "`nНайдено групп дубликатов: $($duplicateGroups.Count)" -ForegroundColor Yellow
$deletedCount = 0

# 5. Автоматическая обработка по приоритетам
foreach ($group in $duplicateGroups) {
    # Сортировка:
    # 1. По наименьшему номеру (0 - чистое имя -> 1 -> 2 -> 3...)
    # 2. Если имена одинакового формата — по дате изменения (более свежий)
    $sorted = $group.Group | Sort-Object -Property @{ Expression = { $_.CopyIndex }; Ascending = $true }, 
                                                  @{ Expression = { $_.LastModified }; Descending = $true }

    $keep = $sorted[0]
    $toDelete = $sorted | Select-Object -Skip 1

    Write-Host "`n[Группа Хэш: $($group.Name.Substring(0, 10))... | $($keep.SizeMB) МБ]" -ForegroundColor Magenta
    Write-Host "  [ОСТАВЛЕН]  (Индекс: $($keep.CopyIndex)) -> $($keep.FullName)" -ForegroundColor Green

    foreach ($file in $toDelete) {
        try {
            [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile(
                $file.FullName,
                'OnlyErrorDialogs',
                'SendToRecycleBin'
            )
            Write-Host "  [В КОРЗИНУ] (Индекс: $($file.CopyIndex)) -> $($file.FullName)" -ForegroundColor DarkGray
            $deletedCount++
        } catch {
            Write-Host "  [ОШИБКА]    Не удалось удалить: $($file.FullName)" -ForegroundColor Red
        }
    }
}

Write-Host "`nГотово! Перемещено в Корзину файлов: $deletedCount шт." -ForegroundColor Cyan
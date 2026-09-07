Write-Host "--- Сетевая информация ---" -ForegroundColor Cyan

# 1. Локальные IP-адреса активных адаптеров
Write-Host "`nЛокальные IP-адреса:" -ForegroundColor Yellow
$localIPs = Get-NetIPAddress -AddressFamily IPv4 | 
    Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.IPAddress -notlike '169.254*' }

$localIPs | ForEach-Object {
    [PSCustomObject]@{
        'Интерфейс' = $_.InterfaceAlias
        'IP-адрес'  = $_.IPAddress
    }
} | Format-Table -AutoSize

# 2. Внешний (публичный) IP-адрес
Write-Host "Внешний (публичный) IP:" -ForegroundColor Yellow
try {
    $publicIP = (Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 3).Trim()
    Write-Host "Ваш публичный IP: $publicIP" -ForegroundColor Green
}
catch {
    Write-Host "Не удалось определить внешний IP (нет интернета или сервис недоступен)." -ForegroundColor Red
}

# 3. Проверка пинга через графическое окно
Write-Host "`nПроверка пинга:" -ForegroundColor Yellow

Add-Type -AssemblyName Microsoft.VisualBasic
$target = [Microsoft.VisualBasic.Interaction]::InputBox(
    "Введите IP-адрес или домен (например, 8.8.8.8 или ya.ru):`nОставьте пустым или нажмите Отмена для пропуска.",
    "Проверка пинга",
    "8.8.8.8"
)

if (-not [string]::IsNullOrWhiteSpace($target)) {
    Write-Host "Отправка запросов к $target..." -ForegroundColor DarkGray
    
    $pingResult = Test-Connection -ComputerName $target -Count 4 -ErrorAction SilentlyContinue

    if ($pingResult) {
        $avgLatency = [Math]::Round(($pingResult | Measure-Object -Property ResponseTime -Average).Average, 1)
        Write-Host "Узел доступен! Средний пинг: ${avgLatency} мс" -ForegroundColor Green
        
        $pingResult | Select-Object @{Name='Узел'; Expression={$_.Address}}, 
                                  @{Name='Время (мс)'; Expression={$_.ResponseTime}}, 
                                  @{Name='TTL'; Expression={$_.TimeToLive}} | Format-Table -AutoSize
    }
    else {
        Write-Host "Узел $target недоступен или превышено время ожидания ответа." -ForegroundColor Red
    }
}
else {
    Write-Host "Проверка пинга пропущена." -ForegroundColor DarkGray
}
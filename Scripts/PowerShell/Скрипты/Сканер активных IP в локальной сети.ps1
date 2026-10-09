$subnet = "192.168.1" # Укажите вашу подсеть

$pingTasks = 1..254 | ForEach-Object {
    $ip = "$subnet.$_"
    $ping = [System.Net.NetworkInformation.Ping]::new()
    [PSCustomObject]@{
        IP   = $ip
        Task = $ping.SendPingAsync($ip, 200) # таймаут 200 мс
    }
}

[System.Threading.Tasks.Task]::WaitAll($pingTasks.Task)

$pingTasks | Where-Object { $_.Task.Result.Status -eq "Success" } | Select-Object -Property IP
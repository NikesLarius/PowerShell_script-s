#Запустить в нужной папке

Get-ChildItem -File | ForEach-Object {
    $ext = if ($_.Extension) { $_.Extension.TrimStart('.').ToLower() } else { 'No_Extension' }
    New-Item -ItemType Directory -Name $ext -Force | Out-Null
    Move-Item -Path $_.FullName -Destination $ext
}
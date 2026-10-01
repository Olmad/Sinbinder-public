# Скачивает картинки сайта (логотип, фото товаров, слайдер, иконки) со старого сайта
# fortuna-futon.ru в папку assets\img. Достаточно запустить один раз.
# Запуск: двойной щелчок по download-images.bat
# (или правой кнопкой по этому файлу -> «Выполнить с помощью PowerShell»).

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$list = Join-Path $root 'assets\img\images.txt'

$ok = 0; $skip = 0; $failed = @()
foreach ($line in [System.IO.File]::ReadAllLines($list, [System.Text.Encoding]::UTF8)) {
    if (-not $line.Trim() -or $line.StartsWith('#')) { continue }
    $parts = $line.Split("`t")
    $name = $parts[0]
    $url = $parts[1]
    $target = Join-Path $root ($name -replace '/', '\')
    if ((Test-Path $target) -and ((Get-Item $target).Length -gt 0)) { $skip++; continue }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    try {
        $wc = New-Object System.Net.WebClient
        $wc.Headers.Add('User-Agent', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36')
        $wc.Headers.Add('Referer', 'https://fortuna-futon.ru/')
        $wc.DownloadFile($url, $target)
        $ok++
        Write-Host "OK      $name"
    } catch {
        if (Test-Path $target) { Remove-Item $target -Force }
        $failed += $name
        Write-Host "ОШИБКА  $name — $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}

Write-Host ''
Write-Host "Скачано: $ok, уже было: $skip, не удалось: $($failed.Count)"
if ($failed.Count -gt 0) {
    Write-Host 'Проверьте, открывается ли https://fortuna-futon.ru в браузере, и запустите ещё раз.' -ForegroundColor Yellow
    Write-Host 'Картинки можно положить и вручную — имена и адреса перечислены в assets\img\images.txt.' -ForegroundColor Yellow
} else {
    Write-Host 'Готово! Обновите страницу сайта в браузере (Ctrl+F5).' -ForegroundColor Green
}
Read-Host 'Нажмите Enter, чтобы закрыть окно'

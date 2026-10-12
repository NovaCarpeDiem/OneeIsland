Stop-Process -Name DynamicIslandWin -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "C:\Users\ali\.gemini\antigravity\scratch\DynamicIslandWin\bin\Release\net10.0-windows10.0.19041.0\DynamicIslandWin.exe"
$psi.WorkingDirectory = "C:\Users\ali\.gemini\antigravity\scratch\DynamicIslandWin"
$psi.UseShellExecute = $true
[System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 1
Get-Process DynamicIslandWin -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, WorkingSet64

$targetExe = Join-Path $PSScriptRoot "bin\Release\TodoTray.exe"
if (-not (Test-Path $targetExe)) {
    $targetExe = Join-Path $PSScriptRoot "bin\Debug\TodoTray.exe"
}

if (-not (Test-Path $targetExe)) {
    Write-Host "[ERROR] Could not find TodoTray.exe in bin\Release or bin\Debug." -ForegroundColor Red
    Write-Host "Please build the project first."
    pause
    exit 1
}

$wsh = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$desktopLnk = Join-Path $desktop "TodoTray.lnk"
$sc1 = $wsh.CreateShortcut($desktopLnk)
$sc1.TargetPath = $targetExe
$sc1.WorkingDirectory = Split-Path $targetExe
$sc1.Description = "TodoTray - System Tray To-Do App"
$sc1.Save()

$startMenu = Join-Path ([Environment]::GetFolderPath('StartMenu')) "Programs"
$startLnk = Join-Path $startMenu "TodoTray.lnk"
$sc2 = $wsh.CreateShortcut($startLnk)
$sc2.TargetPath = $targetExe
$sc2.WorkingDirectory = Split-Path $targetExe
$sc2.Description = "TodoTray - System Tray To-Do App"
$sc2.Save()

Write-Host "[SUCCESS] Shortcuts created:" -ForegroundColor Green
Write-Host " - Desktop:    $desktopLnk"
Write-Host " - Start Menu: $startLnk"
Write-Host "`nYou can now launch TodoTray anytime from your Desktop or Start Menu!"

@echo off
setlocal
echo ========================================================
echo        TodoTray - Shortcut Creator
echo ========================================================
echo.

set "TARGET_EXE=%~dp0bin\Release\TodoTray.exe"
if not exist "%TARGET_EXE%" (
    set "TARGET_EXE=%~dp0bin\Debug\TodoTray.exe"
)

if not exist "%TARGET_EXE%" (
    echo [ERROR] Could not find TodoTray.exe in bin\Release or bin\Debug.
    echo Please build the project first.
    pause
    exit /b 1
)

echo Target: %TARGET_EXE%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$wsh = New-Object -ComObject WScript.Shell; " ^
    "$desktop = [Environment]::GetFolderPath('Desktop'); " ^
    "$sc1 = $wsh.CreateShortcut(\"$desktop\TodoTray.lnk\"); " ^
    "$sc1.TargetPath = '%TARGET_EXE%'; " ^
    "$sc1.WorkingDirectory = [System.IO.Path]::GetDirectoryName('%TARGET_EXE%'); " ^
    "$sc1.Description = 'TodoTray - System Tray To-Do App'; " ^
    "$sc1.Save(); " ^
    "$startMenu = [Environment]::GetFolderPath('StartMenu') + '\Programs'; " ^
    "$sc2 = $wsh.CreateShortcut(\"$startMenu\TodoTray.lnk\"); " ^
    "$sc2.TargetPath = '%TARGET_EXE%'; " ^
    "$sc2.WorkingDirectory = [System.IO.Path]::GetDirectoryName('%TARGET_EXE%'); " ^
    "$sc2.Description = 'TodoTray - System Tray To-Do App'; " ^
    "$sc2.Save();"

echo [SUCCESS] Shortcuts successfully created:
echo  - Desktop:      %%USERPROFILE%%\Desktop\TodoTray.lnk
echo  - Start Menu:   %%APPDATA%%\Microsoft\Windows\Start Menu\Programs\TodoTray.lnk
echo.
echo You can now launch TodoTray anytime from your Desktop or Start Menu!
echo.
pause

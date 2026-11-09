@echo off
setlocal
set "ROOT=%~dp0"
set "GAME=%ROOT%Build\Sinbinder\Sinbinder.exe"
if not exist "%GAME%" (
  echo Sinbinder.exe not found in Build\Sinbinder.
  echo First build the Unity project with "Собрать игру.cmd".
  pause
  exit /b 1
)
echo Starting two LAN test clients on this computer...
start "Sinbinder LAN host" "%GAME%" -lan-host -lan-auto-ready -port 7777 -screen-width 1024 -screen-height 720 -screen-fullscreen 0
timeout /t 3 /nobreak > nul
start "Sinbinder LAN client" "%GAME%" -lan-connect 127.0.0.1 -lan-auto-ready -port 7777 -screen-width 1024 -screen-height 720 -screen-fullscreen 0
echo Two game windows should appear. Right-click on the test field to move.
echo Close the windows to stop. This is NOT the normal Sinbinder campaign.
endlocal

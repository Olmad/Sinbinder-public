@echo off
setlocal
set "GAME=%~dp0Build\Sinbinder\Sinbinder.exe"
if not exist "%GAME%" (
  echo No Unity build found at Build\Sinbinder\Sinbinder.exe.
  echo Build first using "Собрать игру.cmd".
  pause
  exit /b 1
)
echo Starting REAL Warrior registration and AOS order diagnostics.
echo This is ONE LOCAL UNITY GAME, NOT two-player multiplayer yet.
echo Look for [Sinbinder MP Probe] in Unity Player.log.
start "Sinbinder: world authority diagnostics" "%GAME%" -mp-test-map -mp-world-probe -screen-width 1280 -screen-height 800 -screen-fullscreen 0
endlocal

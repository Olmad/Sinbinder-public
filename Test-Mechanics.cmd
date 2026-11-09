@echo off
setlocal
set "GAME=%~dp0Build\Sinbinder\Sinbinder.exe"
if not exist "%GAME%" (
  echo No Unity build in Build\Sinbinder. First run "Собрать игру.cmd".
  pause
  exit /b 1
)
start "Sinbinder: mechanics test map" "%GAME%" -mp-test-map -screen-width 1280 -screen-height 800 -screen-fullscreen 0
endlocal

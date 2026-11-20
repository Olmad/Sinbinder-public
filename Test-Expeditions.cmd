@echo off
setlocal
set "GAME=%~dp0Build\Sinbinder\Sinbinder.exe"
if not exist "%GAME%" (
  echo No Unity build in Build\Sinbinder. First run "Собрать игру.cmd".
  pause
  exit /b 1
)
rem Isolated QA entrance, not a new mission or a parallel AOS simulation.
rem Crypt_Test has the real MissionBoard and MissionMapUI.
start "Sinbinder: expedition QA" "%GAME%" -expedition-test-map -screen-width 1280 -screen-height 800 -screen-fullscreen 0
endlocal

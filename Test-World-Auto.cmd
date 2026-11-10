@echo off
chcp 65001 >nul
echo ================================================
echo SINBINDER - real Unity world LAN acceptance test
echo ================================================
echo This launches ONE authoritative Unity host and TWO independent clients.
echo Rebuild first with: "Собрать игру.cmd"
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-World-Auto.ps1" %*
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" (
  echo.
  echo REAL UNITY TEST FAILED. See Logs\Multiplayer.
  pause
) else (
  echo.
  echo All real Unity acceptance checks passed; see Logs\Multiplayer.
  pause
)
exit /b %RESULT%

@echo off
setlocal
set "GAME=%~dp0Build\Sinbinder\Sinbinder.exe"
set "LOG=%~dp0Build\Sinbinder\WorldHost.log"
set "PORT=7786"
set "PING_MODE=conservative"
if /I "%~1"=="off" set "PING_MODE=off"
if /I "%~1"=="balanced" set "PING_MODE=balanced"
if /I "%~1"=="conservative" set "PING_MODE=conservative"

if not exist "%GAME%" (
  echo No Unity build at Build\Sinbinder\Sinbinder.exe.
  echo First run the existing build script: "Собрать игру.cmd".
  pause
  exit /b 1
)

echo Starting REAL-Warrior diagnostic server on 127.0.0.1:%PORT%
echo Server input ping compensation: %PING_MODE%
echo Modes: off / conservative / balanced (supply as first argument)
echo Real AOS/Warrior simulation runs in this headless Unity host.
echo Clients still display the capsule proving ground; the real
echo gameplay map is NOT synchronized or rendered remotely yet.
start "Sinbinder World Host" "%GAME%" -batchmode -nographics -mp-test-map -mp-world-host -mp-ping-balance %PING_MODE% -port %PORT% -logFile "%LOG%"

echo Launching two LAN client windows...
timeout /t 6 /nobreak >nul
start "Sinbinder World Client 1" "%GAME%" -lan-connect 127.0.0.1 -lan-auto-ready -port %PORT% -screen-width 1100 -screen-height 720 -screen-fullscreen 0
timeout /t 2 /nobreak >nul
start "Sinbinder World Client 2" "%GAME%" -lan-connect 127.0.0.1 -lan-auto-ready -port %PORT% -screen-width 1100 -screen-height 720 -screen-fullscreen 0

echo When connected, press F8 in EACH client window to issue
echo a server-authorized Move order to that client's REAL Lord.
echo Inspect WorldHost.log for errors and world registration.
echo This remains an early transport integration test.
endlocal

@echo off
echo Starting OctopusPet to test audio detection...
echo Please play music through speakers and headphones separately.
echo Check the log file for MusicDetector messages.
echo.
echo Press any key to start...
pause >nul

cd /d "%~dp0"
start "" "bin\Release\net9.0-windows\OctopusPet.exe"

echo.
echo The program is now running.
echo Please test with speakers and headphones.
echo Check the log file: bin\Release\net9.0-windows\octopus_pet.log
echo.
echo Press any key to exit...
pause >nul
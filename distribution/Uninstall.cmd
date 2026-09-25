@echo off
cd /d "%~dp0"
dotnet "%~dp0bin\DragonsDogma2DualSense.dll" uninstall
if errorlevel 1 pause

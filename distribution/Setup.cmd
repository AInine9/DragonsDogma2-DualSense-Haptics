@echo off
cd /d "%~dp0"
dotnet "%~dp0bin\DragonsDogma2DualSense.dll" setup %*
if errorlevel 1 pause

@echo off
:: Installer builder — double-click to run (no ExecutionPolicy issues)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
pause

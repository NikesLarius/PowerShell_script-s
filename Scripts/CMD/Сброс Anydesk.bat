@echo off
net stop AnyDesk >nul 2>&1
taskkill /F /IM AnyDesk.exe >nul 2>&1
del /F /Q "%ProgramData%\AnyDesk\*.conf" >nul 2>&1
del /F /Q "%AppData%\AnyDesk\*.conf" >nul 2>&1
echo AnyDesk reset completed.
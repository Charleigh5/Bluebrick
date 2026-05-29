@echo off
set "DLL_PATH=%~dp0bin\Debug\BlueBrick.dll"
echo Registering Add-in from: %DLL_PATH%
"%windir%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "%DLL_PATH%"
pause

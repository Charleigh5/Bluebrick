@echo off
set DLL=C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\bin\Lab\BlueBrick.Lab.dll
echo Registering: %DLL%
%windir%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe /codebase "%DLL%"
echo.
echo Done. Exit code: %ERRORLEVEL%

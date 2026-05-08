@echo off
set DOTNET_ROOT=C:\Users\vsmir\.dotnet
set PATH=%DOTNET_ROOT%;%PATH%
cd /d "c:\VS\BrowserXn"
"%DOTNET_ROOT%\dotnet.exe" run --project src\BrowserXn
if errorlevel 1 (
    echo.
    echo Exit code: %errorlevel%
    pause
)

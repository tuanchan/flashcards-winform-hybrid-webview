@echo off
chcp 65001 >nul
setlocal

echo ===============================
echo   PUBLISH .NET APP - RELEASE
echo ===============================

set CONFIG=Release
set FRAMEWORK=net8.0-windows
set RUNTIME=win-x64
set OUTPUT=bin\Publish

echo.
echo Dang tim file .csproj...

for %%f in (*.csproj) do (
    set CSPROJ=%%f
    goto found
)

echo Khong tim thay file .csproj trong thu muc hien tai.
echo Hay dat file publish.bat ngang hang voi file .csproj
pause
exit /b 1

:found
echo Tim thay project: %CSPROJ%

echo.
echo Dang clean...
dotnet clean "%CSPROJ%" -c %CONFIG%

echo.
echo Dang restore...
dotnet restore "%CSPROJ%"

echo.
echo Dang publish app nhe hon...
dotnet publish "%CSPROJ%" ^
 -c %CONFIG% ^
 -f %FRAMEWORK% ^
 -r %RUNTIME% ^
 --self-contained false ^
 -o "%OUTPUT%" ^
 /p:PublishSingleFile=false ^
 /p:PublishReadyToRun=false ^
 /p:PublishTrimmed=false

echo.
if %ERRORLEVEL% neq 0 (
    echo Publish that bai.
    pause
    exit /b 1
)

echo ===============================
echo Publish thanh cong!
echo File nam tai: %OUTPUT%
echo ===============================

explorer "%OUTPUT%"
pause
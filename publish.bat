@echo off
echo ========================================
echo  SmoothAutoRun - Build Script
echo ========================================
echo.

echo [1/3] Restoring NuGet packages...
dotnet restore SmoothAutoRun.csproj
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)

echo [2/3] Building Release...
dotnet build SmoothAutoRun.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo [3/3] Publishing single executable...
dotnet publish SmoothAutoRun.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -o ./publish
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo  BUILD SUCCESSFUL!
echo  Executable: publish\SmoothAutoRun.exe
echo ========================================
pause
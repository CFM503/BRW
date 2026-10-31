@echo off
setlocal
echo ===================================================
echo [Build] Compiling China Gas - Oracle EBS Dedicated Browser (x86)...
echo ===================================================

set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] .NET Framework Compiler not found: %CSC%
    pause
    exit /b 1
)

"%CSC%" /target:winexe /platform:x86 /r:System.Security.dll /win32manifest:app.manifest /win32icon:app.ico /out:EBSBrowser.exe *.cs
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Compilation failed!
    pause
    exit /b 1
)

echo.
echo [DEP Patch] Patching PE Header for Java 6 compatibility...
"%CSC%" /target:exe /out:"%~dp0tools\PatchPE.exe" "%~dp0tools\PatchPE.cs" >nul
"%~dp0tools\PatchPE.exe" "%~dp0EBSBrowser.exe"
if exist "%~dp0tools\PatchPE.exe" del "%~dp0tools\PatchPE.exe"

echo.
echo ===================================================
echo [SUCCESS] EBS Dedicated Browser build complete!
echo Output: %~dp0EBSBrowser.exe
echo ===================================================

pause

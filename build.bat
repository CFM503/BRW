@echo off
chcp 65001 >nul
echo ===================================================
echo 正在编译 中国燃气 - Oracle EBS 专属浏览器 (x86)...
echo ===================================================

set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [错误] 未找到 .NET Framework 编译器: %CSC%
    pause
    exit /b 1
)

"%CSC%" /target:winexe /platform:x86 /r:System.Security.dll /win32manifest:app.manifest /win32icon:app.ico /out:EBSBrowser.exe *.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo ===================================================
    echo [成功] EBS 专属浏览器编译完成！
    echo 生成文件: %~dp0EBSBrowser.exe
    echo ===================================================
) else (
    echo.
    echo [错误] 编译失败，请检查报错信息。
)

pause

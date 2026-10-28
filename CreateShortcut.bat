@echo off
chcp 65001 >nul
echo 正在创建桌面快捷方式...

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ws = New-Object -ComObject WScript.Shell; " ^
    "$desktop = [Environment]::GetFolderPath('Desktop'); " ^
    "$s = $ws.CreateShortcut(\"$desktop\中国燃气-EBS专用浏览器.lnk\"); " ^
    "$s.TargetPath = '%~dp0EBSBrowser.exe'; " ^
    "$s.WorkingDirectory = '%~dp0'; " ^
    "$s.Description = '中国燃气 Oracle EBS 专属工作台'; " ^
    "$s.Save()"

echo 桌面快捷方式已成功创建: 中国燃气-EBS专用浏览器
pause

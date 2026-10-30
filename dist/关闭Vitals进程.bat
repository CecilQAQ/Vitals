@echo off
chcp 936 >nul
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    echo 正在申请管理员权限...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" >nul 2>&1
    exit /b
)

echo ============================================
echo   结束 Vitals 相关进程
echo ============================================
echo.

taskkill /IM Vitals.exe /F >nul 2>&1
taskkill /IM PresentMon.exe /F >nul 2>&1

echo 已尝试结束以下进程：
echo   - Vitals.exe      （主程序）
echo   - PresentMon.exe  （帧率采集子进程）
echo.
echo 如果托盘图标仍在，请右键托盘图标选择「退出」。
echo.
pause

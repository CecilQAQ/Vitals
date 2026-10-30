@echo off
chcp 936 >nul
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    echo 正在申请管理员权限...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" >nul 2>&1
    exit /b
)

echo ============================================
echo   Vitals 清理
echo ============================================
echo.

taskkill /IM Vitals.exe /F >nul 2>&1
taskkill /IM PresentMon.exe /F >nul 2>&1

schtasks /Delete /TN Vitals /F >nul 2>&1

del /f /q "%~dp0Vitals.ini" >nul 2>&1
del /f /q "%~dp0Vitals.log" >nul 2>&1
del /f /q "%~dp0Vitals-*.csv" >nul 2>&1

rd /s /q "%TEMP%\Vitals" >nul 2>&1

echo 已清理：
echo   - Vitals.exe / PresentMon.exe 进程
echo   - 开机自启计划任务（Vitals）
echo   - Vitals.ini（配置）
echo   - Vitals.log（崩溃日志）
echo   - Vitals-*.csv（记录数据）
echo   - %%TEMP%%\Vitals（临时文件）
echo.
echo 注意：PawnIO 驱动不会被卸载。
echo       如需卸载，请到「控制面板 - 程序和功能」中卸载 PawnIO。
echo.
pause

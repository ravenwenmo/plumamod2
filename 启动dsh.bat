@echo off
chcp 65001 >nul
title 启动 dsh web

start "dsh-web" cmd /k "npx -y @deepseek-ai/dsh web"
timeout /t 3 /nobreak >nul
start http://127.0.0.1:3080

echo.
echo 服务已启动，浏览器已打开。
echo 关闭 dsh-web 窗口即可停止。
pause
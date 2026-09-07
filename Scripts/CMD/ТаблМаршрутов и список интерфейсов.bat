@echo off
title Сеть - маршруты и интерфейсы

echo ================================
echo ТАБЛИЦА МАРШРУТОВ (route print)
echo ================================
route print

echo.
echo ================================
echo СПИСОК ИНТЕРФЕЙСОВ (netsh)
echo ================================
netsh interface ipv4 show interfaces

echo.
pause

@echo off
setlocal
set ROOT=%~dp0



REM ---- ControlPlane ----
start "CP" cmd /k dotnet run --project ControlPlaneServer\ControlPlaneServer.csproj
timeout /t 1 >nul

REM ---- API ----
start "API" cmd /k dotnet run --project ApiServer\ApiServer.csproj
timeout /t 1 >nul

REM ---- Game / Town ----
cd /d "%ROOT%GameServer"
dotnet build

start "Game" cmd /k dotnet .\bin\Debug\net8.0\GameServer.dll --role Game 
timeout /t 1 >nul

start "Town" cmd /k dotnet .\bin\Debug\net8.0\GameServer.dll --role Town 
timeout /t 1 >nul

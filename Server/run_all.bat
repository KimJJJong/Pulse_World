@echo off
setlocal EnableExtensions
set ROOT=%~dp0
cd /d "%ROOT%"
set ASPNETCORE_ENVIRONMENT=Development
set DOTNET_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://0.0.0.0:5290

call :ensure_file "%ROOT%ControlPlaneServer\appsettings.json" || exit /b 1
call :ensure_file "%ROOT%ApiServer\appsettings.json" || exit /b 1
call :ensure_file "%ROOT%GameServer\appsettings.json" || exit /b 1
call :ensure_file "%ROOT%GameServer\appsettings.Game.json" || exit /b 1
call :ensure_file "%ROOT%GameServer\appsettings.Town.json" || exit /b 1

echo Stopping existing local server processes...
call :stop_existing

call :build "%ROOT%ControlPlaneServer\ControlPlaneServer.csproj" "ControlPlaneServer" || exit /b 1
call :build "%ROOT%ApiServer\ApiServer.csproj" "ApiServer" || exit /b 1
call :build "%ROOT%GameServer\GameServer.csproj" "GameServer" || exit /b 1

echo.
echo Starting ControlPlaneServer...
start "CP" /D "%ROOT%ControlPlaneServer" "%ROOT%ControlPlaneServer\bin\Debug\net8.0\ControlPlaneServer.exe"
call :wait_for_port 5001 "ControlPlaneServer" || exit /b 1

echo.
echo Starting ApiServer...
start "API" /D "%ROOT%ApiServer" "%ROOT%ApiServer\bin\Debug\net8.0\ApiServer.exe"
call :wait_for_port 5290 "ApiServer" || exit /b 1

echo.
echo Starting GameServer...
start "Game" /D "%ROOT%GameServer" "%ROOT%GameServer\bin\Debug\net8.0\GameServer.exe" --role Game
call :wait_for_port 13222 "GameServer" || exit /b 1

echo.
echo Starting TownServer...
start "Town" /D "%ROOT%GameServer" "%ROOT%GameServer\bin\Debug\net8.0\GameServer.exe" --role Town
call :wait_for_port 13221 "TownServer" || exit /b 1

echo.
echo Local servers are running.
exit /b 0

:ensure_file
if exist "%~1" exit /b 0
echo Missing required config: %~1
exit /b 1

:build
echo.
echo Building %~2...
dotnet build "%~1"
if errorlevel 1 (
    echo Build failed: %~2
    exit /b 1
)
exit /b 0

:wait_for_port
echo Waiting for %~2 on port %~1...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$port = %~1;" ^
  "for ($i = 0; $i -lt 60; $i++) { if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { exit 0 }; Start-Sleep -Seconds 1 }; exit 1"
if errorlevel 1 (
    echo Timed out waiting for %~2 on port %~1
    exit /b 1
)
exit /b 0

:stop_existing
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$root = [System.IO.Path]::GetFullPath('%ROOT%');" ^
  "$targets = @('ControlPlaneServer.exe', 'ApiServer.exe', 'GameServer.exe');" ^
  "$processes = Get-CimInstance Win32_Process | Where-Object { $targets -contains $_.Name -and $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase) };" ^
  "foreach ($process in $processes) { Write-Host ('Stopping {0} [{1}]' -f $process.Name, $process.ProcessId); Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }"
exit /b 0

@echo off
REM ============================================================================
REM gen-contracts.bat  (Server\WebGenerater에서 실행)
REM - 이 폴더의 packets.yml을 읽어 서버/유니티 두 경로에 Contracts.Generated.cs 생성
REM - PowerShell에서는 ".\gen-contracts.bat" 로 실행
REM - 괄호 블록(IF (...) ...)를 사용하지 않는 안정판
REM ============================================================================

setlocal EnableExtensions

REM ---- 기준 경로(이 파일이 있는 폴더) ----
set "BASE=%~dp0"
pushd "%BASE%" >nul

REM ---- 기본 경로 설정(필요 시 아래 4줄만 수정) ----
REM 스키마 파일(기본: 현재 폴더의 packets.yml)
set "SCHEMA=%BASE%packets.yml"
REM 생성기 프로젝트(.csproj)
set "CSPROJ=%BASE%WebGenerater.csproj"
REM 서버 출력 경로
set "SERVER_OUT=D:\Git\Server\Trick_Or_Troll\Server\Lobby\Domain\Shared"
REM 유니티 출력 경로
set "UNITY_OUT=D:\Git\Server\Trick_Or_Troll\Client\Assets\3.Script\NetWork\Common"
REM 생성 파일명
set "FILENAME=Contracts.Generated.cs"

REM ---- 인자로 기본값 덮어쓰기 (옵션) ----
REM 사용: .\gen-contracts.bat [schema] [server_out] [unity_out] [filename]
if not "%~1"=="" set "SCHEMA=%~1"
if not "%~2"=="" set "SERVER_OUT=%~2"
if not "%~3"=="" set "UNITY_OUT=%~3"
if not "%~4"=="" set "FILENAME=%~4"

echo [INFO] Base       : "%BASE%"
echo [INFO] Schema     : "%SCHEMA%"
echo [INFO] Project    : "%CSPROJ%"
echo [INFO] Out(Server): "%SERVER_OUT%\%FILENAME%"
echo [INFO] Out(Unity) : "%UNITY_OUT%\%FILENAME%"
echo.

REM ---- dotnet 확인 ----
where dotnet >nul 2>nul
if errorlevel 1 goto ERR_DOTNET

REM ---- 파일/폴더 확인 ----
if not exist "%SCHEMA%"  goto ERR_SCHEMA
if not exist "%CSPROJ%"  goto ERR_CSPROJ

REM ---- 출력 폴더 생성 ----
if not exist "%SERVER_OUT%" mkdir "%SERVER_OUT%" 2>nul
if not exist "%UNITY_OUT%"  mkdir "%UNITY_OUT%"  2>nul

REM ---- 생성 실행 ----
echo [INFO] Generating contracts...
dotnet run --project "%CSPROJ%" -- "%SCHEMA%" ^
  --server "%SERVER_OUT%" ^
  --unity  "%UNITY_OUT%" ^
  --filename "%FILENAME%" ^
  --unity-mode jsonutility
if errorlevel 1 goto ERR_GEN

REM ---- 결과 확인 ----
if exist "%SERVER_OUT%\%FILENAME%" echo [OK  ] "%SERVER_OUT%\%FILENAME%"
if exist "%UNITY_OUT%\%FILENAME%"  echo [OK  ] "%UNITY_OUT%\%FILENAME%"
echo [DONE] Contracts generated.
goto END

:ERR_DOTNET
echo [ERR ] .NET SDK (dotnet) not found. Install .NET SDK and retry.
goto END

:ERR_SCHEMA
echo [ERR ] schema not found: "%SCHEMA%"
goto END

:ERR_CSPROJ
echo [ERR ] generator project not found: "%CSPROJ%"
goto END

:ERR_GEN
echo [ERR ] Generation failed.
goto END

:END
popd >nul
exit /b 0

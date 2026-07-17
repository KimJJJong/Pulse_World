@echo off
set "SCRIPT_DIR=%~dp0"

START /WAIT "" "%SCRIPT_DIR%..\..\PacketGenerator\bin\PacketGenerator.exe" "%SCRIPT_DIR%..\..\PacketGenerator\PDL.xml"

XCOPY /Y "%SCRIPT_DIR%GenPackets.cs" "%SCRIPT_DIR%..\..\GameServer\Packet\"
XCOPY /Y "%SCRIPT_DIR%ServerPacketManager.cs" "%SCRIPT_DIR%..\..\GameServer\Packet\"

XCOPY /Y "%SCRIPT_DIR%GenPackets.cs" "%SCRIPT_DIR%..\..\..\Client\Assets\3.Script\NetWork\GameServer\Packet\"
XCOPY /Y "%SCRIPT_DIR%ClientPacketManager.cs" "%SCRIPT_DIR%..\..\..\Client\Assets\3.Script\NetWork\GameServer\Packet\"

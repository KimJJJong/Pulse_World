START ../../PacketGenerator/bin/PacketGenerator.exe ../../PacketGenerator/PDL.xml

XCOPY /Y GenPackets.cs "D:\Git\Server\RhythmRPG\RhythmRPG\Server\GameServer\Packet"
XCOPY /Y ServerPacketManager.cs "D:\Git\Server\RhythmRPG\RhythmRPG\Server\GameServer\Packet"

XCOPY /Y GenPackets.cs "D:\Git\Server\RhythmRPG\RhythmRPG\Client\Assets\3.Script\NetWork\GameServer\Packet"
XCOPY /Y ClientPacketManager.cs "D:\Git\Server\RhythmRPG\RhythmRPG\Client\Assets\3.Script\NetWork\GameServer\Packet"
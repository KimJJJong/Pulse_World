using ServerCore;
using System;
using System.Collections.Generic;

public class PacketManager
{
	#region Singleton
	static PacketManager _instance = new PacketManager();
	public static PacketManager Instance { get { return _instance; } 	}
	#endregion

	PacketManager()
	{
		Register();
	}

	Dictionary<ushort, Func<PacketSession, ArraySegment<byte>, IPacket>> _makeFunc = new Dictionary<ushort, Func<PacketSession, ArraySegment<byte>, IPacket>>();
	Dictionary<ushort, Action<PacketSession, IPacket>> _handler = new Dictionary<ushort, Action<PacketSession, IPacket>>();
		
	public void Register()
	{
		_makeFunc.Add((ushort)PacketID.SC_Welcome, MakePacket<SC_Welcome>);
		_handler.Add((ushort)PacketID.SC_Welcome, PacketHandler.SC_WelcomeHandler);
		_makeFunc.Add((ushort)PacketID.SC_AllPlayersLoaded, MakePacket<SC_AllPlayersLoaded>);
		_handler.Add((ushort)PacketID.SC_AllPlayersLoaded, PacketHandler.SC_AllPlayersLoadedHandler);
		_makeFunc.Add((ushort)PacketID.SC_GameBegin, MakePacket<SC_GameBegin>);
		_handler.Add((ushort)PacketID.SC_GameBegin, PacketHandler.SC_GameBeginHandler);
		_makeFunc.Add((ushort)PacketID.SC_Error, MakePacket<SC_Error>);
		_handler.Add((ushort)PacketID.SC_Error, PacketHandler.SC_ErrorHandler);
		_makeFunc.Add((ushort)PacketID.SC_Warn, MakePacket<SC_Warn>);
		_handler.Add((ushort)PacketID.SC_Warn, PacketHandler.SC_WarnHandler);
		_makeFunc.Add((ushort)PacketID.SC_Pong, MakePacket<SC_Pong>);
		_handler.Add((ushort)PacketID.SC_Pong, PacketHandler.SC_PongHandler);
		_makeFunc.Add((ushort)PacketID.SC_InitGame, MakePacket<SC_InitGame>);
		_handler.Add((ushort)PacketID.SC_InitGame, PacketHandler.SC_InitGameHandler);
		_makeFunc.Add((ushort)PacketID.SC_BeatSync, MakePacket<SC_BeatSync>);
		_handler.Add((ushort)PacketID.SC_BeatSync, PacketHandler.SC_BeatSyncHandler);
		_makeFunc.Add((ushort)PacketID.SC_BeatActions, MakePacket<SC_BeatActions>);
		_handler.Add((ushort)PacketID.SC_BeatActions, PacketHandler.SC_BeatActionsHandler);
		_makeFunc.Add((ushort)PacketID.SC_BeatTelegraphs, MakePacket<SC_BeatTelegraphs>);
		_handler.Add((ushort)PacketID.SC_BeatTelegraphs, PacketHandler.SC_BeatTelegraphsHandler);

	}

	public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer, Action<PacketSession, IPacket> onRecvCallback = null )
	{
		ushort count = 0;

		ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
		count += 2;
		ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
		count += 2;

		Func < PacketSession, ArraySegment<byte>, IPacket > func = null;
		if (_makeFunc.TryGetValue(id, out func))
		{
            IPacket packet = func.Invoke(session, buffer);
			if (onRecvCallback != null)
				onRecvCallback.Invoke(session, packet);
			else
				HandlePacket(session, packet);
        }
	}

	T MakePacket<T>(PacketSession session, ArraySegment<byte> buffer) where T : IPacket, new()
	{
		T pkt = new T();
		pkt.Read(buffer);
		return pkt;
	}

	public void HandlePacket(PacketSession session, IPacket packet)
	{
        Action<PacketSession, IPacket> action = null;
        if (_handler.TryGetValue(packet.Protocol, out action))
            action.Invoke(session, packet);

    }
}
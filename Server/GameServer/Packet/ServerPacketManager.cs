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
		_makeFunc.Add((ushort)PacketID.CS_Handshake, MakePacket<CS_Handshake>);
		_handler.Add((ushort)PacketID.CS_Handshake, PacketHandler.CS_HandshakeHandler);
		_makeFunc.Add((ushort)PacketID.CS_MapEnter, MakePacket<CS_MapEnter>);
		_handler.Add((ushort)PacketID.CS_MapEnter, PacketHandler.CS_MapEnterHandler);
		_makeFunc.Add((ushort)PacketID.CS_Ready, MakePacket<CS_Ready>);
		_handler.Add((ushort)PacketID.CS_Ready, PacketHandler.CS_ReadyHandler);
		_makeFunc.Add((ushort)PacketID.CS_TownActionRequest, MakePacket<CS_TownActionRequest>);
		_handler.Add((ushort)PacketID.CS_TownActionRequest, PacketHandler.CS_TownActionRequestHandler);
		_makeFunc.Add((ushort)PacketID.CS_Ping, MakePacket<CS_Ping>);
		_handler.Add((ushort)PacketID.CS_Ping, PacketHandler.CS_PingHandler);
		_makeFunc.Add((ushort)PacketID.CS_CalibHit, MakePacket<CS_CalibHit>);
		_handler.Add((ushort)PacketID.CS_CalibHit, PacketHandler.CS_CalibHitHandler);
		_makeFunc.Add((ushort)PacketID.CS_ActionRequest, MakePacket<CS_ActionRequest>);
		_handler.Add((ushort)PacketID.CS_ActionRequest, PacketHandler.CS_ActionRequestHandler);

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
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using ServerCore;

public enum PacketID
{
	CS_Handshake = 1,
	SC_HandshakeOk = 2,
	SC_HandshakeFail = 3,
	SC_ForcedDisconnect = 4,
	CS_MapEnter = 5,
	SC_InitMap = 6,
	CS_Ready = 7,
	SC_ReadyAck = 8,
	CS_TownActionRequest = 9,
	SC_TownBeatActions = 10,
	SC_AllPlayersLoaded = 11,
	SC_GameBegin = 12,
	SC_Error = 13,
	SC_Warn = 14,
	CS_Ping = 15,
	SC_Pong = 16,
	SC_ReturnToTown = 17,
	SC_InitGame = 18,
	SC_CalibResult = 19,
	CS_CalibHit = 20,
	SC_BeatSync = 21,
	CS_ActionRequest = 22,
	SC_ActionInstantBroadcast = 23,
	SC_CancelAction = 24,
	SC_BeatActions = 25,
	SC_BeatTelegraphs = 26,
	SC_EntityDespawn = 27,
	SC_EntitySpawn = 28,
	CS_GetInventory = 29,
	SC_Inventory = 30,
	CS_EquipItem = 31,
	SC_EquipResult = 32,
	CS_Cheat = 33,
	CS_DestroyItem = 34,
	
}

public  interface IPacket
{
	ushort Protocol { get; }
	void Read(ArraySegment<byte> segment);
	ArraySegment<byte> Write();
}


public class CS_Handshake : IPacket
{
	public string TicketId;
	public string ClientNonce;
	public int ClientVersion;
	public int Target;
	public string Key;

	public ushort Protocol { get { return (ushort)PacketID.CS_Handshake; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		ushort TicketIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.TicketId = Encoding.Unicode.GetString(s.Slice(count, TicketIdLen));
		count += TicketIdLen;
		ushort ClientNonceLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.ClientNonce = Encoding.Unicode.GetString(s.Slice(count, ClientNonceLen));
		count += ClientNonceLen;
		this.ClientVersion = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Target = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort KeyLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.Key = Encoding.Unicode.GetString(s.Slice(count, KeyLen));
		count += KeyLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_Handshake);
		count += sizeof(ushort);
		ushort TicketIdLen = (ushort)Encoding.Unicode.GetBytes(this.TicketId, 0, this.TicketId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), TicketIdLen);
		count += sizeof(ushort);
		count += TicketIdLen;
		ushort ClientNonceLen = (ushort)Encoding.Unicode.GetBytes(this.ClientNonce, 0, this.ClientNonce.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ClientNonceLen);
		count += sizeof(ushort);
		count += ClientNonceLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientVersion);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Target);
		count += sizeof(int);
		ushort KeyLen = (ushort)Encoding.Unicode.GetBytes(this.Key, 0, this.Key.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), KeyLen);
		count += sizeof(ushort);
		count += KeyLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_HandshakeOk : IPacket
{
	public string Uid;
	public long ServerTimeMs;
	public long SessionEpoch;
	public int ServerRole;

	public ushort Protocol { get { return (ushort)PacketID.SC_HandshakeOk; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		ushort UidLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.Uid = Encoding.Unicode.GetString(s.Slice(count, UidLen));
		count += UidLen;
		this.ServerTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.SessionEpoch = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.ServerRole = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_HandshakeOk);
		count += sizeof(ushort);
		ushort UidLen = (ushort)Encoding.Unicode.GetBytes(this.Uid, 0, this.Uid.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), UidLen);
		count += sizeof(ushort);
		count += UidLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ServerTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.SessionEpoch);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ServerRole);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_HandshakeFail : IPacket
{
	public int errorCode;
	public string message;

	public ushort Protocol { get { return (ushort)PacketID.SC_HandshakeFail; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.errorCode = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort messageLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.message = Encoding.Unicode.GetString(s.Slice(count, messageLen));
		count += messageLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_HandshakeFail);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.errorCode);
		count += sizeof(int);
		ushort messageLen = (ushort)Encoding.Unicode.GetBytes(this.message, 0, this.message.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), messageLen);
		count += sizeof(ushort);
		count += messageLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_ForcedDisconnect : IPacket
{
	public int Reason;
	public long NewEpoch;
	public string Message;

	public ushort Protocol { get { return (ushort)PacketID.SC_ForcedDisconnect; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.Reason = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.NewEpoch = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		ushort MessageLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.Message = Encoding.Unicode.GetString(s.Slice(count, MessageLen));
		count += MessageLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_ForcedDisconnect);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Reason);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.NewEpoch);
		count += sizeof(long);
		ushort MessageLen = (ushort)Encoding.Unicode.GetBytes(this.Message, 0, this.Message.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), MessageLen);
		count += sizeof(ushort);
		count += MessageLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_MapEnter : IPacket
{
	public long ClientTimeMs;
	public string MapId;
	public int LastKnownRevision;
	public bool WantSnapshot;
	public int MaxPlayers;

	public ushort Protocol { get { return (ushort)PacketID.CS_MapEnter; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ClientTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		ushort MapIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.MapId = Encoding.Unicode.GetString(s.Slice(count, MapIdLen));
		count += MapIdLen;
		this.LastKnownRevision = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.WantSnapshot = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
		count += sizeof(bool);
		this.MaxPlayers = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_MapEnter);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientTimeMs);
		count += sizeof(long);
		ushort MapIdLen = (ushort)Encoding.Unicode.GetBytes(this.MapId, 0, this.MapId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), MapIdLen);
		count += sizeof(ushort);
		count += MapIdLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.LastKnownRevision);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.WantSnapshot);
		count += sizeof(bool);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MaxPlayers);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_InitMap : IPacket
{
	public long ServerTimeMs;
	public int Revision;
	public int TickRate;
	public string MapId;
	public int MapWidth;
	public int MapHeight;
	public int MapVersion;
	public int Mode;
	public int MyActorId;
	public class Players
	{
		public string Uid;
		public int ActorId;
		public string Name;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			ushort UidLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.Uid = Encoding.Unicode.GetString(s.Slice(count, UidLen));
			count += UidLen;
			this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			ushort NameLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.Name = Encoding.Unicode.GetString(s.Slice(count, NameLen));
			count += NameLen;
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			ushort UidLen = (ushort)Encoding.Unicode.GetBytes(this.Uid, 0, this.Uid.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), UidLen);
			count += sizeof(ushort);
			count += UidLen;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
			count += sizeof(int);
			ushort NameLen = (ushort)Encoding.Unicode.GetBytes(this.Name, 0, this.Name.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), NameLen);
			count += sizeof(ushort);
			count += NameLen;
			return success;
		}	
	}
	public List<Players> playerss = new List<Players>();
	public class Entities
	{
		public int EntityId;
		public int EntityType;
		public int OwnerSlot;
		public int X;
		public int Y;
		public int Dir;
		public int Hp;
		public int AppearanceId;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.EntityId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.EntityType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.OwnerSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.X = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Dir = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Hp = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.AppearanceId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityType);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.OwnerSlot);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.X);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Y);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Dir);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Hp);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.AppearanceId);
			count += sizeof(int);
			return success;
		}	
	}
	public List<Entities> entitiess = new List<Entities>();
	public double ActionWindowMs;
	public string SongId;
	public double Bpm;
	public int BaseBeatDivision;
	public long SongStartServerTime;

	public ushort Protocol { get { return (ushort)PacketID.SC_InitMap; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ServerTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.Revision = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.TickRate = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort MapIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.MapId = Encoding.Unicode.GetString(s.Slice(count, MapIdLen));
		count += MapIdLen;
		this.MapWidth = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.MapHeight = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.MapVersion = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Mode = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.MyActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.playerss.Clear();
		ushort playersLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < playersLen; i++)
		{
			Players players = new Players();
			players.Read(s, ref count);
			playerss.Add(players);
		}
		this.entitiess.Clear();
		ushort entitiesLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < entitiesLen; i++)
		{
			Entities entities = new Entities();
			entities.Read(s, ref count);
			entitiess.Add(entities);
		}
		this.ActionWindowMs = BitConverter.ToDouble(s.Slice(count, s.Length - count));
		count += sizeof(double);
		ushort SongIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.SongId = Encoding.Unicode.GetString(s.Slice(count, SongIdLen));
		count += SongIdLen;
		this.Bpm = BitConverter.ToDouble(s.Slice(count, s.Length - count));
		count += sizeof(double);
		this.BaseBeatDivision = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.SongStartServerTime = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_InitMap);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ServerTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Revision);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TickRate);
		count += sizeof(int);
		ushort MapIdLen = (ushort)Encoding.Unicode.GetBytes(this.MapId, 0, this.MapId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), MapIdLen);
		count += sizeof(ushort);
		count += MapIdLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MapWidth);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MapHeight);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MapVersion);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Mode);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MyActorId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.playerss.Count);
		count += sizeof(ushort);
		foreach (Players players in this.playerss)
			success &= players.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.entitiess.Count);
		count += sizeof(ushort);
		foreach (Entities entities in this.entitiess)
			success &= entities.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActionWindowMs);
		count += sizeof(double);
		ushort SongIdLen = (ushort)Encoding.Unicode.GetBytes(this.SongId, 0, this.SongId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), SongIdLen);
		count += sizeof(ushort);
		count += SongIdLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Bpm);
		count += sizeof(double);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BaseBeatDivision);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.SongStartServerTime);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_Ready : IPacket
{
	public int Revision;
	public long ClientTimeMs;

	public ushort Protocol { get { return (ushort)PacketID.CS_Ready; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.Revision = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.ClientTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_Ready);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Revision);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_ReadyAck : IPacket
{
	public int Revision;
	public long ServerTimeMs;

	public ushort Protocol { get { return (ushort)PacketID.SC_ReadyAck; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.Revision = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.ServerTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_ReadyAck);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Revision);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ServerTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_TownActionRequest : IPacket
{
	public int ActorId;
	public int ActionKind;
	public int TargetX;
	public int TargetY;
	public float Rotation;
	public long ClientSendTimeMs;

	public ushort Protocol { get { return (ushort)PacketID.CS_TownActionRequest; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.ActionKind = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.TargetX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.TargetY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Rotation = BitConverter.ToSingle(s.Slice(count, s.Length - count));
		count += sizeof(float);
		this.ClientSendTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_TownActionRequest);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActionKind);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TargetX);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TargetY);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Rotation);
		count += sizeof(float);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientSendTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_TownBeatActions : IPacket
{
	public long BeatIndex;
	public class BeatActionResult
	{
		public int ActorId;
		public int ActionKind;
		public int FromX;
		public int FromY;
		public int ToX;
		public int ToY;
		public float Rotation;
		public bool Accepted;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ActionKind = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.FromX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.FromY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ToX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ToY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Rotation = BitConverter.ToSingle(s.Slice(count, s.Length - count));
			count += sizeof(float);
			this.Accepted = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
			count += sizeof(bool);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActionKind);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.FromX);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.FromY);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ToX);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ToY);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Rotation);
			count += sizeof(float);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Accepted);
			count += sizeof(bool);
			return success;
		}	
	}
	public List<BeatActionResult> beatActionResults = new List<BeatActionResult>();

	public ushort Protocol { get { return (ushort)PacketID.SC_TownBeatActions; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.beatActionResults.Clear();
		ushort beatActionResultLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < beatActionResultLen; i++)
		{
			BeatActionResult beatActionResult = new BeatActionResult();
			beatActionResult.Read(s, ref count);
			beatActionResults.Add(beatActionResult);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_TownBeatActions);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.beatActionResults.Count);
		count += sizeof(ushort);
		foreach (BeatActionResult beatActionResult in this.beatActionResults)
			success &= beatActionResult.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_AllPlayersLoaded : IPacket
{
	public string matchId;
	public class Players
	{
		public string uid;
		public int slot;
		public bool loaded;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			ushort uidLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.uid = Encoding.Unicode.GetString(s.Slice(count, uidLen));
			count += uidLen;
			this.slot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.loaded = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
			count += sizeof(bool);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			ushort uidLen = (ushort)Encoding.Unicode.GetBytes(this.uid, 0, this.uid.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), uidLen);
			count += sizeof(ushort);
			count += uidLen;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.slot);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.loaded);
			count += sizeof(bool);
			return success;
		}	
	}
	public List<Players> playerss = new List<Players>();

	public ushort Protocol { get { return (ushort)PacketID.SC_AllPlayersLoaded; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		ushort matchIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.matchId = Encoding.Unicode.GetString(s.Slice(count, matchIdLen));
		count += matchIdLen;
		this.playerss.Clear();
		ushort playersLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < playersLen; i++)
		{
			Players players = new Players();
			players.Read(s, ref count);
			playerss.Add(players);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_AllPlayersLoaded);
		count += sizeof(ushort);
		ushort matchIdLen = (ushort)Encoding.Unicode.GetBytes(this.matchId, 0, this.matchId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), matchIdLen);
		count += sizeof(ushort);
		count += matchIdLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.playerss.Count);
		count += sizeof(ushort);
		foreach (Players players in this.playerss)
			success &= players.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_GameBegin : IPacket
{
	public string matchId;
	public long startAtMs;
	public int startTick;

	public ushort Protocol { get { return (ushort)PacketID.SC_GameBegin; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		ushort matchIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.matchId = Encoding.Unicode.GetString(s.Slice(count, matchIdLen));
		count += matchIdLen;
		this.startAtMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.startTick = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_GameBegin);
		count += sizeof(ushort);
		ushort matchIdLen = (ushort)Encoding.Unicode.GetBytes(this.matchId, 0, this.matchId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), matchIdLen);
		count += sizeof(ushort);
		count += matchIdLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.startAtMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.startTick);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_Error : IPacket
{
	public int code;
	public string message;

	public ushort Protocol { get { return (ushort)PacketID.SC_Error; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.code = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort messageLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.message = Encoding.Unicode.GetString(s.Slice(count, messageLen));
		count += messageLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_Error);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.code);
		count += sizeof(int);
		ushort messageLen = (ushort)Encoding.Unicode.GetBytes(this.message, 0, this.message.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), messageLen);
		count += sizeof(ushort);
		count += messageLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_Warn : IPacket
{
	public int code;
	public string msg;

	public ushort Protocol { get { return (ushort)PacketID.SC_Warn; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.code = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort msgLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.msg = Encoding.Unicode.GetString(s.Slice(count, msgLen));
		count += msgLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_Warn);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.code);
		count += sizeof(int);
		ushort msgLen = (ushort)Encoding.Unicode.GetBytes(this.msg, 0, this.msg.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), msgLen);
		count += sizeof(ushort);
		count += msgLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_Ping : IPacket
{
	public int seq;
	public long clientSendMs;

	public ushort Protocol { get { return (ushort)PacketID.CS_Ping; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.seq = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.clientSendMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_Ping);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.seq);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.clientSendMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_Pong : IPacket
{
	public int seq;
	public long clientSendMs;
	public long serverRecvMs;
	public long serverSendMs;

	public ushort Protocol { get { return (ushort)PacketID.SC_Pong; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.seq = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.clientSendMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.serverRecvMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.serverSendMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_Pong);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.seq);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.clientSendMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.serverRecvMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.serverSendMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_ReturnToTown : IPacket
{
	

	public ushort Protocol { get { return (ushort)PacketID.SC_ReturnToTown; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_ReturnToTown);
		count += sizeof(ushort);
		
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_InitGame : IPacket
{
	public double ActionWindowMs;
	public int MapWidth;
	public int MapHeight;
	public string MapName;
	public class PlayerActorIds
	{
		public int ActorId;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
			count += sizeof(int);
			return success;
		}	
	}
	public List<PlayerActorIds> playerActorIdss = new List<PlayerActorIds>();
	public int MyActorId;
	public class SpawnEntities
	{
		public int EntityId;
		public int EntityType;
		public int OwnerSlot;
		public int X;
		public int Y;
		public int Hp;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.EntityId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.EntityType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.OwnerSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.X = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Hp = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityType);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.OwnerSlot);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.X);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Y);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Hp);
			count += sizeof(int);
			return success;
		}	
	}
	public List<SpawnEntities> spawnEntitiess = new List<SpawnEntities>();

	public ushort Protocol { get { return (ushort)PacketID.SC_InitGame; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ActionWindowMs = BitConverter.ToDouble(s.Slice(count, s.Length - count));
		count += sizeof(double);
		this.MapWidth = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.MapHeight = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort MapNameLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.MapName = Encoding.Unicode.GetString(s.Slice(count, MapNameLen));
		count += MapNameLen;
		this.playerActorIdss.Clear();
		ushort playerActorIdsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < playerActorIdsLen; i++)
		{
			PlayerActorIds playerActorIds = new PlayerActorIds();
			playerActorIds.Read(s, ref count);
			playerActorIdss.Add(playerActorIds);
		}
		this.MyActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.spawnEntitiess.Clear();
		ushort spawnEntitiesLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < spawnEntitiesLen; i++)
		{
			SpawnEntities spawnEntities = new SpawnEntities();
			spawnEntities.Read(s, ref count);
			spawnEntitiess.Add(spawnEntities);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_InitGame);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActionWindowMs);
		count += sizeof(double);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MapWidth);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MapHeight);
		count += sizeof(int);
		ushort MapNameLen = (ushort)Encoding.Unicode.GetBytes(this.MapName, 0, this.MapName.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), MapNameLen);
		count += sizeof(ushort);
		count += MapNameLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.playerActorIdss.Count);
		count += sizeof(ushort);
		foreach (PlayerActorIds playerActorIds in this.playerActorIdss)
			success &= playerActorIds.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.MyActorId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.spawnEntitiess.Count);
		count += sizeof(ushort);
		foreach (SpawnEntities spawnEntities in this.spawnEntitiess)
			success &= spawnEntities.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_CalibResult : IPacket
{
	public int DiffMs;
	public long BeatIndex;
	public long ServerNowMs;

	public ushort Protocol { get { return (ushort)PacketID.SC_CalibResult; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.DiffMs = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.ServerNowMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_CalibResult);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.DiffMs);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ServerNowMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_CalibHit : IPacket
{
	public long ClientSendTimeMs;
	public long BeatIndex;

	public ushort Protocol { get { return (ushort)PacketID.CS_CalibHit; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ClientSendTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_CalibHit);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientSendTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_BeatSync : IPacket
{
	public long ServerSendTimeMs;
	public long ClientSendTimeMs;
	public long SongStartServerTimeMs;
	public double Bpm;
	public int BaseBeatDivision;
	public long BeatIndex;

	public ushort Protocol { get { return (ushort)PacketID.SC_BeatSync; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ServerSendTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.ClientSendTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.SongStartServerTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.Bpm = BitConverter.ToDouble(s.Slice(count, s.Length - count));
		count += sizeof(double);
		this.BaseBeatDivision = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_BeatSync);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ServerSendTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientSendTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.SongStartServerTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Bpm);
		count += sizeof(double);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BaseBeatDivision);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_ActionRequest : IPacket
{
	public int ActorId;
	public int ActionKind;
	public int SlotIndex;
	public int TargetX;
	public int TargetY;
	public float Rotation;
	public long ClientSendTimeMs;

	public ushort Protocol { get { return (ushort)PacketID.CS_ActionRequest; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.ActionKind = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.SlotIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.TargetX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.TargetY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Rotation = BitConverter.ToSingle(s.Slice(count, s.Length - count));
		count += sizeof(float);
		this.ClientSendTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_ActionRequest);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActionKind);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.SlotIndex);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TargetX);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TargetY);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Rotation);
		count += sizeof(float);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientSendTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_ActionInstantBroadcast : IPacket
{
	public int ActorId;
	public int ActionKind;
	public string SkillId;
	public float Rotation;
	public long StartTick;

	public ushort Protocol { get { return (ushort)PacketID.SC_ActionInstantBroadcast; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.ActionKind = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort SkillIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.SkillId = Encoding.Unicode.GetString(s.Slice(count, SkillIdLen));
		count += SkillIdLen;
		this.Rotation = BitConverter.ToSingle(s.Slice(count, s.Length - count));
		count += sizeof(float);
		this.StartTick = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_ActionInstantBroadcast);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActionKind);
		count += sizeof(int);
		ushort SkillIdLen = (ushort)Encoding.Unicode.GetBytes(this.SkillId, 0, this.SkillId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), SkillIdLen);
		count += sizeof(ushort);
		count += SkillIdLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Rotation);
		count += sizeof(float);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.StartTick);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_CancelAction : IPacket
{
	public int ActorId;

	public ushort Protocol { get { return (ushort)PacketID.SC_CancelAction; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_CancelAction);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_BeatActions : IPacket
{
	public long BeatIndex;
	public class BeatActionResult
	{
		public int ActorId;
		public int ActionKind;
		public int FromX;
		public int FromY;
		public int ToX;
		public int ToY;
		public float Rotation;
		public bool Accepted;
		public class HpUpdate
		{
			public int EntityId;
			public int NewHp;
		
			public void Read(ReadOnlySpan<byte> s, ref ushort count)
			{
				this.EntityId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
				count += sizeof(int);
				this.NewHp = BitConverter.ToInt32(s.Slice(count, s.Length - count));
				count += sizeof(int);
			}
		
			public bool Write(Span<byte> s, ref ushort count)
			{
				ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		
				bool success = true;
				success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityId);
				count += sizeof(int);
				success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.NewHp);
				count += sizeof(int);
				return success;
			}	
		}
		public List<HpUpdate> hpUpdates = new List<HpUpdate>();
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ActionKind = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.FromX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.FromY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ToX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ToY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Rotation = BitConverter.ToSingle(s.Slice(count, s.Length - count));
			count += sizeof(float);
			this.Accepted = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
			count += sizeof(bool);
			this.hpUpdates.Clear();
			ushort hpUpdateLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			for (int i = 0; i < hpUpdateLen; i++)
			{
				HpUpdate hpUpdate = new HpUpdate();
				hpUpdate.Read(s, ref count);
				hpUpdates.Add(hpUpdate);
			}
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActorId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ActionKind);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.FromX);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.FromY);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ToX);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ToY);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Rotation);
			count += sizeof(float);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Accepted);
			count += sizeof(bool);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.hpUpdates.Count);
			count += sizeof(ushort);
			foreach (HpUpdate hpUpdate in this.hpUpdates)
				success &= hpUpdate.Write(s, ref count);
			return success;
		}	
	}
	public List<BeatActionResult> beatActionResults = new List<BeatActionResult>();

	public ushort Protocol { get { return (ushort)PacketID.SC_BeatActions; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.beatActionResults.Clear();
		ushort beatActionResultLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < beatActionResultLen; i++)
		{
			BeatActionResult beatActionResult = new BeatActionResult();
			beatActionResult.Read(s, ref count);
			beatActionResults.Add(beatActionResult);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_BeatActions);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.beatActionResults.Count);
		count += sizeof(ushort);
		foreach (BeatActionResult beatActionResult in this.beatActionResults)
			success &= beatActionResult.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_BeatTelegraphs : IPacket
{
	public long BeatIndex;
	public class Telegraphs
	{
		public int CasterId;
		public int StyleId;
		public int DurationTicks;
		public int Shape;
		public int OriginType;
		public int OriginX;
		public int OriginY;
		public int ParamA;
		public int ParamB;
		public class Cells
		{
			public int X;
			public int Y;
		
			public void Read(ReadOnlySpan<byte> s, ref ushort count)
			{
				this.X = BitConverter.ToInt32(s.Slice(count, s.Length - count));
				count += sizeof(int);
				this.Y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
				count += sizeof(int);
			}
		
			public bool Write(Span<byte> s, ref ushort count)
			{
				ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		
				bool success = true;
				success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.X);
				count += sizeof(int);
				success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Y);
				count += sizeof(int);
				return success;
			}	
		}
		public List<Cells> cellss = new List<Cells>();
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.CasterId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.StyleId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.DurationTicks = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Shape = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.OriginType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.OriginX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.OriginY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ParamA = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.ParamB = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.cellss.Clear();
			ushort cellsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			for (int i = 0; i < cellsLen; i++)
			{
				Cells cells = new Cells();
				cells.Read(s, ref count);
				cellss.Add(cells);
			}
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.CasterId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.StyleId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.DurationTicks);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Shape);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.OriginType);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.OriginX);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.OriginY);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ParamA);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ParamB);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.cellss.Count);
			count += sizeof(ushort);
			foreach (Cells cells in this.cellss)
				success &= cells.Write(s, ref count);
			return success;
		}	
	}
	public List<Telegraphs> telegraphss = new List<Telegraphs>();

	public ushort Protocol { get { return (ushort)PacketID.SC_BeatTelegraphs; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.telegraphss.Clear();
		ushort telegraphsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < telegraphsLen; i++)
		{
			Telegraphs telegraphs = new Telegraphs();
			telegraphs.Read(s, ref count);
			telegraphss.Add(telegraphs);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_BeatTelegraphs);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.telegraphss.Count);
		count += sizeof(ushort);
		foreach (Telegraphs telegraphs in this.telegraphss)
			success &= telegraphs.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_EntityDespawn : IPacket
{
	public long BeatIndex;
	public int EntityId;

	public ushort Protocol { get { return (ushort)PacketID.SC_EntityDespawn; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.EntityId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_EntityDespawn);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_EntitySpawn : IPacket
{
	public long BeatIndex;
	public int EntityId;
	public int EntityType;
	public int X;
	public int Y;
	public int Hp;
	public int AppearanceId;

	public ushort Protocol { get { return (ushort)PacketID.SC_EntitySpawn; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.BeatIndex = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.EntityId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.EntityType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.X = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Hp = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.AppearanceId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_EntitySpawn);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.BeatIndex);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EntityType);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.X);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Y);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Hp);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.AppearanceId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_GetInventory : IPacket
{
	

	public ushort Protocol { get { return (ushort)PacketID.CS_GetInventory; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_GetInventory);
		count += sizeof(ushort);
		
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_Inventory : IPacket
{
	public class Items
	{
		public long InstanceId;
		public int TemplateId;
		public int Amount;
		public int SlotIndex;
		public string AcquiredAt;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.InstanceId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
			count += sizeof(long);
			this.TemplateId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.Amount = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.SlotIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			ushort AcquiredAtLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.AcquiredAt = Encoding.Unicode.GetString(s.Slice(count, AcquiredAtLen));
			count += AcquiredAtLen;
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.InstanceId);
			count += sizeof(long);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TemplateId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Amount);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.SlotIndex);
			count += sizeof(int);
			ushort AcquiredAtLen = (ushort)Encoding.Unicode.GetBytes(this.AcquiredAt, 0, this.AcquiredAt.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), AcquiredAtLen);
			count += sizeof(ushort);
			count += AcquiredAtLen;
			return success;
		}	
	}
	public List<Items> itemss = new List<Items>();
	public class Equipments
	{
		public long InstanceId;
		public int TemplateId;
		public int SlotIndex;
		public int EnhancementLevel;
		public bool IsEquipped;
		public string BaseStats;
		public string RandomOptions;
		public string AcquiredAt;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.InstanceId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
			count += sizeof(long);
			this.TemplateId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.SlotIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.EnhancementLevel = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.IsEquipped = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
			count += sizeof(bool);
			ushort BaseStatsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.BaseStats = Encoding.Unicode.GetString(s.Slice(count, BaseStatsLen));
			count += BaseStatsLen;
			ushort RandomOptionsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.RandomOptions = Encoding.Unicode.GetString(s.Slice(count, RandomOptionsLen));
			count += RandomOptionsLen;
			ushort AcquiredAtLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.AcquiredAt = Encoding.Unicode.GetString(s.Slice(count, AcquiredAtLen));
			count += AcquiredAtLen;
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.InstanceId);
			count += sizeof(long);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TemplateId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.SlotIndex);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.EnhancementLevel);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.IsEquipped);
			count += sizeof(bool);
			ushort BaseStatsLen = (ushort)Encoding.Unicode.GetBytes(this.BaseStats, 0, this.BaseStats.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), BaseStatsLen);
			count += sizeof(ushort);
			count += BaseStatsLen;
			ushort RandomOptionsLen = (ushort)Encoding.Unicode.GetBytes(this.RandomOptions, 0, this.RandomOptions.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), RandomOptionsLen);
			count += sizeof(ushort);
			count += RandomOptionsLen;
			ushort AcquiredAtLen = (ushort)Encoding.Unicode.GetBytes(this.AcquiredAt, 0, this.AcquiredAt.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), AcquiredAtLen);
			count += sizeof(ushort);
			count += AcquiredAtLen;
			return success;
		}	
	}
	public List<Equipments> equipmentss = new List<Equipments>();

	public ushort Protocol { get { return (ushort)PacketID.SC_Inventory; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.itemss.Clear();
		ushort itemsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < itemsLen; i++)
		{
			Items items = new Items();
			items.Read(s, ref count);
			itemss.Add(items);
		}
		this.equipmentss.Clear();
		ushort equipmentsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < equipmentsLen; i++)
		{
			Equipments equipments = new Equipments();
			equipments.Read(s, ref count);
			equipmentss.Add(equipments);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_Inventory);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.itemss.Count);
		count += sizeof(ushort);
		foreach (Items items in this.itemss)
			success &= items.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.equipmentss.Count);
		count += sizeof(ushort);
		foreach (Equipments equipments in this.equipmentss)
			success &= equipments.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_EquipItem : IPacket
{
	public long InstanceId;
	public bool Equip;

	public ushort Protocol { get { return (ushort)PacketID.CS_EquipItem; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.InstanceId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.Equip = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
		count += sizeof(bool);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_EquipItem);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.InstanceId);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Equip);
		count += sizeof(bool);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_EquipResult : IPacket
{
	public bool Success;
	public long InstanceId;
	public bool Equipped;

	public ushort Protocol { get { return (ushort)PacketID.SC_EquipResult; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.Success = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
		count += sizeof(bool);
		this.InstanceId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.Equipped = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
		count += sizeof(bool);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_EquipResult);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Success);
		count += sizeof(bool);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.InstanceId);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Equipped);
		count += sizeof(bool);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_Cheat : IPacket
{
	public int CheatCode;
	public int Param1;
	public int Param2;

	public ushort Protocol { get { return (ushort)PacketID.CS_Cheat; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.CheatCode = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Param1 = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Param2 = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_Cheat);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.CheatCode);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Param1);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Param2);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_DestroyItem : IPacket
{
	public long InstanceId;
	public int Amount;

	public ushort Protocol { get { return (ushort)PacketID.CS_DestroyItem; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.InstanceId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.Amount = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_DestroyItem);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.InstanceId);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Amount);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}


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
	SC_AllPlayersLoaded = 5,
	SC_GameBegin = 6,
	SC_Error = 7,
	SC_Warn = 8,
	CS_Ping = 9,
	SC_Pong = 10,
	SC_InitGame = 11,
	SC_CalibResult = 12,
	CS_CalibHit = 13,
	SC_BeatSync = 14,
	CS_ActionRequest = 15,
	SC_BeatActions = 16,
	SC_BeatTelegraphs = 17,
	SC_EntityDespawn = 18,
	
}

public  interface IPacket
{
	ushort Protocol { get; }
	void Read(ArraySegment<byte> segment);
	ArraySegment<byte> Write();
}


public class CS_Handshake : IPacket
{
	public string ticketId;
	public string clientNonce;

	public ushort Protocol { get { return (ushort)PacketID.CS_Handshake; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		ushort ticketIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.ticketId = Encoding.Unicode.GetString(s.Slice(count, ticketIdLen));
		count += ticketIdLen;
		ushort clientNonceLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.clientNonce = Encoding.Unicode.GetString(s.Slice(count, clientNonceLen));
		count += clientNonceLen;
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
		ushort ticketIdLen = (ushort)Encoding.Unicode.GetBytes(this.ticketId, 0, this.ticketId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ticketIdLen);
		count += sizeof(ushort);
		count += ticketIdLen;
		ushort clientNonceLen = (ushort)Encoding.Unicode.GetBytes(this.clientNonce, 0, this.clientNonce.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), clientNonceLen);
		count += sizeof(ushort);
		count += clientNonceLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_HandshakeOk : IPacket
{
	public int State;
	public long Epoch;
	public string Uid;
	public string RoomId;
	public long ServerNowMs;
	public int LeaseTtlSec;

	public ushort Protocol { get { return (ushort)PacketID.SC_HandshakeOk; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.State = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.Epoch = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		ushort UidLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.Uid = Encoding.Unicode.GetString(s.Slice(count, UidLen));
		count += UidLen;
		ushort RoomIdLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.RoomId = Encoding.Unicode.GetString(s.Slice(count, RoomIdLen));
		count += RoomIdLen;
		this.ServerNowMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.LeaseTtlSec = BitConverter.ToInt32(s.Slice(count, s.Length - count));
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
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.State);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.Epoch);
		count += sizeof(long);
		ushort UidLen = (ushort)Encoding.Unicode.GetBytes(this.Uid, 0, this.Uid.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), UidLen);
		count += sizeof(ushort);
		count += UidLen;
		ushort RoomIdLen = (ushort)Encoding.Unicode.GetBytes(this.RoomId, 0, this.RoomId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), RoomIdLen);
		count += sizeof(ushort);
		count += RoomIdLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ServerNowMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.LeaseTtlSec);
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
	public int TargetX;
	public int TargetY;
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
		this.TargetX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.TargetY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
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
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TargetX);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.TargetY);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.ClientSendTimeMs);
		count += sizeof(long);
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
		public int DurationBeats;
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
			this.DurationBeats = BitConverter.ToInt32(s.Slice(count, s.Length - count));
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
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.DurationBeats);
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


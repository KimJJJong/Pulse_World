using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using ServerCore;

public enum PacketID
{
	CS_JoinGame = 1,
	SC_Welcome = 2,
	CS_Loaded = 3,
	SC_AllPlayersLoaded = 4,
	SC_GameBegin = 5,
	SC_Error = 6,
	SC_Warn = 7,
	CS_Ping = 8,
	SC_Pong = 9,
	SC_BeatSync = 10,
	CS_Input = 11,
	SC_State = 12,
	SC_MakeMapData = 13,
	SC_GameStartWithRollDice = 14,
	CS_FirstDiceResult = 15,
	SC_RollDiceForTurn = 16,
	CS_RollDicePieceMove = 17,
	SC_PieceMoveConfirm = 18,
	SC_FightConfirm = 19,
	CS_BattleCardDraw = 20,
	SC_BattleResult = 21,
	SC_TurnEnd = 22,
	SC_WhoIsWinner = 23,
	SC_StateSnapshot = 24,
	
}

public  interface IPacket
{
	ushort Protocol { get; }
	void Read(ArraySegment<byte> segment);
	ArraySegment<byte> Write();
}


public class CS_JoinGame : IPacket
{
	public string matchId;
	public string uid;
	public string ticket;
	public int protoVer;
	public string nonce;
	public string clientVer;
	public string platform;

	public ushort Protocol { get { return (ushort)PacketID.CS_JoinGame; } }

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
		ushort uidLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.uid = Encoding.Unicode.GetString(s.Slice(count, uidLen));
		count += uidLen;
		ushort ticketLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.ticket = Encoding.Unicode.GetString(s.Slice(count, ticketLen));
		count += ticketLen;
		this.protoVer = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort nonceLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.nonce = Encoding.Unicode.GetString(s.Slice(count, nonceLen));
		count += nonceLen;
		ushort clientVerLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.clientVer = Encoding.Unicode.GetString(s.Slice(count, clientVerLen));
		count += clientVerLen;
		ushort platformLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.platform = Encoding.Unicode.GetString(s.Slice(count, platformLen));
		count += platformLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_JoinGame);
		count += sizeof(ushort);
		ushort matchIdLen = (ushort)Encoding.Unicode.GetBytes(this.matchId, 0, this.matchId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), matchIdLen);
		count += sizeof(ushort);
		count += matchIdLen;
		ushort uidLen = (ushort)Encoding.Unicode.GetBytes(this.uid, 0, this.uid.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), uidLen);
		count += sizeof(ushort);
		count += uidLen;
		ushort ticketLen = (ushort)Encoding.Unicode.GetBytes(this.ticket, 0, this.ticket.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ticketLen);
		count += sizeof(ushort);
		count += ticketLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.protoVer);
		count += sizeof(int);
		ushort nonceLen = (ushort)Encoding.Unicode.GetBytes(this.nonce, 0, this.nonce.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nonceLen);
		count += sizeof(ushort);
		count += nonceLen;
		ushort clientVerLen = (ushort)Encoding.Unicode.GetBytes(this.clientVer, 0, this.clientVer.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), clientVerLen);
		count += sizeof(ushort);
		count += clientVerLen;
		ushort platformLen = (ushort)Encoding.Unicode.GetBytes(this.platform, 0, this.platform.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), platformLen);
		count += sizeof(ushort);
		count += platformLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_Welcome : IPacket
{
	public string matchId;
	public string side;
	public long serverTimeMs;
	public int tickRate;
	public int startTick;
	public string map;
	public int seed;
	public int latencyBudgetMs;
	public string startPolicy;

	public ushort Protocol { get { return (ushort)PacketID.SC_Welcome; } }

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
		ushort sideLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.side = Encoding.Unicode.GetString(s.Slice(count, sideLen));
		count += sideLen;
		this.serverTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.tickRate = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.startTick = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort mapLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.map = Encoding.Unicode.GetString(s.Slice(count, mapLen));
		count += mapLen;
		this.seed = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.latencyBudgetMs = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		ushort startPolicyLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.startPolicy = Encoding.Unicode.GetString(s.Slice(count, startPolicyLen));
		count += startPolicyLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_Welcome);
		count += sizeof(ushort);
		ushort matchIdLen = (ushort)Encoding.Unicode.GetBytes(this.matchId, 0, this.matchId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), matchIdLen);
		count += sizeof(ushort);
		count += matchIdLen;
		ushort sideLen = (ushort)Encoding.Unicode.GetBytes(this.side, 0, this.side.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), sideLen);
		count += sizeof(ushort);
		count += sideLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.serverTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.tickRate);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.startTick);
		count += sizeof(int);
		ushort mapLen = (ushort)Encoding.Unicode.GetBytes(this.map, 0, this.map.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), mapLen);
		count += sizeof(ushort);
		count += mapLen;
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.seed);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.latencyBudgetMs);
		count += sizeof(int);
		ushort startPolicyLen = (ushort)Encoding.Unicode.GetBytes(this.startPolicy, 0, this.startPolicy.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), startPolicyLen);
		count += sizeof(ushort);
		count += startPolicyLen;
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_Loaded : IPacket
{
	public string matchId;
	public string uid;

	public ushort Protocol { get { return (ushort)PacketID.CS_Loaded; } }

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
		ushort uidLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		this.uid = Encoding.Unicode.GetString(s.Slice(count, uidLen));
		count += uidLen;
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_Loaded);
		count += sizeof(ushort);
		ushort matchIdLen = (ushort)Encoding.Unicode.GetBytes(this.matchId, 0, this.matchId.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), matchIdLen);
		count += sizeof(ushort);
		count += matchIdLen;
		ushort uidLen = (ushort)Encoding.Unicode.GetBytes(this.uid, 0, this.uid.Length, segment.Array, segment.Offset + count + sizeof(ushort));
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), uidLen);
		count += sizeof(ushort);
		count += uidLen;
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
		public string side;
		public bool loaded;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			ushort uidLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.uid = Encoding.Unicode.GetString(s.Slice(count, uidLen));
			count += uidLen;
			ushort sideLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			this.side = Encoding.Unicode.GetString(s.Slice(count, sideLen));
			count += sideLen;
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
			ushort sideLen = (ushort)Encoding.Unicode.GetBytes(this.side, 0, this.side.Length, segment.Array, segment.Offset + count + sizeof(ushort));
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), sideLen);
			count += sizeof(ushort);
			count += sideLen;
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

public class SC_BeatSync : IPacket
{
	public long serverTimeMs;
	public int currentBeatIndex;
	public int bpm;
	public int beatDurationMs;
	public int nextBeatIndex;
	public int nextBeatTimeMs;

	public ushort Protocol { get { return (ushort)PacketID.SC_BeatSync; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.serverTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.currentBeatIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.bpm = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.beatDurationMs = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.nextBeatIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.nextBeatTimeMs = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
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
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.serverTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.currentBeatIndex);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.bpm);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.beatDurationMs);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.nextBeatIndex);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.nextBeatTimeMs);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_Input : IPacket
{
	public int beatIndex;
	public int inputActionType;
	public int moveDir;
	public int skillId;

	public ushort Protocol { get { return (ushort)PacketID.CS_Input; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.beatIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.inputActionType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.moveDir = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.skillId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_Input);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.beatIndex);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.inputActionType);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.moveDir);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.skillId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_State : IPacket
{
	public int beatIndex;
	public class EntityState
	{
		public int entityId;
		public int type;
		public int x;
		public int y;
		public int hp;
		public int isDead;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.entityId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.type = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.x = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.hp = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.isDead = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.entityId);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.type);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.x);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.y);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.hp);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.isDead);
			count += sizeof(int);
			return success;
		}	
	}
	public List<EntityState> entityStates = new List<EntityState>();

	public ushort Protocol { get { return (ushort)PacketID.SC_State; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.beatIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.entityStates.Clear();
		ushort entityStateLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < entityStateLen; i++)
		{
			EntityState entityState = new EntityState();
			entityState.Read(s, ref count);
			entityStates.Add(entityState);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_State);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.beatIndex);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.entityStates.Count);
		count += sizeof(ushort);
		foreach (EntityState entityState in this.entityStates)
			success &= entityState.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_MakeMapData : IPacket
{
	public int map;
	public int playerSlotNumber;

	public ushort Protocol { get { return (ushort)PacketID.SC_MakeMapData; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.map = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.playerSlotNumber = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_MakeMapData);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.map);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerSlotNumber);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_GameStartWithRollDice : IPacket
{
	public long serverTimeMs;
	public int mapId;
	public int firstDiceRequestNonce;

	public ushort Protocol { get { return (ushort)PacketID.SC_GameStartWithRollDice; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.serverTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
		count += sizeof(long);
		this.mapId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.firstDiceRequestNonce = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_GameStartWithRollDice);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.serverTimeMs);
		count += sizeof(long);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.mapId);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.firstDiceRequestNonce);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_FirstDiceResult : IPacket
{
	public int nonce;
	public int diceNum;

	public ushort Protocol { get { return (ushort)PacketID.CS_FirstDiceResult; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.nonce = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.diceNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_FirstDiceResult);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.nonce);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.diceNum);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_RollDiceForTurn : IPacket
{
	public int turn;
	public int playerTurnSlot;
	public int diceRequestNonce;

	public ushort Protocol { get { return (ushort)PacketID.SC_RollDiceForTurn; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.playerTurnSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.diceRequestNonce = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_RollDiceForTurn);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerTurnSlot);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.diceRequestNonce);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_RollDicePieceMove : IPacket
{
	public int turn;
	public int nonce;
	public int diceNum;
	public int pieceType;
	public class Path
	{
		public int x;
		public int y;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.x = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.x);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.y);
			count += sizeof(int);
			return success;
		}	
	}
	public List<Path> paths = new List<Path>();

	public ushort Protocol { get { return (ushort)PacketID.CS_RollDicePieceMove; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.nonce = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.diceNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.pieceType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.paths.Clear();
		ushort pathLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < pathLen; i++)
		{
			Path path = new Path();
			path.Read(s, ref count);
			paths.Add(path);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_RollDicePieceMove);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.nonce);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.diceNum);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.pieceType);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.paths.Count);
		count += sizeof(ushort);
		foreach (Path path in this.paths)
			success &= path.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_PieceMoveConfirm : IPacket
{
	public int turn;
	public int pieceType;
	public class Path
	{
		public int x;
		public int y;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.x = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.x);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.y);
			count += sizeof(int);
			return success;
		}	
	}
	public List<Path> paths = new List<Path>();
	public bool willFight;

	public ushort Protocol { get { return (ushort)PacketID.SC_PieceMoveConfirm; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.pieceType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.paths.Clear();
		ushort pathLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < pathLen; i++)
		{
			Path path = new Path();
			path.Read(s, ref count);
			paths.Add(path);
		}
		this.willFight = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
		count += sizeof(bool);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_PieceMoveConfirm);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.pieceType);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.paths.Count);
		count += sizeof(ushort);
		foreach (Path path in this.paths)
			success &= path.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.willFight);
		count += sizeof(bool);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_FightConfirm : IPacket
{
	public bool isFight;
	public int turn;
	public class PiceInfo
	{
		public int playerSlotNum;
		public int piceNum;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.playerSlotNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.piceNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerSlotNum);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.piceNum);
			count += sizeof(int);
			return success;
		}	
	}
	public List<PiceInfo> piceInfos = new List<PiceInfo>();
	public int battleNonce;

	public ushort Protocol { get { return (ushort)PacketID.SC_FightConfirm; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.isFight = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
		count += sizeof(bool);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.piceInfos.Clear();
		ushort piceInfoLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < piceInfoLen; i++)
		{
			PiceInfo piceInfo = new PiceInfo();
			piceInfo.Read(s, ref count);
			piceInfos.Add(piceInfo);
		}
		this.battleNonce = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_FightConfirm);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.isFight);
		count += sizeof(bool);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.piceInfos.Count);
		count += sizeof(ushort);
		foreach (PiceInfo piceInfo in this.piceInfos)
			success &= piceInfo.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.battleNonce);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class CS_BattleCardDraw : IPacket
{
	public int turn;
	public int battleNonce;
	public int cardNum;

	public ushort Protocol { get { return (ushort)PacketID.CS_BattleCardDraw; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.battleNonce = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.cardNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.CS_BattleCardDraw);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.battleNonce);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.cardNum);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_BattleResult : IPacket
{
	public int turn;
	public int battleNonce;
	public int losePlayerSlotNum;
	public int losePiceNum;
	public int newX;
	public int newY;
	public bool isGameEnd;

	public ushort Protocol { get { return (ushort)PacketID.SC_BattleResult; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.battleNonce = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.losePlayerSlotNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.losePiceNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.newX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.newY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.isGameEnd = BitConverter.ToBoolean(s.Slice(count, s.Length - count));
		count += sizeof(bool);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_BattleResult);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.battleNonce);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.losePlayerSlotNum);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.losePiceNum);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.newX);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.newY);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.isGameEnd);
		count += sizeof(bool);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_TurnEnd : IPacket
{
	public int turn;
	public int nextPlayerTurnSlot;

	public ushort Protocol { get { return (ushort)PacketID.SC_TurnEnd; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.nextPlayerTurnSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_TurnEnd);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.nextPlayerTurnSlot);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_WhoIsWinner : IPacket
{
	public int winnerSlot;
	public int reason;

	public ushort Protocol { get { return (ushort)PacketID.SC_WhoIsWinner; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.winnerSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.reason = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_WhoIsWinner);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.winnerSlot);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.reason);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}

public class SC_StateSnapshot : IPacket
{
	public int turn;
	public int revision;
	public class Pieces
	{
		public int playerSlot;
		public int pieceType;
		public int x;
		public int y;
		public int grade;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.playerSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.pieceType = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.x = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.y = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.grade = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerSlot);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.pieceType);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.x);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.y);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.grade);
			count += sizeof(int);
			return success;
		}	
	}
	public List<Pieces> piecess = new List<Pieces>();
	public class CardLeft
	{
		public int playerSlot;
		public int remainCount;
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.playerSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.remainCount = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerSlot);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.remainCount);
			count += sizeof(int);
			return success;
		}	
	}
	public List<CardLeft> cardLefts = new List<CardLeft>();
	public class UsedCards
	{
		public int playerSlot;
		public class Cards
		{
			public int cardNum;
		
			public void Read(ReadOnlySpan<byte> s, ref ushort count)
			{
				this.cardNum = BitConverter.ToInt32(s.Slice(count, s.Length - count));
				count += sizeof(int);
			}
		
			public bool Write(Span<byte> s, ref ushort count)
			{
				ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		
				bool success = true;
				success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.cardNum);
				count += sizeof(int);
				return success;
			}	
		}
		public List<Cards> cardss = new List<Cards>();
	
		public void Read(ReadOnlySpan<byte> s, ref ushort count)
		{
			this.playerSlot = BitConverter.ToInt32(s.Slice(count, s.Length - count));
			count += sizeof(int);
			this.cardss.Clear();
			ushort cardsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
			count += sizeof(ushort);
			for (int i = 0; i < cardsLen; i++)
			{
				Cards cards = new Cards();
				cards.Read(s, ref count);
				cardss.Add(cards);
			}
		}
	
		public bool Write(Span<byte> s, ref ushort count)
		{
			ArraySegment<byte> segment = SendBufferHelper.Open(4096);
	
			bool success = true;
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerSlot);
			count += sizeof(int);
			success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.cardss.Count);
			count += sizeof(ushort);
			foreach (Cards cards in this.cardss)
				success &= cards.Write(s, ref count);
			return success;
		}	
	}
	public List<UsedCards> usedCardss = new List<UsedCards>();

	public ushort Protocol { get { return (ushort)PacketID.SC_StateSnapshot; } }

	public void Read(ArraySegment<byte> segment)
	{
		ushort count = 0;

		ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
		count += sizeof(ushort);
		count += sizeof(ushort);
		this.turn = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.revision = BitConverter.ToInt32(s.Slice(count, s.Length - count));
		count += sizeof(int);
		this.piecess.Clear();
		ushort piecesLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < piecesLen; i++)
		{
			Pieces pieces = new Pieces();
			pieces.Read(s, ref count);
			piecess.Add(pieces);
		}
		this.cardLefts.Clear();
		ushort cardLeftLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < cardLeftLen; i++)
		{
			CardLeft cardLeft = new CardLeft();
			cardLeft.Read(s, ref count);
			cardLefts.Add(cardLeft);
		}
		this.usedCardss.Clear();
		ushort usedCardsLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
		count += sizeof(ushort);
		for (int i = 0; i < usedCardsLen; i++)
		{
			UsedCards usedCards = new UsedCards();
			usedCards.Read(s, ref count);
			usedCardss.Add(usedCards);
		}
	}

	public ArraySegment<byte> Write()
	{
		ArraySegment<byte> segment = SendBufferHelper.Open(4096);
		ushort count = 0;
		bool success = true;

		Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)PacketID.SC_StateSnapshot);
		count += sizeof(ushort);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.turn);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.revision);
		count += sizeof(int);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.piecess.Count);
		count += sizeof(ushort);
		foreach (Pieces pieces in this.piecess)
			success &= pieces.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.cardLefts.Count);
		count += sizeof(ushort);
		foreach (CardLeft cardLeft in this.cardLefts)
			success &= cardLeft.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.usedCardss.Count);
		count += sizeof(ushort);
		foreach (UsedCards usedCards in this.usedCardss)
			success &= usedCards.Write(s, ref count);
		success &= BitConverter.TryWriteBytes(s, count);
		if (success == false)
			return null;
		return SendBufferHelper.Close(count);
	}
}


using System;
using ServerCore;

public static class P2PRelayDiagnosticsPackets
{
    public const ushort HostPingRequestProtocol = 60001;
    public const ushort HostPingPongProtocol = 60002;
    public const ushort ActionTraceProtocol = 60003;

    public static ushort PeekProtocol(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4)
            return 0;

        return BitConverter.ToUInt16(bytes, 2);
    }

    public static long NowMs()
        => (System.Diagnostics.Stopwatch.GetTimestamp() * 1000L) / System.Diagnostics.Stopwatch.Frequency;
}

public enum P2PActionTraceStage
{
    None = 0,
    HostSeen = 1,
    Judge = 2,
    MoveResult = 3
}

public enum P2PActionTraceReason
{
    None = 0,
    AcceptMove = 100,
    AcceptMoveLate = 101,
    AcceptSkill = 110,
    AcceptSkillLate = 111,
    AcceptSkillCatchUp = 112,
    RejectHostOrDepsMissing = 200,
    RejectActorNotFound = 201,
    RejectActorDead = 202,
    RejectCurrentBeatInvalid = 203,
    RejectJudgeWindow = 204,
    RejectDuplicateBeat = 205,
    MoveApplied = 300,
    MoveSameTile = 301,
    MoveBlockedTile = 302,
    MoveOccupied = 303,
    MoveRejected = 304
}

public sealed class P2PHostPingRequestPacket : IPacket
{
    public int RequesterActorId;
    public int TargetHostActorId;
    public int Seq;
    public long ClientSendMs;

    public ushort Protocol => P2PRelayDiagnosticsPackets.HostPingRequestProtocol;

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;
        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
        count += sizeof(ushort);
        count += sizeof(ushort);
        RequesterActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        TargetHostActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        Seq = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ClientSendMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(128);
        ushort count = 0;
        bool success = true;
        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), Protocol);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), RequesterActorId);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), TargetHostActorId);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), Seq);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ClientSendMs);
        count += sizeof(long);
        success &= BitConverter.TryWriteBytes(s, count);

        if (!success)
            return null;

        return SendBufferHelper.Close(count);
    }
}

public sealed class P2PHostPingPongPacket : IPacket
{
    public int TargetActorId;
    public int HostActorId;
    public int Seq;
    public long ClientSendMs;
    public long HostRecvMs;
    public long HostSendMs;

    public ushort Protocol => P2PRelayDiagnosticsPackets.HostPingPongProtocol;

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;
        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
        count += sizeof(ushort);
        count += sizeof(ushort);
        TargetActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        HostActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        Seq = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ClientSendMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
        count += sizeof(long);
        HostRecvMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
        count += sizeof(long);
        HostSendMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(128);
        ushort count = 0;
        bool success = true;
        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), Protocol);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), TargetActorId);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), HostActorId);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), Seq);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ClientSendMs);
        count += sizeof(long);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), HostRecvMs);
        count += sizeof(long);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), HostSendMs);
        count += sizeof(long);
        success &= BitConverter.TryWriteBytes(s, count);

        if (!success)
            return null;

        return SendBufferHelper.Close(count);
    }
}

public sealed class P2PActionTracePacket : IPacket
{
    public int TargetActorId;
    public int ActorId;
    public int ActionKind;
    public int SlotIndex;
    public int TargetX;
    public int TargetY;
    public long ClientSendTimeMs;
    public int StageCode;
    public int ReasonCode;
    public int DetailValue;
    public int ResultX;
    public int ResultY;
    public long ExecuteBeat;
    public long HostObservedMs;

    public ushort Protocol => P2PRelayDiagnosticsPackets.ActionTraceProtocol;

    public void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;
        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);
        count += sizeof(ushort);
        count += sizeof(ushort);
        TargetActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ActorId = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ActionKind = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        SlotIndex = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        TargetX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        TargetY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ClientSendTimeMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
        count += sizeof(long);
        StageCode = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ReasonCode = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        DetailValue = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ResultX = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ResultY = BitConverter.ToInt32(s.Slice(count, s.Length - count));
        count += sizeof(int);
        ExecuteBeat = BitConverter.ToInt64(s.Slice(count, s.Length - count));
        count += sizeof(long);
        HostObservedMs = BitConverter.ToInt64(s.Slice(count, s.Length - count));
    }

    public ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(256);
        ushort count = 0;
        bool success = true;
        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), Protocol);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), TargetActorId);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ActorId);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ActionKind);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), SlotIndex);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), TargetX);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), TargetY);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ClientSendTimeMs);
        count += sizeof(long);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), StageCode);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ReasonCode);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), DetailValue);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ResultX);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ResultY);
        count += sizeof(int);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), ExecuteBeat);
        count += sizeof(long);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), HostObservedMs);
        count += sizeof(long);
        success &= BitConverter.TryWriteBytes(s, count);

        if (!success)
            return null;

        return SendBufferHelper.Close(count);
    }
}

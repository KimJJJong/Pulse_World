using System;
using ServerCore;

public static class P2PRelayDiagnosticsPackets
{
    public const ushort HostPingRequestProtocol = 60001;
    public const ushort HostPingPongProtocol = 60002;

    public static ushort PeekProtocol(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4)
            return 0;

        return BitConverter.ToUInt16(bytes, 2);
    }

    public static long NowMs()
        => (System.Diagnostics.Stopwatch.GetTimestamp() * 1000L) / System.Diagnostics.Stopwatch.Frequency;
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

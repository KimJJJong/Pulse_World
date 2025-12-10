using System;
using System.Collections;
using UnityEngine;

public static class NetConfig
{
    public const int PingIntervalMs = 2000;  // 2초
    public const int PingTimeoutMs = 6000;  // 6초 (3회 미수신 정도 느낌)
}

public enum PlayerSlot : byte { A = 0, B = 1 }
public enum PieceType  : byte { Grade0=0, Grade1=1, Grade2=2, Grade3=3 }

public struct PieceRef
{
    public PlayerSlot Slot;
    public PieceType  Type;
    public PieceRef(PlayerSlot slot, PieceType type) { Slot = slot; Type = type; }
}



public static class SideSlot
{
    public static int ToSlot(char side) => side == 'A' ? 0 : 1; // 방어 로직 추가해도 됨
    public static char ToSide(int slot) => slot == 0 ? 'A' : 'B';
}

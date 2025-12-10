using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;


public sealed class LogicContext
{
    public required RoomBus Bus { get; init; }
    public required ILogger Logger { get; init; }


    public RoomState State { get; set; } = RoomState.None;


  

    public static int NowMs() => (int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static int NextNonce() => Random.Shared.Next(1, int.MaxValue);
}

public enum RoomState { None, RollFirstDice, TurnBegin, WaitMove, WaitBattleCards, End }

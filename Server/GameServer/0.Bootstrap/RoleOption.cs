namespace Server.Bootstrap;

public sealed class RoleOptions
{
    public string Name { get; set; } = "Game"; // "Game" or "Town"
    public int TickMs { get; set; } = 15;      // Game=15, Town=100 같은 값
}

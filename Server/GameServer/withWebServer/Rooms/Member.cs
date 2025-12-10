namespace Rooms;
public sealed class Member
{
    public string UserId { get; init; } = default!;
    public string Name { get; init; } = "Player";
    public int Slot { get; init; }
    public bool Ready { get; set; }
}
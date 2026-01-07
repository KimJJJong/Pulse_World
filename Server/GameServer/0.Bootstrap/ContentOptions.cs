namespace Server.Bootstrap;


public sealed class ContentOptions
{
    public RoleContentOptions Game { get; init; } = new();
    public RoleContentOptions Town { get; init; } = new();

    // 개발/운영에서 같은 루트를 공유하고 싶을 때 공통 fallback
    public string? DefaultBaseDir { get; init; }
}
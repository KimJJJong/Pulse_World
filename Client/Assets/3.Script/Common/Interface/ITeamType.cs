using Gameplay.TypeEnum;

public interface ITeamType
{
    public TeamType GetTeamType();
    public (int, int) GetArrivedPos();
    public (int, int) GetStartPos();
}

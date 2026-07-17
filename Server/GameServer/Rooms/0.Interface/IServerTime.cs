/// <summary>
/// 서버 시간(ms)을 제공하는 인터페이스
/// </summary>
public interface IServerTime
{
    long NowMs { get; }
}
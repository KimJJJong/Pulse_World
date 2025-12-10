public struct BeatActionResult
{
    public int ActorId;
    public ActionKind Kind;
    public int FromX;
    public int FromY;
    public int ToX;
    public int ToY;
    public bool Accepted;    // 입력은 왔는데 충돌/범위 외/판정 실패 등으로 거절되면 false
}
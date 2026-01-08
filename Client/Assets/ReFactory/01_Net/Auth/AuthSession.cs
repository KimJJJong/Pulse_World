public sealed class AuthSession
{
    readonly TokenStore _tokens;

    public AuthSession(TokenStore tokens) => _tokens = tokens;

    public string AccessToken => _tokens.AccessToken;
    public bool HasAccessToken => !string.IsNullOrEmpty(_tokens.AccessToken);

    // refresh 동시 실행 방지
    bool _refreshing;
    public bool TryBeginRefresh()
    {
        if (_refreshing) return false;
        _refreshing = true;
        return true;
    }
    public void EndRefresh() => _refreshing = false;
}

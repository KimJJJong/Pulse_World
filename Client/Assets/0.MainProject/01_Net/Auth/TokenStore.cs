using UnityEngine;

public sealed class TokenStore
{
    const string KUid = "auth.uid";
    const string KAccess = "auth.access";
    const string KAccessExp = "auth.accessExpMs";
    const string KRefresh = "auth.refresh";
    const string KRefreshExp = "auth.refreshExpMs";

    public string Uid => PlayerPrefs.GetString(KUid, "");
    public string AccessToken => PlayerPrefs.GetString(KAccess, "");
    public long AccessExpMs => long.TryParse(PlayerPrefs.GetString(KAccessExp, "0"), out var v) ? v : 0;

    public string RefreshToken => PlayerPrefs.GetString(KRefresh, "");
    public long RefreshExpMs => long.TryParse(PlayerPrefs.GetString(KRefreshExp, "0"), out var v) ? v : 0;

    public bool HasAccessToken => !string.IsNullOrEmpty(AccessToken);
    public bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);

    public void SaveAll(string uid, string access, long accessExpMs, string refresh, long refreshExpMs)
    {
        PlayerPrefs.SetString(KUid, uid ?? "");
        PlayerPrefs.SetString(KAccess, access ?? "");
        PlayerPrefs.SetString(KAccessExp, accessExpMs.ToString());
        PlayerPrefs.SetString(KRefresh, refresh ?? "");
        PlayerPrefs.SetString(KRefreshExp, refreshExpMs.ToString());
        PlayerPrefs.Save();
    }

    public void SaveFromRefresh(string access, long accessExpMs, string refresh, long refreshExpMs)
    {
        // uid는 refresh 응답에 없으므로 유지
        PlayerPrefs.SetString(KAccess, access ?? "");
        PlayerPrefs.SetString(KAccessExp, accessExpMs.ToString());
        PlayerPrefs.SetString(KRefresh, refresh ?? "");
        PlayerPrefs.SetString(KRefreshExp, refreshExpMs.ToString());
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(KUid);
        PlayerPrefs.DeleteKey(KAccess);
        PlayerPrefs.DeleteKey(KAccessExp);
        PlayerPrefs.DeleteKey(KRefresh);
        PlayerPrefs.DeleteKey(KRefreshExp);
        PlayerPrefs.Save();
    }
}


using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using NetClient.Lobby;

public class LoginTestUI : MonoBehaviour
{
    public InputField inputBaseUrl;
    public InputField inputDeviceId;
    public Text logArea;
    public Button btnGuestLogin;
    public Button btnGoogleLogin;

    LobbyApiClient _api;

    private void Start()
    {    
        if (btnGuestLogin != null)
            btnGuestLogin.onClick.AddListener(OnGuestLogin);
        if (btnGoogleLogin != null)
            btnGoogleLogin.onClick.AddListener(OnGoogleLogin);
        _api = new LobbyApiClient("http://localhost:5290", "1.0.0");

    }

    private async void OnGuestLogin()
    {
        string baseUrl = inputBaseUrl?.text.Trim() ?? "http://localhost:5290";
        //TokenManager.Instance.InitBaseUrl(baseUrl);

        logArea.text = $"▶ 게스트 로그인 중... ({baseUrl})\n";

        //bool ok = await TokenManager.Instance.LoginGuestAsync();
        var res = await _api.GuestLoginAsync(inputDeviceId.text);
        _api.SetAccessToken(res.accessToken);
        if (res.ok)
        {
            logArea.text += "✅ 로그인 성공!\n";
            SceneManager.LoadScene("LobbyRoomSample");
        }
        else
        {
            logArea.text += "❌ 로그인 실패. 서버 연결 또는 토큰 오류\n";
        }
    }
    private async void OnGoogleLogin()
    {
        string baseUrl = inputBaseUrl?.text.Trim() ?? "http://localhost:5290";
        //TokenManager.Instance.InitBaseUrl(baseUrl);

        logArea.text = $"▶ Google 로그인URL 수신 중... ({baseUrl})\n";

        //bool ok = await TokenManager.Instance.LoginGuestAsync();
        var res = await _api.GoogleUrlRequest();
        if (res.ok)
        {
            logArea.text += $"✅ URL 수신 성공!\n {res.Url}";
            //SceneManager.LoadScene("LobbyRoomSample"); // URL 로드
            Debug.Log($"URL : {res.Url}");
            
        }
        else
        {
            logArea.text += "❌ URL 로드 실패. \n";
        }
    }
}

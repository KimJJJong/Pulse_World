using System.Threading.Tasks;
using UnityEngine;

public sealed class LoginScreen : MonoBehaviour
{
    [SerializeField] LoginView view = null!;
    [SerializeField] ConfirmPopup confirm = null!;

    void Awake()
    {
        var root = AppBootstrap.Instance.Root;

        view.DeviceIdText.text = root.Identity.DeviceId;
        view.SetError("");
        view.SetBusy(false);

        view.LoginButton.onClick.AddListener(() => _ = OnClickLoginAsync());

        view.CopyDeviceIdButton.onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = root.Identity.DeviceId;
            view.SetError("DeviceId를 클립보드에 복사했어요.");
        });

        view.ResetDeviceIdButton.onClick.AddListener(() =>
        {
            confirm.Show(
                title: "DeviceId 재발급",
                message: "DeviceId를 재발급하면 '새 계정'으로 취급될 수 있어요.\n정말 재발급할까요?",
                onOk: () =>
                {
                    // 로컬 토큰도 같이 초기화 (계정이 바뀌는 전제)
                    root.Tokens.Clear();

                    var newId = root.Identity.ResetDeviceId();
                    view.DeviceIdText.text = newId;

                    view.SetError("DeviceId를 재발급했어요. 이제 다시 로그인해요.");
                },
                onCancel: () => { }
            );
        });
    }

    async Task OnClickLoginAsync()
    {
        view.SetError("");
        view.SetBusy(true);

        var root = AppBootstrap.Instance.Root;

        var r = await root.AuthApi.LoginGuestAsync();
        view.SetBusy(false);

        if (!r.Ok)
        {
            view.SetError(r.Error);
            return;
        }

        root.AuthApi.ApplyLogin(r.Data);
        SceneRouter.Load(SceneNames.Home);
    }
}

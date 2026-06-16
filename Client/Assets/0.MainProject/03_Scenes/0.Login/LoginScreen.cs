using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class LoginScreen : MonoBehaviour
{
    [SerializeField] LoginView view = null!;
    [SerializeField] ConfirmPopup confirm = null!;

    void Awake()
    {
        if (AppBootstrap.Instance == null || AppBootstrap.Instance.Root == null)
        {
            Debug.LogWarning("LoginScreen requires AppBootstrap. Loading Bootstrap scene first.");
            SceneRouter.Load(SceneNames.Bootstrap);
            enabled = false;
            return;
        }

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
        if (AppBootstrap.Instance == null || AppBootstrap.Instance.Root == null)
        {
            SceneRouter.Load(SceneNames.Bootstrap);
            return;
        }

        view.SetError("");
        view.SetBusy(true);

        var root = AppBootstrap.Instance.Root;

        try
        {
            var r = await root.AuthApi.LoginPreferredAsync(root.SteamPlatform, root.Config);

            if (!r.Ok)
            {
                view.SetError(r.Error);
                return;
            }

            if (r.Data == null)
            {
                view.SetError("로그인 응답이 비어 있습니다.");
                return;
            }

            root.AuthApi.ApplyLogin(r.Data);
            SceneRouter.Load(SceneNames.WorldMap);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            view.SetError($"로그인 중 오류가 발생했습니다.\n{ex.Message}");
        }
        finally
        {
            if (view != null)
                view.SetBusy(false);
        }
    }
}

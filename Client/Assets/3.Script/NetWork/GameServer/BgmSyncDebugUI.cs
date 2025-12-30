// ===============================
// BgmSyncDebugUI.cs
//  - TMP 텍스트에 현재 Center/Offset/State를 표시
// ===============================
using UnityEngine;
using TMPro;

public sealed class BgmSyncDebugUI : MonoBehaviour
{
    [SerializeField] private BgmSyncPlayer _bgm;
    [SerializeField] private TextMeshProUGUI _text;

    void Awake()
    {
        if (_bgm == null)
            _bgm = FindFirstObjectByType<BgmSyncPlayer>();
    }

    void Update()
    {
        if (_bgm == null || _text == null) return;

        _text.text =
            $"BGM Sync: {_bgm.State}\n" +
            $"AlignCenter: {_bgm.AlignToBeatCenter}  (toggle: ;)\n" +
            $"FineOffsetMs: {_bgm.FineOffsetMs}  ([ ] step)\n" +
            $"TotalAudioOffsetMs: {_bgm.TotalAudioOffsetMs}\n";
    }
}

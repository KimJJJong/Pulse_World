using TMPro;
using UnityEngine;

public class StageInfoPanelView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI bpmText;
    [SerializeField] private RectTransform beatMarker;
    private string _lastStageName;
    private bool _legacyBeatInfoHidden;

    private void OnEnable()
    {
        HideLegacyBeatInfo();
    }

    public void SetStage(string stageName, double bpm)
    {
        string resolvedStageName = string.IsNullOrWhiteSpace(stageName) ? "InGame" : stageName;
        if (_legacyBeatInfoHidden && string.Equals(_lastStageName, resolvedStageName, System.StringComparison.Ordinal))
            return;

        _lastStageName = resolvedStageName;
        if (stageText != null)
            stageText.text = resolvedStageName;

        HideLegacyBeatInfo();
    }

    private void HideLegacyBeatInfo()
    {
        if (bpmText != null)
            bpmText.gameObject.SetActive(false);

        if (beatMarker != null)
            beatMarker.gameObject.SetActive(false);

        _legacyBeatInfoHidden = true;
    }
}

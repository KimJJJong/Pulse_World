using TMPro;
using UnityEngine;

public class StageInfoPanelView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI bpmText;
    [SerializeField] private RectTransform beatMarker;

    private void OnEnable()
    {
        HideLegacyBeatInfo();
    }

    public void SetStage(string stageName, double bpm)
    {
        if (stageText != null)
            stageText.text = string.IsNullOrWhiteSpace(stageName) ? "InGame" : stageName;

        HideLegacyBeatInfo();
    }

    private void HideLegacyBeatInfo()
    {
        if (bpmText != null)
            bpmText.gameObject.SetActive(false);

        if (beatMarker != null)
            beatMarker.gameObject.SetActive(false);
    }
}

using TMPro;
using UnityEngine;

public class StageInfoPanelView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI bpmText;
    [SerializeField] private RectTransform beatMarker;
    [SerializeField] private float markerStartX = -82f;
    [SerializeField] private float markerEndX = 332f;

    public void SetStage(string stageName, double bpm)
    {
        if (stageText != null)
            stageText.text = string.IsNullOrWhiteSpace(stageName) ? "InGame" : stageName;

        if (bpmText != null)
            bpmText.text = bpm > 0d ? $"BPM {Mathf.RoundToInt((float)bpm)}" : "BPM --";
    }

    private void Update()
    {
        if (beatMarker == null || RhythmClient.Instance == null)
            return;

        Vector2 position = beatMarker.anchoredPosition;
        position.x = Mathf.Lerp(markerStartX, markerEndX, (float)RhythmClient.Instance.GetCurrentBeatProgress01());
        beatMarker.anchoredPosition = position;
    }
}

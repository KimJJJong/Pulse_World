using UnityEngine;

public class BeatPulse : MonoBehaviour
{
    [SerializeField] private HexHudView hud;
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private float pulseDuration = 0.12f; // 초
    [SerializeField] private float glowPeakAlpha = 0.6f;

    private RhythmClient Rhythm => RhythmClient.Instance;
    private long _lastBeat = long.MinValue;
    private float _t = 1f;

    void Update()
    {
        if (Rhythm == null || hud == null) return;

        long beat = Rhythm.GetCurrentBeatIndex();
        if (beat != _lastBeat)
        {
            _lastBeat = beat;
            _t = 0f; // 펄스 시작
        }

        // 펄스 진행
        if (_t <= pulseDuration)
        {
            _t += Time.deltaTime;

            float u = Mathf.Clamp01(_t / pulseDuration);
            // 빠르게 튀고 빠르게 돌아오는 곡선(간단한 ease-out/in)
            float pop = 1f + (pulseScale - 1f) * (1f - u) * (1f - u); // 초반 강, 후반 약
            hud.SetPulseScale(pop);

            float a = glowPeakAlpha * (1f - u);
            hud.SetGlowAlpha(a);
        }
        else
        {
            hud.SetPulseScale(1f);
            hud.SetGlowAlpha(0f);
        }
    }
}

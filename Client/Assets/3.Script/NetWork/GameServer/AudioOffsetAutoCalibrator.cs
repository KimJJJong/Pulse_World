// ===============================
// 2) AudioOffsetAutoCalibrator.cs (새 버전)
// - 캘리브레이션 ON 동안 diffMs를 모은다
// - N개 모이면 median(diffMs)을 구해 AutoAlignOffsetMs에 "한 번에" 적용
// - 적용 후 자동 OFF
// ===============================
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AudioOffsetAutoCalibrator : MonoBehaviour
{
    public static AudioOffsetAutoCalibrator Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private BgmSyncPlayer _bgm;

    [Header("Mode")]
    [SerializeField] private bool _enabled = false;
    public bool Enabled => _enabled;

    [Header("Collect")]
    [SerializeField] private int _targetSamples = 16;

    [Tooltip("너무 큰 값 제외(실수 입력/끊김). 반박자 테스트하려면 1000 이상 권장")]
    [SerializeField] private int _ignoreAbsDiffOverMs = 1000;

    [Header("Apply")]
    [Tooltip("AutoAlignOffsetMs 변화량 제한(캘리브레이션은 크게 움직여야 하니 보통 넉넉히)")]
    [SerializeField] private int _maxApplyAbsMs = 1000;

    [Header("Hotkeys")]
    [SerializeField] private bool _enableHotkeys = true;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F8;
    [SerializeField] private KeyCode _applyNowKey = KeyCode.F9;
    [SerializeField] private KeyCode _clearKey = KeyCode.F10;

    private readonly List<int> _samples = new();
    public int SampleCount => _samples.Count;

    public int LastMedianDiffMs { get; private set; } = 0;
    public int LastAppliedDeltaMs { get; private set; } = 0;

    void Awake()
    {
        Instance = this;

        if (_bgm == null)
            _bgm = FindFirstObjectByType<BgmSyncPlayer>();
    }

    void Update()
    {
        if (!_enableHotkeys) return;

        if (Input.GetKeyDown(_toggleKey))
            SetEnabled(!_enabled, resetSamples: true);

        if (Input.GetKeyDown(_applyNowKey))
            Apply(force: true);

        if (Input.GetKeyDown(_clearKey))
            ClearSamples();
    }

    public void SetEnabled(bool on, bool resetSamples)
    {
        _enabled = on;
        if (resetSamples) ClearSamples();
    }

    public void ClearSamples()
    {
        _samples.Clear();
        LastMedianDiffMs = 0;
        LastAppliedDeltaMs = 0;
    }

    public void OnServerDiff(int diffMs)
    {
        if (!_enabled) return;

        if (Mathf.Abs(diffMs) > _ignoreAbsDiffOverMs)
            return;

        _samples.Add(diffMs);

        if (_samples.Count >= _targetSamples)
            Apply(force: false);
    }

    private void Apply(bool force)
    {
        if (_bgm == null)
            _bgm = FindFirstObjectByType<BgmSyncPlayer>();
        if (_bgm == null) return;

        if (!force && _samples.Count < _targetSamples)
            return;

        if (_samples.Count == 0)
            return;

        int median = ComputeMedian(_samples);
        LastMedianDiffMs = median;

        // ✅ abs(diff) 최소화 목적:
        // 오디오 오프셋을 O만큼 적용하면 diff' = diff - O
        // Σ|diff - O| 를 최소화하는 O는 median(diff)
        // 따라서 AutoAlignOffsetMs에 median을 "한 번에" 더해준다.
        int delta = Mathf.Clamp(median, -_maxApplyAbsMs, _maxApplyAbsMs);
        LastAppliedDeltaMs = delta;

        _bgm.AddAutoAlignOffsetMs(delta, save: true);
        _bgm.ForceHardSeekNextTick(); // [Instant Calibration] Apply immediately by jumping

        _samples.Clear();

        // 적용 후 자동 OFF (결과가 확실히 보이게)
        _enabled = false;
    }

    private static int ComputeMedian(List<int> values)
    {
        var tmp = new List<int>(values);
        tmp.Sort();
        int n = tmp.Count;
        int mid = n / 2;
        if ((n & 1) == 1) return tmp[mid];
        return (tmp[mid - 1] + tmp[mid]) / 2;
    }
}

// ===============================
// 0) 목표
// - 서버에서 내려주는 diffMs(early -, late +) 샘플 N개를 모아
// - |diffMs - O| 합이 최소가 되는 O = median(diffMs)을 구하고
// - BGM 오디오 기준점을 AutoAlignOffsetMs로 "한 번에" 이동시켜 abs(diff)를 최소화한다.
// - 캘리브레이션 ON일 때만 동작, 적용 후 자동 OFF.
// ===============================



// ===============================
// 1) BgmSyncPlayer.cs 수정본 (AutoAlignOffsetMs 추가 + total offset 반영)
//    - 기존 파일에 그대로 덮어쓰거나, 필요한 부분만 병합
// ===============================
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BgmSyncPlayer : MonoBehaviour
{
    public enum SyncState { Idle, Waiting, Starting, Playing, Ended, Error }

    [Header("Refs")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _clip;

    [Header("Sync Policy (Non-loop)")]
    [SerializeField] private float _syncIntervalSec = 0.05f;
    [SerializeField] private float _hardSeekThresholdSec = 0.5f;
    [SerializeField] private float _pitchStartThresholdSec = 0.08f; // 피치 조절(Catch-up) 시작 기준
    [SerializeField] private float _pitchStopThresholdSec = 0.02f; // 피치 조절 완료(휴식) 기준
    
    [Header("Pitch Tuning")]
    [SerializeField] private float _maxPitchRate = 1.02f; // 최대 +2% 빠름
    [SerializeField] private float _minPitchRate = 0.98f; // 최소 -2% 느림

    private bool _isCatchingUp = false;

    [SerializeField] private float _preRollSec = 0.3f;
    [SerializeField] private float _nearEndEpsilonSec = 0.05f;

    [Header("Beat Align (Audio Only)")]
    [SerializeField] private bool _alignToBeatCenter = false;

    [Tooltip("장치(스피커/이어폰) 고정 지연 보정용(작게). +면 음악이 빨리 들림")]
    [SerializeField] private int _deviceOffsetMs = 0;

    [Tooltip("자동 정렬(캘리브레이션) 결과로 곡 기준점을 이동(크게도 가능). +면 음악이 빨리 들림")]
    [SerializeField] private int _autoAlignOffsetMs = 0;

    [Header("Clamp")]
    [SerializeField] private int _minDeviceOffsetMs = -200;
    [SerializeField] private int _maxDeviceOffsetMs = 200;

    [SerializeField] private int _minAutoAlignOffsetMs = -1000;
    [SerializeField] private int _maxAutoAlignOffsetMs = 1000;

    public SyncState State { get; private set; } = SyncState.Idle;

    public bool AlignToBeatCenter => _alignToBeatCenter;
    public int DeviceOffsetMs => _deviceOffsetMs;
    public int AutoAlignOffsetMs => _autoAlignOffsetMs;

    private const string PREF_DEVICE_OFFSET = "BGM_DEVICE_OFFSET_MS";
    private const string PREF_AUTOALIGN_OFFSET = "BGM_AUTOALIGN_OFFSET_MS";

    public int TotalAudioOffsetMs
    {
        get
        {
            var r = RhythmClient.Instance;
            if (r == null) return _deviceOffsetMs + _autoAlignOffsetMs;

            double half = r.GetBeatDurationMs() * 0.5;
            double total = (_alignToBeatCenter ? half : 0.0) + _deviceOffsetMs + _autoAlignOffsetMs;
            return (int)Math.Round(total);
        }
    }

    public void SetDeviceOffsetMs(int ms, bool save = false)
    {
        _deviceOffsetMs = Mathf.Clamp(ms, _minDeviceOffsetMs, _maxDeviceOffsetMs);
        if (save) PlayerPrefs.SetInt(PREF_DEVICE_OFFSET, _deviceOffsetMs);
    }

    public void SetAutoAlignOffsetMs(int ms, bool save = false)
    {
        _autoAlignOffsetMs = Mathf.Clamp(ms, _minAutoAlignOffsetMs, _maxAutoAlignOffsetMs);
        if (save) PlayerPrefs.SetInt(PREF_AUTOALIGN_OFFSET, _autoAlignOffsetMs);
    }

    public void AddAutoAlignOffsetMs(int deltaMs, bool save = false)
    {
        SetAutoAlignOffsetMs(_autoAlignOffsetMs + deltaMs, save);
    }

    public void LoadOffsetsFromPrefs(int defaultDeviceMs = 0, int defaultAutoAlignMs = 0)
    {
        SetDeviceOffsetMs(PlayerPrefs.GetInt(PREF_DEVICE_OFFSET, defaultDeviceMs), save: false);
        SetAutoAlignOffsetMs(PlayerPrefs.GetInt(PREF_AUTOALIGN_OFFSET, defaultAutoAlignMs), save: false);
    }

    private bool _forceHardSeekNextTick = false;

    public void ForceHardSeekNextTick()
    {
        _forceHardSeekNextTick = true;
    }

    public event Action OnSongStarted;
    public event Action OnSongEnded;

    private double _nextSyncAt;
    private bool _startedEventRaised;
    private bool _prevAlignMode;
    private int _prevDeviceOffset;
    private int _prevAutoAlignOffset;

    void Reset()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_audioSource != null)
        {
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
        }
    }

    public void Bind(AudioClip clip, AudioSource src = null)
    {
        if (src != null) _audioSource = src;
        _clip = clip;

        if (_audioSource != null && _clip != null)
            _audioSource.clip = _clip;
    }

    public void StartSync()
    {
        if (_audioSource == null || _clip == null)
        {
            Debug.LogError("[BgmSyncPlayer] Missing AudioSource or AudioClip.");
            State = SyncState.Error;
            return;
        }

        if (!_audioSource.enabled)
            _audioSource.enabled = true;

        if (!_audioSource.gameObject.activeInHierarchy)
        {
            Debug.LogError("[BgmSyncPlayer] AudioSource GameObject is inactive in hierarchy. Cannot play.");
            State = SyncState.Error;
            return;
        }

        State = SyncState.Waiting;
        _nextSyncAt = Time.unscaledTimeAsDouble;
        _startedEventRaised = false;
        _isCatchingUp = false;

        if (_audioSource.isPlaying)
            _audioSource.Stop();

        _audioSource.time = 0f;
        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
    }

    public void StopSync()
    {
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();

        State = SyncState.Idle;
        _startedEventRaised = false;
        _isCatchingUp = false;
    }

    void Update()
    {
        // 실시간 인스펙터 변경 감지 및 즉시 반영 (Hard Seek)
        if (_prevAlignMode != _alignToBeatCenter || _prevDeviceOffset != _deviceOffsetMs || _prevAutoAlignOffset != _autoAlignOffsetMs)
        {
            _prevAlignMode = _alignToBeatCenter;
            _prevDeviceOffset = _deviceOffsetMs;
            _prevAutoAlignOffset = _autoAlignOffsetMs;
            ForceHardSeekNextTick();
        }

        if (State == SyncState.Idle || State == SyncState.Ended || State == SyncState.Error)
            return;

        if (Time.unscaledTimeAsDouble < _nextSyncAt)
            return;

        _nextSyncAt = Time.unscaledTimeAsDouble + _syncIntervalSec;
        TickSync();
    }

    private void TickSync()
    {
        var rhythm = RhythmClient.Instance;
        if (rhythm == null) return;
        if (_audioSource == null || _clip == null) return;

        if (!_audioSource.enabled || !_audioSource.gameObject.activeInHierarchy)
        {
            Debug.LogError("[BgmSyncPlayer] AudioSource became disabled/inactive during sync. Stop.");
            State = SyncState.Error;
            return;
        }

        long serverNowMs = rhythm.GetCurrentServerTimeMs();
        long startMs = rhythm.ServerSongStartMs;

        //  totalAudioOffsetMs (오디오에만 적용)
        double beatMs = rhythm.GetBeatDurationMs();
        double centerMs = _alignToBeatCenter ? (beatMs * 0.5) : 0.0;
        double totalOffsetMs = centerMs + _deviceOffsetMs + _autoAlignOffsetMs;

        double elapsedSec = (serverNowMs - startMs + totalOffsetMs) / 1000.0;

        if (elapsedSec < -_preRollSec)
        {
            if (State != SyncState.Waiting) State = SyncState.Waiting;
            return;
        }

        if (elapsedSec < 0)
        {
            if (State != SyncState.Starting) 
            {
                State = SyncState.Starting;

                // [Extreme Optimization] 서버 시간이 아직 곡 시작 시간 이전일 경우,
                // Update 루프를 기다려서 Play()를 누르지 않고, 사운드 카드의 DSP 시간에 맞춰 완벽한 타이밍에 재생되도록 예약합니다.
                double dspStart = AudioSettings.dspTime + (-elapsedSec);
                _audioSource.PlayScheduled(dspStart);
                Debug.Log($"[BgmSyncPlayer] Scheduled BGM Start perfectly in {-elapsedSec:F3}s (DSP Time: {dspStart:F3})");
            }
            return;
        }

        double clipLen = _clip.length;

        if (elapsedSec >= clipLen || elapsedSec >= clipLen - _nearEndEpsilonSec)
        {
            EndSong();
            return;
        }

        if (!_audioSource.isPlaying)
        {
            // 만약 Scheduled Play가 이미 걸려있어서 곧 재생될 운명이라면 건너뜁니다.
            // (PlayScheduled를 호출해도 isPlaying은 해당 dspTime이 되기 전까지 false를 리턴할 수 있기 때문)
            if (State == SyncState.Starting && elapsedSec < 0.1f)
            {
                // 예약 재생이 방금 시작되어 아직 isPlaying이 false로 뜨는 찰나의 순간
                // 여기서 SeekTo를 해버리면 애써 잡아놓은 완벽한 예약이 망가집니다.
            }
            else
            {
                // 중간 난입 혹은 엄청난 렉으로 예약 시간을 놓친 경우
                Debug.LogWarning($"[BgmSyncPlayer] Late start or Sync lost. Forcing Play() at {elapsedSec:F3}s");
                SeekTo((float)elapsedSec);
                _audioSource.Play();
            }

            State = SyncState.Playing;

            if (!_startedEventRaised)
            {
                _startedEventRaised = true;
                OnSongStarted?.Invoke();
            }
            return;
        }

        if (State != SyncState.Playing)
            State = SyncState.Playing;

        float actualSec = _audioSource.time;
        float expectedSec = (float)elapsedSec;

        float drift = actualSec - expectedSec;
        float abs = Mathf.Abs(drift);

        // 초반 3초 이내에는 작은 오차(30ms 이상)만 나도 즉시 강제로 Snap 해버림 (초기 웜업 요동 방지)
        bool isEarlyStartup = actualSec < 3.0f;
        float dynamicHardSeekSec = isEarlyStartup ? 0.03f : _hardSeekThresholdSec;

        if (_forceHardSeekNextTick || abs >= dynamicHardSeekSec)
        {
            if (_forceHardSeekNextTick)
                Debug.LogWarning($"[BgmSyncPlayer] Forced Hard Seek by Parameter Change/Calibration. Drift: {drift * 1000f:F1}ms");
            else
                Debug.LogWarning($"[BgmSyncPlayer] Hard Seek triggered! (Early={isEarlyStartup}) Drift: {drift * 1000f:F1}ms");

            SeekTo(expectedSec);
            _audioSource.pitch = 1.0f;
            _isCatchingUp = false;
            _forceHardSeekNextTick = false;
            return;
        }
        
        // --- 데드밴드(Hysteresis) 피치 싱크 로직 ---
        if (!_isCatchingUp && abs >= _pitchStartThresholdSec)
        {
            _isCatchingUp = true;
            Debug.LogWarning($"[BgmSyncPlayer] Catch-up 진입! (Drift: {drift * 1000f:F1}ms)");
        }
        else if (_isCatchingUp && abs <= _pitchStopThresholdSec)
        {
            _isCatchingUp = false;
            _audioSource.pitch = 1.0f;
            Debug.LogWarning($"[BgmSyncPlayer] Catch-up 완료 및 정배속(1.0) 복원. (Drift: {drift * 1000f:F1}ms)");
        }

        if (_isCatchingUp)
        {
            float targetPitch = (drift > 0) ? _minPitchRate : _maxPitchRate;
            _audioSource.pitch = targetPitch; // 즉작적인 피치 적용 (Lerp 제거하여 교정시간 단축)
            // 매 프레임 로그를 찍으면 부하가 심하므로 상태 전환 시(위)에만 로그 출력
        }
        else
        {
            if (_audioSource.pitch != 1.0f)
                _audioSource.pitch = 1.0f; // 안전 보장
        }
    }

    private void SeekTo(float sec)
    {
        sec = Mathf.Clamp(sec, 0f, Mathf.Max(0f, _clip.length - 0.001f));
        _audioSource.time = sec;
    }

    private void EndSong()
    {
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();

        if (State != SyncState.Ended)
        {
            State = SyncState.Ended;
            OnSongEnded?.Invoke();
        }
    }
}

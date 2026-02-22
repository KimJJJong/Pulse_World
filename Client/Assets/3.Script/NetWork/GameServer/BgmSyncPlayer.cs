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
    [SerializeField] private bool _alignToBeatCenter = false; // [Change] Default false for Nearest input

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

    public event Action OnSongStarted;
    public event Action OnSongEnded;

    private double _nextSyncAt;
    private bool _startedEventRaised;

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
            if (State != SyncState.Starting) State = SyncState.Starting;
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
            SeekTo((float)elapsedSec);
            _audioSource.Play();
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

        // drift = Audio시간 - 서버목표시간. 
        // 양수면 오디오가 서버보다 앞서감(빠름) -> 느리게 (Pitch < 1)
        // 음수면 오디오가 서버보다 뒤쳐짐(느림) -> 빠르게 (Pitch > 1)
        float drift = actualSec - expectedSec;
        float abs = Mathf.Abs(drift);

        if (abs >= _hardSeekThresholdSec)
        {
            Debug.LogWarning($"[BgmSyncPlayer] Hard Seek triggered! Drift: {drift * 1000f:F1}ms");
            SeekTo(expectedSec);
            _audioSource.pitch = 1.0f;
            _isCatchingUp = false;
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

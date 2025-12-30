// ===============================
// BgmSyncPlayer.cs
// ===============================
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BgmSyncPlayer : MonoBehaviour
{
    public enum SyncState { Idle, Waiting, Starting, Playing, Ended, Error }

    [Header("Refs")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _clip; // non-loop track

    [Header("Sync Policy (Non-loop)")]
    [Tooltip("동기화 체크 주기(초). 0.05~0.1 권장 (10~20Hz)")]
    [SerializeField] private float _syncIntervalSec = 0.05f;

    [Tooltip("이 이상 drift면 즉시 seek로 고정 (초)")]
    [SerializeField] private float _hardSeekThresholdSec = 0.08f; // 80ms

    [Tooltip("이 이하 drift는 무시 (초)")]
    [SerializeField] private float _ignoreThresholdSec = 0.02f; // 20ms

    [Tooltip("시작 전 프리롤(초). expected가 -preRoll 이하면 대기")]
    [SerializeField] private float _preRollSec = 0.3f;

    [Tooltip("끝나기 직전 너무 근접하면 재생 대신 종료 처리 (초)")]
    [SerializeField] private float _nearEndEpsilonSec = 0.05f;

    [Header("Beat Align (Audio Only)")]
    [Tooltip("비트 중앙(0.5)에 맞춰 오디오를 이동 (판정/비트 계산은 그대로)")]
    [SerializeField] private bool _alignToBeatCenter = true;

    [Tooltip("오디오만 미세 이동(ms). +면 음악이 빨리 들림(앞당김), -면 늦게 들림")]
    [SerializeField] private int _fineOffsetMs = 0;

    [Tooltip("런타임 키 튜닝 사용")]
    [SerializeField] private bool _enableRuntimeTuning = true;

    [Tooltip("키 튜닝 스텝(ms)")]
    [SerializeField] private int _tuneStepMs = 5;

    [Tooltip("FineOffsetMs 최소/최대")]
    [SerializeField] private int _minFineOffsetMs = -200;
    [SerializeField] private int _maxFineOffsetMs = 200;

    public SyncState State { get; private set; } = SyncState.Idle;

    public bool AlignToBeatCenter => _alignToBeatCenter;
    public int FineOffsetMs => _fineOffsetMs;

    // 디버그 표시용: 현재 적용되는 총 오프셋(ms)
    public int TotalAudioOffsetMs
    {
        get
        {
            var r = RhythmClient.Instance;
            if (r == null) return _fineOffsetMs;

            double half = r.GetBeatDurationMs() * 0.5;
            double total = (_alignToBeatCenter ? half : 0.0) + _fineOffsetMs;
            return (int)Math.Round(total);
        }
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

    /// <summary>
    /// RhythmClient.OnBeatSync 이후(게임 시작 시 1회) 호출하면 됨.
    /// </summary>
    public void StartSync()
    {
        if (_audioSource == null || _clip == null)
        {
            Debug.LogError("[BgmSyncPlayer] Missing AudioSource or AudioClip.");
            State = SyncState.Error;
            return;
        }

        // ✅ disabled면 Play 불가
        if (!_audioSource.enabled)
            _audioSource.enabled = true;

        // ✅ 부모/오브젝트가 비활성이면 Play 불가
        if (!_audioSource.gameObject.activeInHierarchy)
        {
            Debug.LogError("[BgmSyncPlayer] AudioSource GameObject is inactive in hierarchy. Cannot play.");
            State = SyncState.Error;
            return;
        }

        State = SyncState.Waiting;
        _nextSyncAt = Time.unscaledTimeAsDouble;
        _startedEventRaised = false;

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
    }

    void Update()
    {
        if (_enableRuntimeTuning)
            RuntimeTuning();

        if (State == SyncState.Idle || State == SyncState.Ended || State == SyncState.Error)
            return;

        if (Time.unscaledTimeAsDouble < _nextSyncAt)
            return;

        _nextSyncAt = Time.unscaledTimeAsDouble + _syncIntervalSec;
        TickSync();
    }

    private void RuntimeTuning()
    {
        // 토글: ; 키
        if (Input.GetKeyDown(KeyCode.Semicolon))
            _alignToBeatCenter = !_alignToBeatCenter;

        // 미세 조정: [ / ] 키
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            _fineOffsetMs -= _tuneStepMs;

        if (Input.GetKeyDown(KeyCode.RightBracket))
            _fineOffsetMs += _tuneStepMs;

        _fineOffsetMs = Mathf.Clamp(_fineOffsetMs, _minFineOffsetMs, _maxFineOffsetMs);
    }

    private void TickSync()
    {
        var rhythm = RhythmClient.Instance;
        if (rhythm == null)
        {
            Debug.LogWarning("[BgmSyncPlayer] RhythmClient.Instance is null");
            return;
        }

        if (_audioSource == null || _clip == null)
            return;

        // 안전: 런타임 중 비활성/비활성화되면 즉시 에러
        if (!_audioSource.enabled || !_audioSource.gameObject.activeInHierarchy)
        {
            Debug.LogError("[BgmSyncPlayer] AudioSource became disabled/inactive during sync. Stop.");
            State = SyncState.Error;
            return;
        }

        long serverNowMs = rhythm.GetCurrentServerTimeMs();
        long startMs = rhythm.ServerSongStartMs;

        // ✅ 오디오 오프셋(비트 중앙 + 미세 ms)을 "오디오에만" 적용
        double beatMs = rhythm.GetBeatDurationMs();
        double centerMs = _alignToBeatCenter ? (beatMs * 0.5) : 0.0;
        double audioOffsetMs = centerMs + _fineOffsetMs;

        double elapsedSec = (serverNowMs - startMs + audioOffsetMs) / 1000.0;

        // 시작 전: 너무 이르면 대기
        if (elapsedSec < -_preRollSec)
        {
            if (State != SyncState.Waiting)
                State = SyncState.Waiting;
            return;
        }

        // 시작 전이지만 곧 시작
        if (elapsedSec < 0)
        {
            if (State != SyncState.Starting)
                State = SyncState.Starting;
            return;
        }

        double clipLen = _clip.length;

        // 곡 종료(서버 기준)
        if (elapsedSec >= clipLen || elapsedSec >= clipLen - _nearEndEpsilonSec)
        {
            EndSong();
            return;
        }

        // 아직 재생 시작 안 했으면: expected 위치로 seek 후 Play
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

        float drift = actualSec - expectedSec;
        float abs = Mathf.Abs(drift);

        if (abs <= _ignoreThresholdSec)
            return;

        // 최소 기능: 일정 이상이면 즉시 고정
        if (abs >= _hardSeekThresholdSec)
        {
            SeekTo(expectedSec);
        }
        // 0.02~0.08 구간은 아무 것도 안 함(나중에 pitch 보정으로 확장)
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

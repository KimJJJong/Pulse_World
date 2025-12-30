// ===============================
// BgmDirector.cs
//  - 네트워크/게임 상태 이벤트를 받아 BgmSyncPlayer를 "한 번만" 시작시키는 트리거 레이어
// ===============================
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BgmDirector : MonoBehaviour
{
    public static BgmDirector Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private BgmSyncPlayer _player;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _clip; // 3분+ non-loop

    private long _lastStartedSongStartMs = long.MinValue;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_player == null) _player = GetComponentInChildren<BgmSyncPlayer>(true);
        if (_audioSource == null) _audioSource = GetComponentInChildren<AudioSource>(true);

        // 안전: 시작부터 enabled 보장
        if (_audioSource != null)
        {
            _audioSource.enabled = true;
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
        }
    }

    void OnEnable()
    {
        ClientHandlers.OnBeatSyncReady += HandleBeatSyncReady;
    }

    void OnDisable()
    {
        ClientHandlers.OnBeatSyncReady -= HandleBeatSyncReady;
    }

    private void HandleBeatSyncReady()
    {
        var rhythm = RhythmClient.Instance;
        if (rhythm == null) return;

        long startMs = rhythm.ServerSongStartMs;
        if (startMs <= 0) return;

        // 같은 곡 startMs면 중복 시작 방지
        if (startMs == _lastStartedSongStartMs)
            return;

        _lastStartedSongStartMs = startMs;

        if (_player == null || _clip == null)
        {
            Debug.LogError("[BgmDirector] Missing player or clip.");
            return;
        }

        // 오디오 소스가 비활성/disabled면 강제 켜기
        if (_audioSource != null)
        {
            if (!_audioSource.enabled) _audioSource.enabled = true;
            if (!_audioSource.gameObject.activeInHierarchy)
                Debug.LogWarning("[BgmDirector] AudioSource GameObject inactive. Make sure Director/AudioSource root is active.");

            _audioSource.clip = _clip;
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
        }

        _player.Bind(_clip, _audioSource);
        _player.StartSync();
    }

    /// <summary>게임 종료/로비 복귀 등에서 호출</summary>
    public void StopBgm()
    {
        _lastStartedSongStartMs = long.MinValue;
        if (_player != null) _player.StopSync();
        else if (_audioSource != null) _audioSource.Stop();
    }
}

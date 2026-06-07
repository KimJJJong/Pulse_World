using System;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum UiSfxKind
{
    Hover,
    Click,
    Confirm,
    Cancel,
    Toggle,
    Slider,
    Open,
    Close,
    Error,
    TextFocus
}

public sealed class UiSfxService : MonoBehaviour
{
    private const string ResourcePrefix = "Audio/UI/";
    private const string StreamingFolder = "UIAudio";
    private const float GlobalStackCooldown = 0.055f;

    private static readonly Dictionary<UiSfxKind, string> FileNames = new Dictionary<UiSfxKind, string>
    {
        { UiSfxKind.Hover, "Ui_Hover.wav" },
        { UiSfxKind.Click, "Ui_Click.wav" },
        { UiSfxKind.Confirm, "Ui_Confirm.wav" },
        { UiSfxKind.Cancel, "Ui_Cancel.wav" },
        { UiSfxKind.Toggle, "Ui_Toggle.wav" },
        { UiSfxKind.Slider, "Ui_SliderTick.wav" },
        { UiSfxKind.Open, "Ui_Open.wav" },
        { UiSfxKind.Close, "Ui_Close.wav" },
        { UiSfxKind.Error, "Ui_Error.wav" },
        { UiSfxKind.TextFocus, "Ui_TextFocus.wav" }
    };

    private readonly Dictionary<UiSfxKind, AudioClip> _unityClips = new Dictionary<UiSfxKind, AudioClip>();
    private readonly Dictionary<UiSfxKind, FMOD.Sound> _fmodSounds = new Dictionary<UiSfxKind, FMOD.Sound>();
    private readonly float[] _lastPlayTimes = new float[Enum.GetValues(typeof(UiSfxKind)).Length];

    private AudioSource _audioSource;
    private float _lastAnyPlayTime = -999f;
    private bool _fmodReady;
    private bool _loggedBackend;

    public static UiSfxService Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("UiSfxService");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<UiSfxService>();
    }

    public static void PlayGlobal(UiSfxKind kind)
    {
        if (Instance != null)
            Instance.Play(kind);
    }

    public static UiSfxKind ClassifyButton(GameObject target)
    {
        var name = target != null ? target.name.ToLowerInvariant() : "";
        if (ContainsAny(name, "cancel", "close", "back", "exit", "quit", "clear", "delete", "remove"))
            return UiSfxKind.Cancel;
        if (ContainsAny(name, "ok", "confirm", "apply", "start", "login", "join", "create", "select", "ready", "accept", "save"))
            return UiSfxKind.Confirm;
        return UiSfxKind.Click;
    }

    public void Play(UiSfxKind kind)
    {
        var index = (int)kind;
        var now = Time.unscaledTime;
        var volume = GetVolume(kind);
        if (volume <= 0f)
            return;

        if (now - _lastAnyPlayTime < GlobalStackCooldown)
            return;

        if (now - _lastPlayTimes[index] < GetCooldown(kind))
            return;

        _lastPlayTimes[index] = now;
        _lastAnyPlayTime = now;

        if (_fmodReady && PlayFmod(kind, volume))
        {
            LogBackend("FMOD Core");
            return;
        }

        PlayUnity(kind, volume);
        LogBackend("Unity Audio");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadUnityClips();
        TryLoadFmodSounds();
        if (!_fmodReady)
            EnsureUnityAudioSource();

        SceneManager.sceneLoaded += OnSceneLoaded;
        InvokeRepeating(nameof(ScanActiveSelectables), 0.20f, 0.75f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        foreach (var pair in _fmodSounds)
        {
            pair.Value.release();
        }

        _fmodSounds.Clear();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScanActiveSelectables();
    }

    private void ScanActiveSelectables()
    {
        var selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < selectables.Length; i++)
        {
            var selectable = selectables[i];
            if (selectable == null || !selectable.gameObject.activeInHierarchy)
                continue;

            var target = selectable.GetComponent<UiSfxTarget>();
            if (target == null)
                target = selectable.gameObject.AddComponent<UiSfxTarget>();

            target.Bind(selectable);
        }
    }

    private void LoadUnityClips()
    {
        foreach (var pair in FileNames)
        {
            var resourceName = Path.GetFileNameWithoutExtension(pair.Value);
            var clip = Resources.Load<AudioClip>(ResourcePrefix + resourceName);
            if (clip != null)
                _unityClips[pair.Key] = clip;
        }
    }

    private void TryLoadFmodSounds()
    {
        try
        {
            foreach (var pair in FileNames)
            {
                var path = Path.Combine(Application.streamingAssetsPath, StreamingFolder, pair.Value);
                if (!File.Exists(path))
                    continue;

                var mode = FMOD.MODE.DEFAULT | FMOD.MODE._2D | FMOD.MODE.LOOP_OFF | FMOD.MODE.CREATESAMPLE;
                var result = RuntimeManager.CoreSystem.createSound(path, mode, out var sound);
                if (result == FMOD.RESULT.OK)
                    _fmodSounds[pair.Key] = sound;
            }

            _fmodReady = _fmodSounds.Count > 0;
        }
        catch (Exception ex)
        {
            _fmodReady = false;
            Debug.LogWarning("[UiSfxService] FMOD Core UI SFX init failed. Falling back to Unity Audio. " + ex.Message);
        }
    }

    private bool PlayFmod(UiSfxKind kind, float volume)
    {
        if (!_fmodSounds.TryGetValue(kind, out var sound))
            return false;

        var result = RuntimeManager.CoreSystem.playSound(sound, default, false, out var channel);
        if (result != FMOD.RESULT.OK)
            return false;

        channel.setVolume(volume);
        return true;
    }

    private void PlayUnity(UiSfxKind kind, float volume)
    {
        EnsureUnityAudioSource();
        if (_audioSource == null || !_unityClips.TryGetValue(kind, out var clip) || clip == null)
            return;

        _audioSource.PlayOneShot(clip, volume);
    }

    private void EnsureUnityAudioSource()
    {
        if (_audioSource != null)
            return;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1f;
    }

    private void LogBackend(string backend)
    {
        if (_loggedBackend)
            return;

        _loggedBackend = true;
        Debug.Log("[UiSfxService] UI SFX backend: " + backend);
    }

    private static float GetVolume(UiSfxKind kind)
    {
        switch (kind)
        {
            case UiSfxKind.Hover:
            case UiSfxKind.Slider:
            case UiSfxKind.TextFocus:
                return 0f;
            case UiSfxKind.Click:
                return 0.16f;
            case UiSfxKind.Toggle:
                return 0.13f;
            case UiSfxKind.Open:
            case UiSfxKind.Close:
                return 0.14f;
            case UiSfxKind.Confirm:
                return 0.18f;
            case UiSfxKind.Cancel:
                return 0.16f;
            case UiSfxKind.Error:
                return 0.20f;
            default:
                return 0.15f;
        }
    }

    private static float GetCooldown(UiSfxKind kind)
    {
        switch (kind)
        {
            case UiSfxKind.Hover:
            case UiSfxKind.TextFocus:
                return 0.20f;
            case UiSfxKind.Slider:
                return 0.18f;
            case UiSfxKind.Toggle:
                return 0.12f;
            case UiSfxKind.Open:
            case UiSfxKind.Close:
                return 0.15f;
            case UiSfxKind.Error:
                return 0.20f;
            default:
                return 0.08f;
        }
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (value.Contains(tokens[i]))
                return true;
        }

        return false;
    }
}

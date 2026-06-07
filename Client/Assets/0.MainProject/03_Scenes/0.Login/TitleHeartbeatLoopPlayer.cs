using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public sealed class TitleHeartbeatLoopPlayer : MonoBehaviour
{
    private AudioSource _source;
    private bool _loggedPlayback;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        ConfigureSource();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (_source == null)
            _source = GetComponent<AudioSource>();

        ConfigureSource();

        if (_source.clip != null && !_source.isPlaying)
            _source.Play();

        StartCoroutine(VerifyPlayback());
    }

    private IEnumerator VerifyPlayback()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        ConfigureSource();
        if (_source.clip != null && !_source.isPlaying)
            _source.Play();

        if (!_loggedPlayback)
        {
            _loggedPlayback = true;
            Debug.Log($"[TitleHeartbeatLoopPlayer] clip={_source.clip?.name ?? "null"} playing={_source.isPlaying} volume={_source.volume:F2} listenerVolume={AudioListener.volume:F2}");
        }
    }

    private void ConfigureSource()
    {
        if (_source == null)
            return;

        AudioListener.pause = false;
        AudioListener.volume = 1f;
        EnsureActiveAudioListener();

        _source.enabled = true;
        _source.mute = false;
        _source.ignoreListenerPause = true;
        _source.playOnAwake = true;
        _source.loop = true;
        _source.volume = Mathf.Clamp(_source.volume <= 0f ? 0.58f : _source.volume, 0.1f, 0.72f);
        _source.spatialBlend = 0f;
        _source.dopplerLevel = 0f;
    }

    private static void EnsureActiveAudioListener()
    {
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < listeners.Length; i++)
        {
            var listener = listeners[i];
            if (listener != null && listener.gameObject.activeInHierarchy)
            {
                listener.enabled = true;
                return;
            }
        }

        var camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (camera == null)
            return;

        var cameraListener = camera.GetComponent<AudioListener>();
        if (cameraListener == null)
            cameraListener = camera.gameObject.AddComponent<AudioListener>();

        cameraListener.enabled = true;
    }
}

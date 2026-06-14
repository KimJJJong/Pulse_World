using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TownBgmRuntime : MonoBehaviour
{
    private const string TownSongKey = "Game_Town_Acoustic_01";
    private const double TownBpm = 90.0;
    private const int TownBaseBeatDivision = 1;
    private const float LocalRhythmFallbackDelaySeconds = 0.35f;

    private RhythmClient _rhythm;
    private FMODDrumSequencer _sequencer;
    private Coroutine _localRhythmFallbackRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    public static void EnsureForScene(Scene scene)
    {
        if (!IsTownScene(scene))
            return;

        var runtime = FindSceneComponent<TownBgmRuntime>(scene);
        if (runtime == null)
        {
            var go = new GameObject("TownBgmRuntime");
            SceneManager.MoveGameObjectToScene(go, scene);
            runtime = go.AddComponent<TownBgmRuntime>();
        }

        runtime.Configure();
        runtime.StartLocalRhythmFallback();
    }

    private void Awake()
    {
        Configure();
    }

    private void OnEnable()
    {
        StartLocalRhythmFallback();
    }

    private void OnDisable()
    {
        if (_localRhythmFallbackRoutine != null)
        {
            StopCoroutine(_localRhythmFallbackRoutine);
            _localRhythmFallbackRoutine = null;
        }
    }

    private void Configure()
    {
        var scene = gameObject.scene;
        if (!IsTownScene(scene))
            return;

        _rhythm = EnsureRhythmClient(scene);
        _sequencer = EnsureSequencer(scene);
    }

    private void StartLocalRhythmFallback()
    {
        if (!isActiveAndEnabled || !IsTownScene(gameObject.scene))
            return;

        if (_localRhythmFallbackRoutine != null)
            StopCoroutine(_localRhythmFallbackRoutine);

        _localRhythmFallbackRoutine = StartCoroutine(CoStartLocalRhythmFallback());
    }

    private IEnumerator CoStartLocalRhythmFallback()
    {
        yield return new WaitForSecondsRealtime(LocalRhythmFallbackDelaySeconds);

        Configure();

        if (_rhythm != null && _rhythm.ServerSongStartMs <= 0)
            _rhythm.StartLocal(TownBpm, TownBaseBeatDivision);

        _localRhythmFallbackRoutine = null;
    }

    private static RhythmClient EnsureRhythmClient(Scene scene)
    {
        var rhythm = FindSceneComponent<RhythmClient>(scene);
        if (rhythm != null)
            return rhythm;

        var go = new GameObject("RhythmClient");
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.AddComponent<RhythmClient>();
    }

    private static FMODDrumSequencer EnsureSequencer(Scene scene)
    {
        var sequencer = FindSceneComponent<FMODDrumSequencer>(scene);
        if (sequencer == null)
        {
            var go = new GameObject("TownBgmSequencer");
            SceneManager.MoveGameObjectToScene(go, scene);
            sequencer = go.AddComponent<FMODDrumSequencer>();
        }

        sequencer.enableSequencer = true;
        sequencer.rhythmJsonAsset = LoadTownRhythmAsset();
        if (sequencer.rhythmJsonAsset == null)
        {
            Debug.LogError(
                $"[TownBgmRuntime] Rhythm JSON not found for {TownSongKey}. Expected Resources/Data/Sound/Json/{TownSongKey}.json");
        }

        return sequencer;
    }

    private static TextAsset LoadTownRhythmAsset()
    {
        return Resources.Load<TextAsset>($"Data/Sound/Json/{TownSongKey}")
               ?? Resources.Load<TextAsset>($"Data/Stage/{TownSongKey}")
               ?? Resources.Load<TextAsset>(TownSongKey);
    }

    private static bool IsTownScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        return string.Equals(scene.name, SceneNames.TownMap, StringComparison.OrdinalIgnoreCase)
               || string.Equals(scene.name, SceneNames.Town_Forest, StringComparison.OrdinalIgnoreCase)
               || scene.name.StartsWith("Town", StringComparison.OrdinalIgnoreCase);
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        var components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component == null || component.gameObject == null)
                continue;

            if (component.gameObject.scene == scene)
                return component;
        }

        return null;
    }
}

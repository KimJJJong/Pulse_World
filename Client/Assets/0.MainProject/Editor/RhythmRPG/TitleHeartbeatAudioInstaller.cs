#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TitleHeartbeatAudioInstaller
{
    private const string MenuPath = "RhythmRPG/Editors/Audio/Install Title Heartbeat Loop";
    private const string LoginScenePath = "Assets/0.MainProject/Scenes/Login.unity";
    private const string AudioFolder = "Assets/0.MainProject/Audio";
    private const string TitleAudioFolder = AudioFolder + "/Title";
    private const string AudioClipPath = TitleAudioFolder + "/Title_Heartbeat_DugeunLoop.wav";
    private const string AudioObjectName = "TitleHeartbeatLoopAudio";

    [MenuItem(MenuPath)]
    public static void Install()
    {
        EnableUnityAudioOutput();
        EnsureFolder("Assets/0.MainProject", "Audio");
        EnsureFolder(AudioFolder, "Title");

        WriteHeartbeatWav(ToFullPath(AudioClipPath));
        AssetDatabase.ImportAsset(AudioClipPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureAudioImporter(AudioClipPath);

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioClipPath);
        if (clip == null)
        {
            throw new InvalidOperationException("Failed to import title heartbeat audio clip: " + AudioClipPath);
        }

        var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
        var audioObject = FindSceneObject(scene, AudioObjectName);
        if (audioObject == null)
        {
            audioObject = new GameObject(AudioObjectName);
            SceneManager.MoveGameObjectToScene(audioObject, scene);
        }

        var source = audioObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = audioObject.AddComponent<AudioSource>();
        }

        var player = audioObject.GetComponent<TitleHeartbeatLoopPlayer>();
        if (player == null)
        {
            player = audioObject.AddComponent<TitleHeartbeatLoopPlayer>();
        }

        audioObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        audioObject.transform.localScale = Vector3.one;

        source.clip = clip;
        source.playOnAwake = true;
        source.loop = true;
        source.volume = 0.58f;
        source.pitch = 1f;
        source.priority = 64;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;

        var listener = EnsureSceneAudioListener(scene);

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(player);
        if (listener != null)
            EditorUtility.SetDirty(listener);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene("Assets/0.MainProject/Scenes/Bootstrap.unity", OpenSceneMode.Single);
        Debug.Log("Installed title heartbeat loop audio source in Login scene: " + AudioClipPath);
    }

    private static AudioListener EnsureSceneAudioListener(Scene scene)
    {
        var sceneCameras = new System.Collections.Generic.List<Camera>();
        foreach (var root in scene.GetRootGameObjects())
        {
            root.GetComponentsInChildren(true, sceneCameras);
        }

        Camera targetCamera = null;
        for (var i = 0; i < sceneCameras.Count; i++)
        {
            if (sceneCameras[i] != null && sceneCameras[i].CompareTag("MainCamera"))
            {
                targetCamera = sceneCameras[i];
                break;
            }
        }

        if (targetCamera == null && sceneCameras.Count > 0)
        {
            targetCamera = sceneCameras[0];
        }

        if (targetCamera == null)
        {
            return null;
        }

        var listener = targetCamera.GetComponent<AudioListener>();
        if (listener == null)
        {
            listener = targetCamera.gameObject.AddComponent<AudioListener>();
        }

        listener.enabled = true;
        return listener;
    }

    private static void EnableUnityAudioOutput()
    {
        var audioManager = Unsupported.GetSerializedAssetInterfaceSingleton("AudioManager");
        if (audioManager == null)
        {
            return;
        }

        var serializedAudioManager = new SerializedObject(audioManager);
        var disableAudio = serializedAudioManager.FindProperty("m_DisableAudio");
        if (disableAudio == null)
        {
            return;
        }

        if (disableAudio.boolValue)
        {
            disableAudio.boolValue = false;
            serializedAudioManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        var path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static string ToFullPath(string assetPath)
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("Unable to resolve Unity project root.");
        }

        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }

            var children = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child.gameObject.name == objectName)
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private static void ConfigureAudioImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Failed to get AudioImporter for: " + assetPath);
        }

        importer.forceToMono = true;
        importer.loadInBackground = false;

        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.quality = 1f;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }

    private static void WriteHeartbeatWav(string fullPath)
    {
        const int sampleRate = 48000;
        const int channels = 1;
        const double duration = 4.80;
        var sampleCount = (int)(sampleRate * duration);
        var samples = new float[sampleCount];

        AddHeartbeat(samples, sampleRate, 0.55, 0.88f);
        AddHeartbeat(samples, sampleRate, 2.15, 0.84f);
        AddHeartbeat(samples, sampleRate, 3.75, 0.82f);

        RemoveDcOffset(samples);
        SoftLimit(samples, 1.35f);
        FadeClipEdges(samples, sampleRate, 0.22);
        Normalize(samples, 0.68f);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var writer = new BinaryWriter(File.Open(fullPath, FileMode.Create, FileAccess.Write)))
        {
            var dataLength = samples.Length * channels * sizeof(short);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * sizeof(short));
            writer.Write((short)(channels * sizeof(short)));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            for (var i = 0; i < samples.Length; i++)
            {
                var value = Mathf.Clamp(samples[i], -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(value * short.MaxValue));
            }
        }
    }

    private static void AddHeartbeat(float[] samples, int sampleRate, double startTime, float amplitude)
    {
        AddDeepThump(samples, sampleRate, startTime, 86.0, 58.0, amplitude, 0.62);
        AddDeepThump(samples, sampleRate, startTime + 0.29, 76.0, 54.0, amplitude * 0.70f, 0.54);
    }

    private static void AddDeepThump(float[] samples, int sampleRate, double startTime, double startFrequency, double endFrequency, float amplitude, double lengthSeconds)
    {
        var startSample = Math.Max(0, (int)(startTime * sampleRate));
        var length = Math.Min(samples.Length - startSample, (int)(lengthSeconds * sampleRate));
        if (length <= 0)
        {
            return;
        }

        const double sweepRate = 5.2;
        for (var i = 0; i < length; i++)
        {
            var t = i / (double)sampleRate;
            var phase = 2.0 * Math.PI * (endFrequency * t + (startFrequency - endFrequency) * (1.0 - Math.Exp(-sweepRate * t)) / sweepRate);
            var attack = Smooth01(Math.Min(1.0, t / 0.042));
            var decay = Math.Exp(-t * 4.45);
            var release = Smooth01(Math.Min(1.0, (lengthSeconds - t) / 0.14));
            var envelope = attack * decay * release;

            var sub = 0.68 * Math.Sin(phase);
            var chest = 0.58 * Math.Sin(phase * 1.72 + 0.32);
            var pressure = 0.16 * Math.Sin(phase * 0.72 - 0.18);
            var softKnock = 0.06 * Math.Sin(phase * 2.55) * Math.Exp(-t * 14.0);
            samples[startSample + i] += (float)((sub + chest + pressure + softKnock) * envelope * amplitude);
        }
    }

    private static double Smooth01(double value)
    {
        value = Math.Max(0.0, Math.Min(1.0, value));
        return value * value * (3.0 - 2.0 * value);
    }

    private static void RemoveDcOffset(float[] samples)
    {
        if (samples.Length == 0)
        {
            return;
        }

        double sum = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            sum += samples[i];
        }

        var average = (float)(sum / samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] -= average;
        }
    }

    private static void SoftLimit(float[] samples, float drive)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)Math.Tanh(samples[i] * drive) / drive;
        }
    }

    private static void FadeClipEdges(float[] samples, int sampleRate, double fadeSeconds)
    {
        var fadeSamples = Math.Min(samples.Length / 2, Math.Max(1, (int)(sampleRate * fadeSeconds)));
        for (var i = 0; i < fadeSamples; i++)
        {
            var gain = (float)Smooth01(i / (double)fadeSamples);
            samples[i] *= gain;
            samples[samples.Length - 1 - i] *= gain;
        }
    }

    private static void Normalize(float[] samples, float targetPeak)
    {
        var peak = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        }

        if (peak <= 0f)
        {
            return;
        }

        var gain = targetPeak / peak;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class UiSfxAudioInstaller
{
    private const string MenuPath = "RhythmRPG/Editors/Audio/Install UI SFX Pack";
    private const string ResourceFolder = "Assets/0.MainProject/Resources/Audio/UI";
    private const string StreamingFolder = "Assets/StreamingAssets/UIAudio";

    [MenuItem(MenuPath)]
    public static void Install()
    {
        EnsureAssetFolder("Assets/0.MainProject/Resources", "Audio");
        EnsureAssetFolder("Assets/0.MainProject/Resources/Audio", "UI");
        EnsureAssetFolder("Assets", "StreamingAssets");
        EnsureAssetFolder("Assets/StreamingAssets", "UIAudio");

        WritePair("Ui_Hover.wav", CreateHover());
        WritePair("Ui_Click.wav", CreateClick());
        WritePair("Ui_Confirm.wav", CreateConfirm());
        WritePair("Ui_Cancel.wav", CreateCancel());
        WritePair("Ui_Toggle.wav", CreateToggle());
        WritePair("Ui_SliderTick.wav", CreateSliderTick());
        WritePair("Ui_Open.wav", CreateOpen());
        WritePair("Ui_Close.wav", CreateClose());
        WritePair("Ui_Error.wav", CreateError());
        WritePair("Ui_TextFocus.wav", CreateTextFocus());

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureFolderAudioImport(ResourceFolder);
        AssetDatabase.SaveAssets();

        Debug.Log("Installed UI SFX pack in Resources and StreamingAssets for FMOD Core playback.");
    }

    private static void WritePair(string fileName, float[] samples)
    {
        WriteWav(ToFullPath(ResourceFolder + "/" + fileName), samples, 48000);
        WriteWav(ToFullPath(StreamingFolder + "/" + fileName), samples, 48000);
    }

    private static float[] CreateHover()
    {
        var samples = NewClip(0.050);
        AddNoise(samples, 48000, 0.00, 0.040, 0.018f, 0.004, 0.018);
        AddTone(samples, 48000, 0.00, 0.036, 340.0, 300.0, 0.025f, 0.006, 0.018);
        Finish(samples, 0.11f);
        return samples;
    }

    private static float[] CreateClick()
    {
        var samples = NewClip(0.095);
        AddNoise(samples, 48000, 0.00, 0.070, 0.115f, 0.003, 0.034);
        AddTone(samples, 48000, 0.004, 0.072, 245.0, 205.0, 0.120f, 0.006, 0.036);
        AddTone(samples, 48000, 0.002, 0.052, 420.0, 340.0, 0.070f, 0.004, 0.026);
        Finish(samples, 0.26f);
        return samples;
    }

    private static float[] CreateConfirm()
    {
        var samples = NewClip(0.145);
        AddNoise(samples, 48000, 0.00, 0.090, 0.085f, 0.004, 0.045);
        AddTone(samples, 48000, 0.006, 0.105, 310.0, 420.0, 0.120f, 0.010, 0.052);
        AddTone(samples, 48000, 0.026, 0.080, 480.0, 560.0, 0.055f, 0.010, 0.040);
        Finish(samples, 0.28f);
        return samples;
    }

    private static float[] CreateCancel()
    {
        var samples = NewClip(0.130);
        AddNoise(samples, 48000, 0.00, 0.086, 0.075f, 0.004, 0.040);
        AddTone(samples, 48000, 0.004, 0.095, 330.0, 240.0, 0.115f, 0.008, 0.048);
        AddTone(samples, 48000, 0.012, 0.074, 205.0, 175.0, 0.050f, 0.008, 0.038);
        Finish(samples, 0.25f);
        return samples;
    }

    private static float[] CreateToggle()
    {
        var samples = NewClip(0.085);
        AddNoise(samples, 48000, 0.00, 0.060, 0.085f, 0.003, 0.030);
        AddTone(samples, 48000, 0.006, 0.054, 300.0, 360.0, 0.070f, 0.006, 0.028);
        Finish(samples, 0.23f);
        return samples;
    }

    private static float[] CreateSliderTick()
    {
        var samples = NewClip(0.040);
        AddNoise(samples, 48000, 0.00, 0.030, 0.030f, 0.002, 0.014);
        AddTone(samples, 48000, 0.000, 0.028, 380.0, 320.0, 0.025f, 0.004, 0.014);
        Finish(samples, 0.10f);
        return samples;
    }

    private static float[] CreateOpen()
    {
        var samples = NewClip(0.150);
        AddNoise(samples, 48000, 0.00, 0.105, 0.065f, 0.008, 0.052);
        AddTone(samples, 48000, 0.018, 0.096, 250.0, 420.0, 0.095f, 0.012, 0.048);
        Finish(samples, 0.22f);
        return samples;
    }

    private static float[] CreateClose()
    {
        var samples = NewClip(0.140);
        AddNoise(samples, 48000, 0.00, 0.095, 0.060f, 0.008, 0.048);
        AddTone(samples, 48000, 0.004, 0.092, 420.0, 260.0, 0.095f, 0.010, 0.046);
        Finish(samples, 0.21f);
        return samples;
    }

    private static float[] CreateError()
    {
        var samples = NewClip(0.200);
        AddNoise(samples, 48000, 0.00, 0.130, 0.065f, 0.006, 0.062);
        AddTone(samples, 48000, 0.000, 0.105, 235.0, 195.0, 0.120f, 0.008, 0.052);
        AddTone(samples, 48000, 0.088, 0.084, 315.0, 255.0, 0.070f, 0.008, 0.042);
        Finish(samples, 0.27f);
        return samples;
    }

    private static float[] CreateTextFocus()
    {
        var samples = NewClip(0.060);
        AddNoise(samples, 48000, 0.00, 0.042, 0.022f, 0.004, 0.020);
        AddTone(samples, 48000, 0.000, 0.038, 300.0, 330.0, 0.030f, 0.006, 0.020);
        Finish(samples, 0.10f);
        return samples;
    }

    private static float[] NewClip(double duration)
    {
        return new float[Mathf.CeilToInt((float)(48000 * duration))];
    }

    private static void AddTone(float[] samples, int sampleRate, double start, double duration, double startFrequency, double endFrequency, float amplitude, double attack, double release)
    {
        var startSample = Math.Max(0, (int)(start * sampleRate));
        var length = Math.Min(samples.Length - startSample, (int)(duration * sampleRate));
        if (length <= 0)
            return;

        var phase = 0.0;
        for (var i = 0; i < length; i++)
        {
            var t = i / (double)sampleRate;
            var n = length <= 1 ? 0.0 : i / (double)(length - 1);
            var frequency = Mathf.Lerp((float)startFrequency, (float)endFrequency, (float)Smooth01(n));
            phase += 2.0 * Math.PI * frequency / sampleRate;
            var env = Envelope(t, duration, attack, release);
            samples[startSample + i] += (float)(Math.Sin(phase) * env * amplitude);
        }
    }

    private static void AddNoise(float[] samples, int sampleRate, double start, double duration, float amplitude, double attack, double release)
    {
        var startSample = Math.Max(0, (int)(start * sampleRate));
        var length = Math.Min(samples.Length - startSample, (int)(duration * sampleRate));
        if (length <= 0)
            return;

        var previous = 0.0;
        for (var i = 0; i < length; i++)
        {
            var t = i / (double)sampleRate;
            var white = HashNoise(i + startSample);
            previous = previous * 0.82 + white * 0.18;
            var env = Envelope(t, duration, attack, release);
            samples[startSample + i] += (float)(previous * env * amplitude);
        }
    }

    private static double Envelope(double t, double duration, double attack, double release)
    {
        var a = attack <= 0.0 ? 1.0 : Smooth01(Math.Min(1.0, t / attack));
        var r = release <= 0.0 ? 1.0 : Smooth01(Math.Min(1.0, (duration - t) / release));
        return a * r;
    }

    private static double HashNoise(int sample)
    {
        unchecked
        {
            var x = (uint)(sample * 747796405 + 2891336453);
            x = ((x >> ((int)(x >> 28) + 4)) ^ x) * 277803737;
            x = (x >> 22) ^ x;
            return (x / (double)uint.MaxValue) * 2.0 - 1.0;
        }
    }

    private static void Finish(float[] samples, float targetPeak)
    {
        RemoveDcOffset(samples);
        SoftLimit(samples, 1.18f);
        FadeEdges(samples, 48000, 0.006);
        Normalize(samples, targetPeak);
    }

    private static void RemoveDcOffset(float[] samples)
    {
        if (samples.Length == 0)
            return;

        double sum = 0.0;
        for (var i = 0; i < samples.Length; i++)
            sum += samples[i];

        var average = (float)(sum / samples.Length);
        for (var i = 0; i < samples.Length; i++)
            samples[i] -= average;
    }

    private static void SoftLimit(float[] samples, float drive)
    {
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Tanh(samples[i] * drive) / drive;
    }

    private static void FadeEdges(float[] samples, int sampleRate, double fadeSeconds)
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
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));

        if (peak <= 0f)
            return;

        var gain = targetPeak / peak;
        for (var i = 0; i < samples.Length; i++)
            samples[i] *= gain;
    }

    private static double Smooth01(double value)
    {
        value = Math.Max(0.0, Math.Min(1.0, value));
        return value * value * (3.0 - 2.0 * value);
    }

    private static void WriteWav(string fullPath, float[] samples, int sampleRate)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using (var writer = new BinaryWriter(File.Open(fullPath, FileMode.Create, FileAccess.Write)))
        {
            const int channels = 1;
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

    private static void ConfigureFolderAudioImport(string folder)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                continue;

            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.PCM,
                quality = 1f,
                preloadAudioData = true,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
            };
            importer.SaveAndReimport();
        }
    }

    private static void EnsureAssetFolder(string parent, string child)
    {
        var path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static string ToFullPath(string assetPath)
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(projectRoot))
            throw new InvalidOperationException("Unable to resolve Unity project root.");

        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif

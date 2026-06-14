using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Shared.Data;
using Newtonsoft.Json;
using FMODUnity;

public class FMODDrumSequencer : MonoBehaviour
{
    private const string MasterVolumeKey = "Options.MasterVolume";
    private const string InGameVolumeKey = "Options.InGameVolume";
    private const float DefaultMasterVolume = 0.85f;
    private const float DefaultInGameVolume = 0.80f;

    private static readonly HashSet<string> ValidFmodEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Synthwave_Dagger", "Synthwave_Greatsword", "Synthwave_Bow", "Synthwave_Parry", "Synthwave_Staff",
        "Lofi_Dagger", "Lofi_Greatsword", "Lofi_Bow", "Lofi_Parry", "Lofi_Staff",
        "Funk_Dagger", "Funk_Greatsword", "Funk_Bow", "Funk_Parry", "Funk_Staff",
        "Orchestral_Dagger", "Orchestral_Greatsword", "Orchestral_Bow", "Orchestral_Parry", "Orchestral_Staff",
        "Jazz_Dagger", "Jazz_Greatsword", "Jazz_Bow", "Jazz_Parry", "Jazz_Staff",
        "Bass_Synthwave", "Bass_Lofi", "Bass_Funk", "Bass_Orchestral", "Bass_Jazz",
        "Melody_Synthwave", "Melody_Lofi", "Melody_Funk", "Melody_Orchestral", "Melody_Jazz",
        "Kick", "HiHat", "TestSmith"
    };

    [Header("Rhythm Data")]
    public TextAsset rhythmJsonAsset;

    [Header("Settings")]
    public float lookAheadSec = 0.1f;    // 0.1초 미리 탐색해서 발동 준비
    public bool enableSequencer = true;

    [System.Serializable]
    public struct DrumSoundMap
    {
        [Tooltip("JSON에 입력한 이름 (예: Kick, BassKick)")]
        public string soundKey;
        [Tooltip("실제 등록된 FMOD 이벤트를 지정하세요")]
        public EventReference fmodEvent;
    }

    [Header("FMOD Event Mapping")]
    [Tooltip("JSON의 SoundKey 텍스트와 실제 FMOD 이벤트를 연결합니다.")]
    public List<DrumSoundMap> drumMappings = new List<DrumSoundMap>();

    public static FMODDrumSequencer Instance { get; private set; }

    private RhythmStageData _stageData;
    private RhythmBlock _currentBlock;
    private long _blockStartBeatIndex = 0;

    // 이미 스케줄링(재생)한 노트들을 추적 (Measure, Tick 베이스)
    private readonly HashSet<ScheduledNoteKey> _playedNotes = new HashSet<ScheduledNoteKey>();
    private readonly Dictionary<string, RhythmBlock> _blocksById = new Dictionary<string, RhythmBlock>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EventReference> _drumMappingsByKey = new Dictionary<string, EventReference>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FMOD.Sound> _simulatorSoundCache = new Dictionary<string, FMOD.Sound>();
    private float _nextInitializeAttemptTime;

    private struct PendingPlay
    {
        public double targetTimeSec;
        public FMOD.Studio.EventInstance instance;
        public string soundKey;
    }
    private readonly List<PendingPlay> _pendingPlays = new List<PendingPlay>();
    private const float InitializeRetryInterval = 0.5f;

    private readonly struct ScheduledNoteKey : IEquatable<ScheduledNoteKey>
    {
        private readonly long _blockStartBeat;
        private readonly BassNote _note;

        public ScheduledNoteKey(long blockStartBeat, BassNote note)
        {
            _blockStartBeat = blockStartBeat;
            _note = note;
        }

        public bool Equals(ScheduledNoteKey other)
        {
            return _blockStartBeat == other._blockStartBeat && ReferenceEquals(_note, other._note);
        }

        public override bool Equals(object obj)
        {
            return obj is ScheduledNoteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_blockStartBeat.GetHashCode() * 397) ^ (_note != null ? _note.GetHashCode() : 0);
            }
        }
    }

    private void ClearPendingPlays()
    {
        foreach (var p in _pendingPlays)
        {
            if (p.instance.isValid())
            {
                p.instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                p.instance.release();
            }
        }
        _pendingPlays.Clear();
    }

    private const int SimulatorSampleRate = 48000;
    private const float SimulatorMasterVolume = 0.7f;
    private static bool _hasRuntimeVolumePreview;
    private static float _runtimePreviewMasterVolume = DefaultMasterVolume;
    private static float _runtimePreviewInGameVolume = DefaultInGameVolume;
    private static readonly Dictionary<string, float> JazzToneFrequencies = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "Bass_C2", 65.41f }, { "Bass_D2", 73.42f }, { "Bass_Eb2", 77.78f }, { "Bass_F2", 87.31f },
        { "Bass_Gb2", 92.50f }, { "Bass_G2", 98.00f }, { "Bass_Ab2", 103.83f }, { "Bass_A2", 110.00f },
        { "Bass_Bb2", 116.54f }, { "Bass_B2", 123.47f }, { "Bass_C3", 130.81f }, { "Bass_Db3", 138.59f },
        { "Bass_D3", 146.83f }, { "Bass_Eb3", 155.56f }, { "Bass_E3", 164.81f }, { "Bass_F3", 174.61f },
        { "Bass_Gb3", 185.00f }, { "Bass_G3", 196.00f }, { "Bass_Ab3", 207.65f }, { "Bass_A3", 220.00f },
        { "Bass_Bb3", 233.08f }, { "Bass_B3", 246.94f }, { "Bass_C4", 261.63f },
        { "Sax_C4", 261.63f }, { "Sax_D4", 293.66f }, { "Sax_Eb4", 311.13f }, { "Sax_F4", 349.23f },
        { "Sax_Gb4", 369.99f }, { "Sax_G4", 392.00f }, { "Sax_Ab4", 415.30f }, { "Sax_A4", 440.00f },
        { "Sax_Bb4", 466.16f }, { "Sax_B4", 493.88f }, { "Sax_C5", 523.25f }, { "Sax_Db5", 554.37f },
        { "Sax_D5", 587.33f }, { "Sax_Eb5", 622.25f }, { "Sax_F5", 698.46f }, { "Sax_Gb5", 739.99f },
        { "Sax_G5", 783.99f }, { "Sax_Ab5", 830.61f }, { "Sax_A5", 880.00f }, { "Sax_Bb5", 932.33f },
        { "Sax_C6", 1046.50f }
    };

    private static readonly Dictionary<string, float[]> JazzChordFrequencies = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "Piano_Cm7", new[] { 130.81f, 155.56f, 196.00f, 233.08f } },
        { "Piano_Cm9", new[] { 130.81f, 155.56f, 196.00f, 233.08f, 293.66f } },
        { "Piano_F7", new[] { 174.61f, 220.00f, 261.63f, 311.13f } },
        { "Piano_Fm7", new[] { 174.61f, 207.65f, 261.63f, 311.13f } },
        { "Piano_Bb7", new[] { 233.08f, 293.66f, 349.23f, 415.30f } },
        { "Piano_Bbmaj7", new[] { 116.54f, 146.83f, 174.61f, 220.00f } },
        { "Piano_Ebmaj7", new[] { 155.56f, 196.00f, 233.08f, 293.66f } },
        { "Piano_Abmaj7", new[] { 207.65f, 261.63f, 311.13f, 392.00f } },
        { "Piano_Ab7#11", new[] { 207.65f, 246.94f, 311.13f, 349.23f } },
        { "Piano_Dm7b5", new[] { 146.83f, 174.61f, 207.65f, 261.63f } },
        { "Piano_Dbmaj7", new[] { 138.59f, 174.61f, 207.65f, 261.63f } },
        { "Piano_G7alt", new[] { 196.00f, 246.94f, 349.23f, 415.30f } }
    };

    private void Awake()
    {
        Instance = this;
        RebuildDrumMappingCache();
    }

    public static void SetRuntimeVolumePreview(float masterVolume, float inGameVolume)
    {
        _hasRuntimeVolumePreview = true;
        _runtimePreviewMasterVolume = Mathf.Clamp01(masterVolume);
        _runtimePreviewInGameVolume = Mathf.Clamp01(inGameVolume);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildDrumMappingCache();
    }
#endif

    private void OnDestroy()
    {
        ClearPendingPlays();

        // 씬 전환/파괴 시 모든 FMOD 재생 중지하여 오디오 누수 방지
        if (FMODUnity.RuntimeManager.IsInitialized)
        {
            try
            {
                FMOD.Studio.Bus masterBus;
                if (FMODUnity.RuntimeManager.StudioSystem.getBus("bus:/", out masterBus) == FMOD.RESULT.OK)
                {
                    masterBus.stopAllEvents(FMOD.Studio.STOP_MODE.IMMEDIATE);
                }
                FMOD.ChannelGroup masterCG;
                if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) == FMOD.RESULT.OK)
                {
                    masterCG.stop();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FMODDrum] Failed to stop FMOD systems in OnDestroy: {e.Message}");
            }
        }

        foreach (var sound in _simulatorSoundCache.Values)
        {
            if (sound.hasHandle())
                sound.release();
        }

        _simulatorSoundCache.Clear();

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        InitializeIfRequired();
    }

    private void InitializeIfRequired()
    {
        string resolvedSongKey = null;
        if (P2PContentDirector.Instance != null)
        {
            resolvedSongKey = P2PContentDirector.Instance.GetStageSongKey();
        }

        // If we already have the correct stage loaded, we don't need to do anything.
        if (_stageData != null && !string.IsNullOrEmpty(resolvedSongKey) && string.Equals(_stageData.StageId, resolvedSongKey, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Also if stage data is already loaded and resolvedSongKey is empty/null, we can keep the fallback/current data for now.
        if (_stageData != null && string.IsNullOrEmpty(resolvedSongKey))
        {
            return;
        }

        string rhythmJsonText = null;

        if (!string.IsNullOrEmpty(resolvedSongKey))
        {
            if (P2PServerContentResolver.TryLoadRhythmJson(resolvedSongKey, out string serverJson))
            {
                rhythmJsonText = serverJson;
                Debug.Log($"[FMODDrumSequencer] Dynamically loaded Rhythm JSON from server content using SongKey: {resolvedSongKey}");
            }
            else
            {
                var textAsset = Resources.Load<TextAsset>(resolvedSongKey)
                                ?? Resources.Load<TextAsset>($"Data/Sound/Json/{resolvedSongKey}_Rhythm")
                                ?? Resources.Load<TextAsset>($"Data/Sound/Json/{resolvedSongKey}")
                                ?? Resources.Load<TextAsset>($"Data/Stage/{resolvedSongKey}");
                if (textAsset != null)
                {
                    rhythmJsonText = textAsset.text;
                    Debug.Log($"[FMODDrumSequencer] Dynamically loaded Rhythm JSON from Resources using SongKey: {resolvedSongKey}");
                }
            }
        }

        if (string.IsNullOrEmpty(rhythmJsonText) && rhythmJsonAsset != null)
        {
            if (_stageData == null) // Only use fallback if we haven't loaded anything yet
            {
                rhythmJsonText = rhythmJsonAsset.text;
                Debug.Log($"[FMODDrumSequencer] Fallback: Loaded Rhythm JSON from Inspector assigned asset.");
            }
        }

        if (!string.IsNullOrEmpty(rhythmJsonText))
        {
            try
            {
                var newData = JsonConvert.DeserializeObject<RhythmStageData>(rhythmJsonText);
                if (newData != null && newData.Blocks.Count > 0)
                {
                    _stageData = newData;
                    _currentBlock = _stageData.Blocks[0];
                    _blockStartBeatIndex = 0;
                    _playedNotes.Clear(); // Clear played notes on reload to avoid mismatch
                    ClearPendingPlays(); // Clear pending plays on stage reload
                    RebuildBlockCache(_stageData);
                    RebuildDrumMappingCache();
                    WarmSimulatorSoundCache(_stageData);
                    Debug.Log($"[FMODDrumSequencer] Loaded Stage: {_stageData.StageId} | Found {_currentBlock.BassPattern.Count} Bass Notes in Block 0.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FMODDrumSequencer] Failed to parse JSON: {e.Message}");
            }
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextInitializeAttemptTime)
        {
            _nextInitializeAttemptTime = Time.unscaledTime + InitializeRetryInterval;
            InitializeIfRequired();
        }

        if (!enableSequencer || _stageData == null || _currentBlock == null) return;
        if (RhythmClient.Instance == null || RhythmClient.Instance.ServerSongStartMs == 0) return;

        double currentServerTimeSec = RhythmClient.Instance.GetCurrentServerTimeMs() / 1000.0;

        // Process pending play queue
        for (int i = _pendingPlays.Count - 1; i >= 0; i--)
        {
            if (currentServerTimeSec >= _pendingPlays[i].targetTimeSec)
            {
                var play = _pendingPlays[i];
                if (play.instance.isValid())
                {
                    double driftSec = currentServerTimeSec - play.targetTimeSec;
                    if (driftSec > 0.002) // Only compensate if drift is greater than 2ms
                    {
                        int driftMs = (int)(driftSec * 1000.0);
                        if (driftMs < 300) // Prevent clicking on heavy frame drops
                        {
                            play.instance.setTimelinePosition(driftMs);
                        }
                    }
                    play.instance.start();
                    play.instance.release();
                }
                _pendingPlays.RemoveAt(i);
            }
        }

        double lookAheadTimeSec = currentServerTimeSec + lookAheadSec;

        int beatsPerMeasure = _stageData.TimeSignatureNum;
        int totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;

        // 1. 현재 블록의 끝나는 시간 판별
        double blockDurationSec = (totalBeatsInBlock * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0;
        double blockEndSec = (RhythmClient.Instance.ServerSongStartMs + _blockStartBeatIndex * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0 + blockDurationSec;

        // 2. 블록 전환 (현재 실제 비트가 현재 블록의 범위를 크게 벗어났을 경우 단번에 워프하여 과거 노트 난사 방지)
        long currentBeatIndex = RhythmClient.Instance.GetCurrentBeatIndex();
        if (currentBeatIndex >= _blockStartBeatIndex + totalBeatsInBlock)
        {
            while (currentBeatIndex >= _blockStartBeatIndex + totalBeatsInBlock)
            {
                var nextBlock = ResolveNextBlock(_currentBlock);
                _currentBlock = nextBlock;
                _blockStartBeatIndex += totalBeatsInBlock;

                totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;
            }

            // 워프 완료 후, 새로 계산된 블록 기준 EndSec 재계산
            blockDurationSec = (totalBeatsInBlock * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0;
            blockEndSec = (RhythmClient.Instance.ServerSongStartMs + _blockStartBeatIndex * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0 + blockDurationSec;
        }
        else if (currentServerTimeSec >= blockEndSec)
        {
            // 점진적인 일반 블록 전환 (1비트 수준 미세 초과 시)
            var nextBlock = ResolveNextBlock(_currentBlock);
            _currentBlock = nextBlock;
            _blockStartBeatIndex += totalBeatsInBlock;

            // 변환된 블록 기준으로 EndSec 재계산
            totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;
            blockDurationSec = (totalBeatsInBlock * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0;
            blockEndSec = (RhythmClient.Instance.ServerSongStartMs + _blockStartBeatIndex * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0 + blockDurationSec;
        }

        // 3. 현재 블록 스케줄링
        ScheduleNotesForBlock(_currentBlock, _blockStartBeatIndex, currentServerTimeSec, lookAheadTimeSec);

        // 4. 미리보기(Look-Ahead) 윈도우가 다음 블록 영역을 침범할 경우 딜레이 방지를 위해 미래 블록도 미리 스케줄링!
        if (lookAheadTimeSec >= blockEndSec)
        {
            var nextBlock = ResolveNextBlock(_currentBlock);
            ScheduleNotesForBlock(nextBlock, _blockStartBeatIndex + totalBeatsInBlock, currentServerTimeSec, lookAheadTimeSec);
        }
    }

    private void RebuildBlockCache(RhythmStageData stageData)
    {
        _blocksById.Clear();
        if (stageData?.Blocks == null)
            return;

        for (int i = 0; i < stageData.Blocks.Count; i++)
        {
            RhythmBlock block = stageData.Blocks[i];
            if (block != null && !string.IsNullOrEmpty(block.BlockId))
                _blocksById[block.BlockId] = block;
        }
    }

    private void RebuildDrumMappingCache()
    {
        _drumMappingsByKey.Clear();
        if (drumMappings == null)
            return;

        for (int i = 0; i < drumMappings.Count; i++)
        {
            DrumSoundMap mapping = drumMappings[i];
            if (!string.IsNullOrEmpty(mapping.soundKey))
                _drumMappingsByKey[mapping.soundKey] = mapping.fmodEvent;
        }
    }

    private RhythmBlock ResolveNextBlock(RhythmBlock block)
    {
        if (block == null || string.IsNullOrEmpty(block.DefaultNextBlock))
            return block;

        return _blocksById.TryGetValue(block.DefaultNextBlock, out RhythmBlock nextBlock) && nextBlock != null
            ? nextBlock
            : block;
    }

    private void ScheduleNotesForBlock(RhythmBlock block, long blockStartAbsoluteBeat, double currentTimeSec, double lookAheadTimeSec)
    {
        int beatsPerMeasure = _stageData.TimeSignatureNum;

        foreach (var note in block.BassPattern)
        {
            // 이 노트의 절대 비트 위치 계산
            long noteBeatAbsolute = blockStartAbsoluteBeat + (note.MeasureIndex * beatsPerMeasure) + (note.Tick / _stageData.TicksPerBeat);
            double noteTimeMs = RhythmClient.Instance.GetBeatTimeMs(noteBeatAbsolute);

            // 미세 조정을 위해 Tick 단위 잔여물 계산
            double tickFraction = (note.Tick % _stageData.TicksPerBeat) / (double)_stageData.TicksPerBeat;
            noteTimeMs += tickFraction * RhythmClient.Instance.GetBeatDurationMs();

            double noteTimeSec = noteTimeMs / 1000.0;

            var noteId = new ScheduledNoteKey(blockStartAbsoluteBeat, note);

            // 아직 재생하지 않았고, 현재 시간 ~ LookAhead 시간 사이에 도달하는 노트인가?
            if (!_playedNotes.Contains(noteId) && noteTimeSec <= lookAheadTimeSec)
            {
                _playedNotes.Add(noteId);
                ScheduleFMODEvent(block, note, noteTimeSec, currentTimeSec);
            }
        }
    }

    private void ScheduleFMODEvent(RhythmBlock block, BassNote note, double targetTimeSec, double currentTimeSec)
    {
        if (TryScheduleBassMelodySimulatorSound(block, note, targetTimeSec))
            return;

        FMOD.Studio.EventInstance instance = default;
        bool hasInstance = false;

        // 1. JSON 문자열(SoundKey)과 맵핑된 FMOD EventReference 찾기
        if (_drumMappingsByKey.TryGetValue(note.SoundKey, out EventReference mappedEvent) && !mappedEvent.IsNull)
        {
            try
            {
                instance = RuntimeManager.CreateInstance(mappedEvent);
                hasInstance = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FMODDrum] Event Create Failed for Key {note.SoundKey} via EventReference: {e.Message}");
                return;
            }
        }
        else
        {
            // 2. 맵핑이 없거나 빈 경우 동적 FMOD 이벤트 경로 매핑 시도
            string eventPath = GetDynamicFmodEventPath(note.SoundKey);
            if (!string.IsNullOrEmpty(eventPath))
            {
                try
                {
                    instance = RuntimeManager.CreateInstance(eventPath);
                    hasInstance = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[FMODDrum] Event Create Failed for Key {note.SoundKey} via dynamic path {eventPath}: {e.Message}");
                    return;
                }
            }
        }

        if (!hasInstance)
        {
            Debug.LogError($"[FMODDrum] Mapping for '{note.SoundKey}' is missing or EventReference is empty, and dynamic path could not be resolved.");
            return;
        }

        instance.setVolume(GetScaledNoteVolume(note));
        
        // 악보 개별 피치 오프셋에 화성 진행(Chord) 피치 오프셋을 더해 최종 조옮김 피치 전달
        int finalPitchOffset = note.PitchOffset + GetPitchOffsetAt(block, note.MeasureIndex, note.Tick);
        instance.setParameterByName("PitchOffset", finalPitchOffset);

        double latestCurrentTimeSec = GetCurrentServerTimeSec();
        if (targetTimeSec <= latestCurrentTimeSec)
        {
            // 지연이 없거나 이미 늦었으면 즉시 재생
            instance.start();
            instance.release();
        }
        else if (TryStartStudioEventAtDspTime(instance, targetTimeSec))
        {
            // DSP clock 기반으로 예약되었으므로 프레임 Update 지터를 타지 않습니다.
        }
        else
        {
            // DSP 예약 실패 시에만 대기 큐로 폴백합니다.
            _pendingPlays.Add(new PendingPlay
            {
                targetTimeSec = targetTimeSec,
                instance = instance,
                soundKey = note.SoundKey
            });
        }
    }

    private bool TryScheduleBassMelodySimulatorSound(RhythmBlock block, BassNote note, double targetTimeSec)
    {
        string toneKey = GetToneKey(note);
        if (!IsBassMelodySimulatorSound(toneKey))
            return false;

        if (!TryGetOrCreateSimulatorSound(block, note, out FMOD.Sound sound))
            return true;

        double delaySec = Math.Max(0.0, targetTimeSec - GetCurrentServerTimeSec());
        if (!TryGetTargetDspClock(delaySec, out ulong targetDSPClock))
        {
            Debug.LogError($"[FMODDrum] Could not resolve FMOD DSP clock for '{note.SoundKey}'.");
            return true;
        }

        FMOD.RESULT playResult = RuntimeManager.CoreSystem.playSound(sound, default, true, out FMOD.Channel channel);
        if (playResult != FMOD.RESULT.OK)
        {
            Debug.LogError($"[FMODDrum] BassMelody simulator play failed for '{note.SoundKey}'. Result={playResult}");
            return true;
        }

        channel.setVolume(GetScaledNoteVolume(note));
        if (delaySec > 0)
            channel.setDelay(targetDSPClock, 0, false);
        channel.setPaused(false);

        return true;
    }

    private void WarmSimulatorSoundCache(RhythmStageData stageData)
    {
        if (stageData == null || stageData.Blocks == null)
            return;

        foreach (var block in stageData.Blocks)
        {
            if (block?.BassPattern == null)
                continue;

            foreach (var note in block.BassPattern)
            {
                if (IsBassMelodySimulatorSound(GetToneKey(note)))
                    TryGetOrCreateSimulatorSound(block, note, out _);
            }
        }
    }

    private bool TryGetOrCreateSimulatorSound(RhythmBlock block, BassNote note, out FMOD.Sound sound)
    {
        sound = default;

        SimulatorChord chord = GetChordAt(block, note.MeasureIndex, note.Tick);
        string cacheKey = BuildSimulatorCacheKey(note, chord);

        if (_simulatorSoundCache.TryGetValue(cacheKey, out sound))
            return true;

        byte[] wavBytes = RenderBassMelodySimulatorWav(note, chord);
        var exInfo = new FMOD.CREATESOUNDEXINFO
        {
            cbsize = System.Runtime.InteropServices.Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>(),
            length = (uint)wavBytes.Length
        };

        FMOD.MODE mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.CREATESAMPLE | FMOD.MODE._2D | FMOD.MODE.LOOP_OFF;
        FMOD.RESULT createResult = RuntimeManager.CoreSystem.createSound(wavBytes, mode, ref exInfo, out sound);
        if (createResult != FMOD.RESULT.OK)
        {
            Debug.LogError($"[FMODDrum] BassMelody simulator sound create failed for '{note.SoundKey}'. Result={createResult}");
            return false;
        }

        _simulatorSoundCache[cacheKey] = sound;
        return true;
    }

    private bool TryStartStudioEventAtDspTime(FMOD.Studio.EventInstance instance, double targetTimeSec)
    {
        double delaySec = Math.Max(0.0, targetTimeSec - GetCurrentServerTimeSec());
        
        RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out _, out _);
        if (sampleRate <= 0) sampleRate = SimulatorSampleRate;

        // SCHEDULE_DELAY는 FMOD Studio 이벤트의 상대 딜레이 샘플 수를 필요로 합니다. 절대 DSP 클럭을 넘길 시 오랜 무음 딜레이가 발생합니다.
        float delaySamples = (float)Math.Max(0.0, Math.Round(delaySec * sampleRate));

        FMOD.RESULT scheduleResult = instance.setProperty(FMOD.Studio.EVENT_PROPERTY.SCHEDULE_DELAY, delaySamples);
        if (scheduleResult != FMOD.RESULT.OK)
        {
            Debug.LogWarning($"[FMODDrum] Studio DSP schedule failed. Result={scheduleResult}");
            return false;
        }

        instance.start();
        instance.release();
        return true;
    }

    private static double GetCurrentServerTimeSec()
    {
        return RhythmClient.Instance != null
            ? RhythmClient.Instance.GetCurrentServerTimeMs() / 1000.0
            : Time.unscaledTimeAsDouble;
    }

    private static float GetScaledNoteVolume(BassNote note)
    {
        var noteVolume = note != null && note.VolumeMultiplier > 0f ? note.VolumeMultiplier : 1.0f;
        return Mathf.Max(0f, noteVolume) * GetInGameVolumeScale();
    }

    private static float GetInGameVolumeScale()
    {
        return GetSavedVolume(MasterVolumeKey, DefaultMasterVolume)
               * GetSavedVolume(InGameVolumeKey, DefaultInGameVolume);
    }

    private static float GetSavedVolume(string key, float fallback)
    {
        if (_hasRuntimeVolumePreview)
        {
            if (key == MasterVolumeKey)
                return _runtimePreviewMasterVolume;
            if (key == InGameVolumeKey)
                return _runtimePreviewInGameVolume;
        }

        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
    }

    private static bool TryGetTargetDspClock(double delaySec, out ulong targetDSPClock)
    {
        targetDSPClock = 0;

        RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup masterCG);
        if (!masterCG.hasHandle())
            return false;

        FMOD.RESULT clockResult = masterCG.getDSPClock(out ulong dspClock, out ulong parentClock);
        if (clockResult != FMOD.RESULT.OK)
            return false;

        FMOD.RESULT formatResult = RuntimeManager.CoreSystem.getSoftwareFormat(
            out int sampleRate,
            out FMOD.SPEAKERMODE speakerMode,
            out int numRawSpeakers);

        if (formatResult != FMOD.RESULT.OK || sampleRate <= 0)
            sampleRate = SimulatorSampleRate;

        ulong delaySamples = (ulong)Math.Max(0.0, Math.Round(delaySec * sampleRate));
        targetDSPClock = dspClock + delaySamples;
        return true;
    }

    private static int GetPitchOffsetAt(RhythmBlock block, long measureIndex, long tick)
    {
        if (block == null || block.ChordEvents == null || block.ChordEvents.Count == 0)
            return 0;

        int beatsPerMeasure = Instance != null && Instance._stageData != null ? Instance._stageData.TimeSignatureNum : 4;
        int ticksPerBeat = Instance != null && Instance._stageData != null ? Instance._stageData.TicksPerBeat : 480;
        long noteAbsoluteTick = (measureIndex * beatsPerMeasure * ticksPerBeat) + tick;

        ChordEvent bestChord = null;
        long bestChordTick = -1;

        foreach (var chord in block.ChordEvents)
        {
            long chordAbsoluteTick = (chord.MeasureIndex * beatsPerMeasure * ticksPerBeat) + chord.Tick;
            if (chordAbsoluteTick <= noteAbsoluteTick)
            {
                if (chordAbsoluteTick > bestChordTick)
                {
                    bestChordTick = chordAbsoluteTick;
                    bestChord = chord;
                }
            }
        }

        return bestChord != null ? bestChord.PitchOffset : 0;
    }

    private static bool IsBassMelodySimulatorSound(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey))
            return false;

        return soundKey.StartsWith("SlapBass_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("FunkGuitar", StringComparison.OrdinalIgnoreCase) ||
               soundKey.Equals("Clav_Chop", StringComparison.OrdinalIgnoreCase) ||
               IsEnsembleSimulatorSound(soundKey) ||
               IsJazzSimulatorSound(soundKey);
    }

    private static bool IsEnsembleSimulatorSound(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey))
            return false;

        return soundKey.StartsWith("SynthBass_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("SynthLead_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("SubBass_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("Rhodes_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("LofiFlute_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("OrchBass_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("Timpani_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("Violin_", StringComparison.OrdinalIgnoreCase) ||
               soundKey.StartsWith("Harp_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJazzSimulatorSound(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey))
            return false;

        return JazzToneFrequencies.ContainsKey(soundKey) ||
               JazzChordFrequencies.ContainsKey(soundKey);
    }

    public static bool UsesBassMelodySimulator(BassNote note)
    {
        return note != null && IsBassMelodySimulatorSound(GetToneKey(note));
    }

    public static string GetRuntimeToneKey(BassNote note)
    {
        return note == null ? string.Empty : GetToneKey(note);
    }

    public static byte[] RenderBassMelodySimulatorPreviewWav(RhythmBlock block, BassNote note)
    {
        if (note == null || !UsesBassMelodySimulator(note))
            return null;

        SimulatorChord chord = GetChordAt(block, note.MeasureIndex, note.Tick);
        return RenderBassMelodySimulatorWav(note, chord);
    }

    private static bool IsBassMelodySimulatorMelody(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey))
            return false;

        return soundKey.IndexOf("lead", StringComparison.OrdinalIgnoreCase) >= 0 ||
               soundKey.IndexOf("flute", StringComparison.OrdinalIgnoreCase) >= 0 ||
               soundKey.IndexOf("guitar", StringComparison.OrdinalIgnoreCase) >= 0 ||
               soundKey.IndexOf("violin", StringComparison.OrdinalIgnoreCase) >= 0 ||
               soundKey.IndexOf("harp", StringComparison.OrdinalIgnoreCase) >= 0 ||
               soundKey.IndexOf("sax", StringComparison.OrdinalIgnoreCase) >= 0 ||
               soundKey.IndexOf("piano", StringComparison.OrdinalIgnoreCase) >= 0 ||
               soundKey.IndexOf("rhodes", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildSimulatorCacheKey(BassNote note, SimulatorChord chord)
    {
        return $"{note.SoundKey}|tone={GetToneKey(note)}|notePitch={note.PitchOffset}|chordPitch={chord.PitchOffset}|chordType={chord.ChordType}";
    }

    private static string GetToneKey(BassNote note)
    {
        return !string.IsNullOrWhiteSpace(note.ToneKey) ? note.ToneKey : note.SoundKey;
    }

    private static SimulatorChord GetChordAt(RhythmBlock block, long measureIndex, long tick)
    {
        if (block == null || block.ChordEvents == null || block.ChordEvents.Count == 0)
            return new SimulatorChord(0, "Major");

        int beatsPerMeasure = Instance != null && Instance._stageData != null ? Instance._stageData.TimeSignatureNum : 4;
        int ticksPerBeat = Instance != null && Instance._stageData != null ? Instance._stageData.TicksPerBeat : 480;
        long noteAbsoluteTick = (measureIndex * beatsPerMeasure * ticksPerBeat) + tick;

        ChordEvent bestChord = null;
        long bestChordTick = -1;

        foreach (var chord in block.ChordEvents)
        {
            long chordAbsoluteTick = (chord.MeasureIndex * beatsPerMeasure * ticksPerBeat) + chord.Tick;
            if (chordAbsoluteTick <= noteAbsoluteTick)
            {
                if (chordAbsoluteTick > bestChordTick)
                {
                    bestChordTick = chordAbsoluteTick;
                    bestChord = chord;
                }
            }
        }

        if (bestChord == null)
            bestChord = block.ChordEvents[0];

        return new SimulatorChord(bestChord.PitchOffset, bestChord.ChordType);
    }

    private static byte[] RenderBassMelodySimulatorWav(BassNote note, SimulatorChord chord)
    {
        string toneKey = GetToneKey(note);
        float[] samples = IsJazzSimulatorSound(toneKey)
            ? RenderJazzSimulatorSound(toneKey)
            : (IsBassMelodySimulatorMelody(toneKey)
                ? RenderSimulatorMelody(note, chord)
                : RenderSimulatorBass(note, chord));

        return BuildMono16Wav(samples, SimulatorSampleRate);
    }

    private static float[] RenderJazzSimulatorSound(string toneKey)
    {
        if (JazzChordFrequencies.TryGetValue(toneKey, out float[] chordFrequencies))
            return RenderJazzChord(chordFrequencies);

        if (JazzToneFrequencies.TryGetValue(toneKey, out float frequency))
        {
            if (toneKey.StartsWith("Bass_", StringComparison.OrdinalIgnoreCase))
                return RenderJazzBass(frequency);

            return RenderJazzMelody(frequency);
        }

        return new float[Mathf.CeilToInt(0.1f * SimulatorSampleRate)];
    }

    private static float[] RenderJazzBass(float frequency)
    {
        const float durationSec = 0.4f;
        int sampleCount = Mathf.CeilToInt(durationSec * SimulatorSampleRate);
        float[] samples = new float[sampleCount];
        var filter = BiquadFilter.CreateLowPass(SimulatorSampleRate, 200.0f, 1.5f);
        double phase = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SimulatorSampleRate;
            float env = Envelope(time, 0.015f, 0.35f, 0.35f * SimulatorMasterVolume * SimulatorMasterVolume);
            float raw = Triangle(ref phase, frequency);
            samples[i] = Mathf.Clamp(filter.Process(raw * env), -1.0f, 1.0f);
        }

        return samples;
    }

    private static float[] RenderJazzMelody(float frequency)
    {
        const float durationSec = 0.6f;
        int sampleCount = Mathf.CeilToInt(durationSec * SimulatorSampleRate);
        float[] samples = new float[sampleCount];
        double phase = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SimulatorSampleRate;
            float detuneCents = Mathf.Sin(2.0f * Mathf.PI * 5.5f * time) * 4.0f;
            float detunedFrequency = frequency * Mathf.Pow(2.0f, detuneCents / 1200.0f);
            float env = Envelope(time, 0.04f, 0.5f, 0.18f * SimulatorMasterVolume * SimulatorMasterVolume);
            float raw = Sine(ref phase, detunedFrequency);
            samples[i] = Mathf.Clamp(raw * env, -1.0f, 1.0f);
        }

        return samples;
    }

    private static float[] RenderJazzChord(float[] frequencies)
    {
        const float durationSec = 0.6f;
        int sampleCount = Mathf.CeilToInt(durationSec * SimulatorSampleRate);
        float[] samples = new float[sampleCount];
        double[] phases = new double[frequencies.Length];
        float voicePeak = (0.18f * SimulatorMasterVolume * SimulatorMasterVolume) / Mathf.Max(1, frequencies.Length);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SimulatorSampleRate;
            float env = Envelope(time, 0.04f, 0.5f, voicePeak);
            float mixed = 0.0f;

            for (int voice = 0; voice < frequencies.Length; voice++)
            {
                mixed += Sine(ref phases[voice], frequencies[voice]) * env;
            }

            samples[i] = Mathf.Clamp(mixed, -1.0f, 1.0f);
        }

        return samples;
    }

    private static float[] RenderSimulatorBass(BassNote note, SimulatorChord chord)
    {
        const float durationSec = 0.4f;
        int sampleCount = Mathf.CeilToInt(durationSec * SimulatorSampleRate);
        float[] samples = new float[sampleCount];
        string noteKey = GetToneKey(note);

        int degree = 1;
        int octaveOffset = -2;

        if (noteKey.IndexOf("Third", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            degree = 3;
        }
        else if (noteKey.IndexOf("Fifth", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            degree = 5;
        }
        else if (noteKey.IndexOf("Oct", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 noteKey.IndexOf("Pop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 noteKey.IndexOf("Slide", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            octaveOffset = -1;
        }

        float frequency = GetFrequencyForChordTone(chord, degree, octaveOffset);
        var filter = BiquadFilter.CreateLowPass(SimulatorSampleRate, GetSimulatorBassFilterFrequency(note), 1.5f);
        SimulatorWaveform waveform = GetSimulatorBassWaveform(note);
        double phase = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SimulatorSampleRate;
            float env = Envelope(time, 0.015f, 0.35f, 0.35f * SimulatorMasterVolume * SimulatorMasterVolume);
            float raw = Oscillate(waveform, ref phase, frequency);
            samples[i] = Mathf.Clamp(filter.Process(raw * env), -1.0f, 1.0f);
        }

        return samples;
    }

    private static float[] RenderSimulatorMelody(BassNote note, SimulatorChord chord)
    {
        const float durationSec = 0.6f;
        int sampleCount = Mathf.CeilToInt(durationSec * SimulatorSampleRate);
        float[] samples = new float[sampleCount];

        float baseFrequency = ResolveSimulatorMelodyFrequency(note, chord);
        SimulatorWaveform waveform = GetSimulatorMelodyWaveform(note);
        GetSimulatorVibrato(note, out float vibratoSpeed, out float vibratoDepth);
        double phase = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SimulatorSampleRate;
            float detuneCents = Mathf.Sin(2.0f * Mathf.PI * vibratoSpeed * time) * vibratoDepth;
            float frequency = baseFrequency * Mathf.Pow(2.0f, detuneCents / 1200.0f);
            float env = Envelope(time, 0.04f, 0.5f, 0.18f * SimulatorMasterVolume * SimulatorMasterVolume);
            float raw = Oscillate(waveform, ref phase, frequency);
            samples[i] = Mathf.Clamp(raw * env, -1.0f, 1.0f);
        }

        return samples;
    }

    private static float ResolveSimulatorMelodyFrequency(BassNote note, SimulatorChord chord)
    {
        if (TryParseFixedNote(GetToneKey(note), out string noteName, out int octave, out int semitoneOffset))
        {
            int midi = 60 + semitoneOffset + ((octave - 4) * 12);
            int noteOffset = semitoneOffset + ((octave - 4) * 12);
            int relativeSemitone = PositiveModulo(noteOffset - chord.PitchOffset, 12);
            string chordType = chord.ChordType.ToLowerInvariant();

            if (IsMinorLike(chordType) && relativeSemitone == 4)
                midi -= 1;
            else if ((chordType.Contains("major") || chordType.Contains("maj")) && relativeSemitone == 3)
                midi += 1;

            return MidiToFrequency(midi);
        }

        return MidiToFrequency(60 + note.PitchOffset);
    }

    private static bool TryParseFixedNote(string soundKey, out string noteName, out int octave, out int semitoneOffset)
    {
        noteName = string.Empty;
        octave = 4;
        semitoneOffset = 0;

        string[] parts = soundKey.Split('_');
        if (parts.Length <= 1)
            return false;

        string token = parts[1];
        if (string.IsNullOrEmpty(token))
            return false;

        int index = 0;
        while (index < token.Length && (char.IsLetter(token[index]) || token[index] == '#'))
        {
            noteName += token[index];
            index++;
        }

        if (string.IsNullOrEmpty(noteName))
            return false;

        if (index < token.Length && int.TryParse(token.Substring(index), out int parsedOctave))
            octave = parsedOctave;

        return TryGetNoteSemitone(noteName, out semitoneOffset);
    }

    private static bool TryGetNoteSemitone(string noteName, out int semitone)
    {
        switch (noteName)
        {
            case "C": semitone = 0; return true;
            case "C#": semitone = 1; return true;
            case "Db": semitone = 1; return true;
            case "D": semitone = 2; return true;
            case "D#": semitone = 3; return true;
            case "Eb": semitone = 3; return true;
            case "E": semitone = 4; return true;
            case "F": semitone = 5; return true;
            case "F#": semitone = 6; return true;
            case "Gb": semitone = 6; return true;
            case "G": semitone = 7; return true;
            case "G#": semitone = 8; return true;
            case "Ab": semitone = 8; return true;
            case "A": semitone = 9; return true;
            case "A#": semitone = 10; return true;
            case "Bb": semitone = 10; return true;
            case "B": semitone = 11; return true;
            default: semitone = 0; return false;
        }
    }

    private static SimulatorWaveform GetSimulatorBassWaveform(BassNote note)
    {
        string toneKey = GetToneKey(note);
        if (toneKey.StartsWith("SubBass_", StringComparison.OrdinalIgnoreCase) ||
            toneKey.StartsWith("OrchBass_", StringComparison.OrdinalIgnoreCase) ||
            toneKey.StartsWith("Timpani_", StringComparison.OrdinalIgnoreCase))
        {
            return SimulatorWaveform.Triangle;
        }

        return SimulatorWaveform.Saw;
    }

    private static float GetSimulatorBassFilterFrequency(BassNote note)
    {
        string toneKey = GetToneKey(note);
        if (toneKey.StartsWith("SynthBass_", StringComparison.OrdinalIgnoreCase))
            return 280.0f;
        if (toneKey.StartsWith("SubBass_", StringComparison.OrdinalIgnoreCase))
            return 150.0f;
        if (toneKey.StartsWith("OrchBass_", StringComparison.OrdinalIgnoreCase) ||
            toneKey.StartsWith("Timpani_", StringComparison.OrdinalIgnoreCase))
            return 180.0f;

        return 450.0f;
    }

    private static SimulatorWaveform GetSimulatorMelodyWaveform(BassNote note)
    {
        string toneKey = GetToneKey(note);
        if (toneKey.StartsWith("SynthLead_", StringComparison.OrdinalIgnoreCase))
            return SimulatorWaveform.Square;
        if (toneKey.StartsWith("LofiFlute_", StringComparison.OrdinalIgnoreCase) ||
            toneKey.StartsWith("Rhodes_", StringComparison.OrdinalIgnoreCase))
            return SimulatorWaveform.Sine;
        if (toneKey.StartsWith("Violin_", StringComparison.OrdinalIgnoreCase) ||
            toneKey.StartsWith("Harp_", StringComparison.OrdinalIgnoreCase))
            return SimulatorWaveform.Saw;

        return SimulatorWaveform.Triangle;
    }

    private static void GetSimulatorVibrato(BassNote note, out float speed, out float depth)
    {
        string toneKey = GetToneKey(note);
        if (toneKey.StartsWith("LofiFlute_", StringComparison.OrdinalIgnoreCase) ||
            toneKey.StartsWith("Rhodes_", StringComparison.OrdinalIgnoreCase))
        {
            speed = 4.2f;
            depth = 6.0f;
            return;
        }

        if (toneKey.StartsWith("Violin_", StringComparison.OrdinalIgnoreCase) ||
            toneKey.StartsWith("Harp_", StringComparison.OrdinalIgnoreCase))
        {
            speed = 6.2f;
            depth = 3.0f;
            return;
        }

        speed = 5.5f;
        depth = 4.0f;
    }

    private static float GetFrequencyForChordTone(SimulatorChord chord, int degree, int octaveOffset)
    {
        int degreeOffset = 0;
        string chordType = chord.ChordType.ToLowerInvariant();

        if (degree == 3)
            degreeOffset = IsMinorLike(chordType) ? 3 : 4;
        else if (degree == 5)
            degreeOffset = chordType.Contains("dim") || chordType.Contains("halfdiminished") ? 6 : chordType.Contains("aug") ? 8 : 7;
        else if (degree == 7)
            degreeOffset = chordType.Contains("maj7") || chordType.Contains("major7") ? 11 : chordType.Contains("dim7") ? 9 : 10;
        else if (degree == 9)
            degreeOffset = 14;
        else if (degree == 11)
            degreeOffset = 17;

        int midi = 60 + chord.PitchOffset + degreeOffset + (octaveOffset * 12);
        return MidiToFrequency(midi);
    }

    private static bool IsMinorLike(string chordType)
    {
        return chordType.Contains("minor") ||
               chordType.Contains("min") ||
               chordType.Contains("dim") ||
               chordType.Contains("halfdiminished");
    }

    private static float MidiToFrequency(int midi)
    {
        return 440.0f * Mathf.Pow(2.0f, (midi - 69) / 12.0f);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }

    private static float Envelope(float time, float attackEndSec, float releaseEndSec, float peak)
    {
        if (time <= 0.0f)
            return 0.001f;

        if (time < attackEndSec)
            return Mathf.Lerp(0.001f, peak, time / attackEndSec);

        if (time < releaseEndSec)
        {
            float t = (time - attackEndSec) / (releaseEndSec - attackEndSec);
            return 0.001f * Mathf.Pow(peak / 0.001f, 1.0f - t);
        }

        return 0.001f;
    }

    private static float Saw(ref double phase, float frequency)
    {
        phase += frequency / SimulatorSampleRate;
        phase -= Math.Floor(phase);
        return (float)((2.0 * phase) - 1.0);
    }

    private static float Triangle(ref double phase, float frequency)
    {
        phase += frequency / SimulatorSampleRate;
        phase -= Math.Floor(phase);
        return (float)((2.0 * Math.Abs((2.0 * phase) - 1.0)) - 1.0);
    }

    private static float Sine(ref double phase, float frequency)
    {
        phase += frequency / SimulatorSampleRate;
        phase -= Math.Floor(phase);
        return Mathf.Sin((float)(2.0 * Math.PI * phase));
    }

    private static float Square(ref double phase, float frequency)
    {
        phase += frequency / SimulatorSampleRate;
        phase -= Math.Floor(phase);
        return phase < 0.5 ? 1.0f : -1.0f;
    }

    private static float Oscillate(SimulatorWaveform waveform, ref double phase, float frequency)
    {
        switch (waveform)
        {
            case SimulatorWaveform.Sine:
                return Sine(ref phase, frequency);
            case SimulatorWaveform.Square:
                return Square(ref phase, frequency);
            case SimulatorWaveform.Saw:
                return Saw(ref phase, frequency);
            case SimulatorWaveform.Triangle:
            default:
                return Triangle(ref phase, frequency);
        }
    }

    private static byte[] BuildMono16Wav(float[] samples, int sampleRate)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            int dataLength = samples.Length * sizeof(short);

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataLength);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataLength);

            foreach (float sample in samples)
            {
                short value = (short)Mathf.RoundToInt(Mathf.Clamp(sample, -1.0f, 1.0f) * short.MaxValue);
                writer.Write(value);
            }

            return stream.ToArray();
        }
    }

    private readonly struct SimulatorChord
    {
        public readonly int PitchOffset;
        public readonly string ChordType;

        public SimulatorChord(int pitchOffset, string chordType)
        {
            PitchOffset = pitchOffset;
            ChordType = string.IsNullOrWhiteSpace(chordType) ? "Major" : chordType;
        }
    }

    private enum SimulatorWaveform
    {
        Sine,
        Square,
        Saw,
        Triangle
    }

    private struct BiquadFilter
    {
        private readonly float _b0;
        private readonly float _b1;
        private readonly float _b2;
        private readonly float _a1;
        private readonly float _a2;
        private float _x1;
        private float _x2;
        private float _y1;
        private float _y2;

        private BiquadFilter(float b0, float b1, float b2, float a1, float a2)
        {
            _b0 = b0;
            _b1 = b1;
            _b2 = b2;
            _a1 = a1;
            _a2 = a2;
            _x1 = 0.0f;
            _x2 = 0.0f;
            _y1 = 0.0f;
            _y2 = 0.0f;
        }

        public static BiquadFilter CreateLowPass(float sampleRate, float frequency, float q)
        {
            float omega = 2.0f * Mathf.PI * frequency / sampleRate;
            float sin = Mathf.Sin(omega);
            float cos = Mathf.Cos(omega);
            float alpha = sin / (2.0f * q);

            float b0 = (1.0f - cos) * 0.5f;
            float b1 = 1.0f - cos;
            float b2 = (1.0f - cos) * 0.5f;
            float a0 = 1.0f + alpha;
            float a1 = -2.0f * cos;
            float a2 = 1.0f - alpha;

            return new BiquadFilter(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public float Process(float input)
        {
            float output = (_b0 * input) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);
            _x2 = _x1;
            _x1 = input;
            _y2 = _y1;
            _y1 = output;
            return output;
        }
    }

    /// <summary>
    /// 현재 클라이언트의 Playhead 위치를 기준으로 가장 최근에 발동된 ChordEvent 객체를 반환합니다.
    /// </summary>
    public ChordEvent GetCurrentChord()
    {
        if (_stageData == null || _currentBlock == null) return null;
        if (RhythmClient.Instance == null || RhythmClient.Instance.ServerSongStartMs == 0) return null;

        long currentBeatIndex = RhythmClient.Instance.GetCurrentBeatIndex();
        if (currentBeatIndex < 0) return null;

        int beatsPerMeasure = _stageData.TimeSignatureNum;
        int totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;

        long beatInBlock = currentBeatIndex - _blockStartBeatIndex;
        if (beatInBlock < 0 || beatInBlock >= totalBeatsInBlock)
            return null;

        long measureIndex = beatInBlock / beatsPerMeasure;
        long relativeBeatInMeasure = beatInBlock % beatsPerMeasure;
        long currentTick = relativeBeatInMeasure * _stageData.TicksPerBeat;

        if (_currentBlock.ChordEvents == null || _currentBlock.ChordEvents.Count == 0)
            return null;

        long noteAbsoluteTick = (measureIndex * beatsPerMeasure * _stageData.TicksPerBeat) + currentTick;
        ChordEvent bestChord = null;
        long bestChordTick = -1;

        foreach (var chord in _currentBlock.ChordEvents)
        {
            long chordAbsoluteTick = (chord.MeasureIndex * beatsPerMeasure * _stageData.TicksPerBeat) + chord.Tick;
            if (chordAbsoluteTick <= noteAbsoluteTick)
            {
                if (chordAbsoluteTick > bestChordTick)
                {
                    bestChordTick = chordAbsoluteTick;
                    bestChord = chord;
                }
            }
        }
        return bestChord ?? _currentBlock.ChordEvents[0];
    }

    public string GetDynamicFmodEventPath(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey)) return null;

        // 1:1 이름 매칭 검증 (FMOD 이벤트 명과 완전히 동일한 경우 바로 리다이렉트 재생)
        if (ValidFmodEventNames.TryGetValue(soundKey, out string exactName))
        {
            if (exactName.EndsWith("_Dagger", StringComparison.OrdinalIgnoreCase) ||
                exactName.EndsWith("_Greatsword", StringComparison.OrdinalIgnoreCase) ||
                exactName.EndsWith("_Bow", StringComparison.OrdinalIgnoreCase) ||
                exactName.EndsWith("_Parry", StringComparison.OrdinalIgnoreCase) ||
                exactName.EndsWith("_Staff", StringComparison.OrdinalIgnoreCase))
            {
                return $"event:/SFX/{exactName}";
            }
            if (exactName.StartsWith("Bass_", StringComparison.OrdinalIgnoreCase))
            {
                return $"event:/Bass/{exactName}";
            }
            if (exactName.StartsWith("Melody_", StringComparison.OrdinalIgnoreCase))
            {
                return $"event:/Melody/{exactName}";
            }
            if (exactName.Equals("Kick", StringComparison.OrdinalIgnoreCase) ||
                exactName.Equals("HiHat", StringComparison.OrdinalIgnoreCase) ||
                exactName.Equals("TestSmith", StringComparison.OrdinalIgnoreCase))
            {
                return $"event:/Drums/{exactName}";
            }
            return $"event:/{exactName}";
        }

        string genre = GetCurrentGenre();

        // 1. 베이스 사운드 매핑 (장르 명시형)
        if (soundKey.StartsWith("SynthBass_", StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Synthwave";
        if (soundKey.StartsWith("LofiBass_", StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Lofi";
        if (soundKey.StartsWith("FunkBass_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("SlapBass_", StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Funk";
        if (soundKey.StartsWith("OrchBass_", StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Orchestral";
        if (soundKey.StartsWith("JazzBass_", StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Jazz";

        // 1-1. 베이스 사운드 매핑 (범용 접두어) -> 현재 스테이지 장르 기준
        if (soundKey.StartsWith("Bass_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("BassKick", StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/Bass/Bass_{genre}";
        }

        // 2. 멜로디 사운드 매핑 (장르 명시형/악기명)
        if (soundKey.StartsWith("SynthLead_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("SynthPluck_", StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Synthwave";

        if (soundKey.StartsWith("LofiFlute_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Rhodes_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("LofiPiano_", StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Lofi";

        if (soundKey.StartsWith("Clav_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Clavinet_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("FunkGuitar_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("FunkMute_", StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Funk";

        if (soundKey.StartsWith("OrchFlute_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Violin_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Harp_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Cello_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Flute_", StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Orchestral";

        if (soundKey.StartsWith("JazzSax_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Vibraphone_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("JazzGuitar_", StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Jazz";

        // 2-1. 멜로디 사운드 매핑 (범용 접두어) -> 현재 스테이지 장르 기준
        if (soundKey.StartsWith("Melody_", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Lead_", StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/Melody/Melody_{genre}";
        }

        // 3. Kick / HiHat 기본 드럼 및 Timpani (팀파니는 킥에셋 피치 튜닝 매핑)
        if (soundKey.Equals("Kick", StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Timpani_", StringComparison.OrdinalIgnoreCase))
            return "event:/Drums/Kick";
        if (soundKey.Equals("HiHat", StringComparison.OrdinalIgnoreCase))
            return "event:/Drums/HiHat";
        if (soundKey.Equals("Smith", StringComparison.OrdinalIgnoreCase) ||
            soundKey.Equals("TestSmith", StringComparison.OrdinalIgnoreCase))
            return "event:/Drums/TestSmith";

        return null;
    }

    public string GetCurrentGenre()
    {
        if (_stageData == null) return "Synthwave";
        string id = _stageData.StageId.ToLowerInvariant();
        if (id.Contains("synth")) return "Synthwave";
        if (id.Contains("lofi")) return "Lofi";
        if (id.Contains("funk")) return "Funk";
        if (id.Contains("ethereal") || id.Contains("orch")) return "Orchestral";
        if (id.Contains("jazz")) return "Jazz";
        return "Synthwave";
    }

    /// <summary>
    /// 현재 클라이언트의 Playhead 위치를 기준으로 가장 최근에 발동된 ChordEvent의 PitchOffset 값을 반환합니다.
    /// 타격음 등 다른 사운드의 화성을 동기화할 때 사용됩니다.
    /// </summary>
    public int GetCurrentPitchOffset()
    {
        if (_stageData == null || _currentBlock == null) return 0;
        if (RhythmClient.Instance == null || RhythmClient.Instance.ServerSongStartMs == 0) return 0;

        long currentBeatIndex = RhythmClient.Instance.GetCurrentBeatIndex();
        if (currentBeatIndex < 0) return 0;

        int beatsPerMeasure = _stageData.TimeSignatureNum;
        int totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;

        long beatInBlock = currentBeatIndex - _blockStartBeatIndex;
        // 블록을 넘어서거나 음수면 예외처리로 오프셋 0 반환
        if (beatInBlock < 0 || beatInBlock >= totalBeatsInBlock)
            return 0;

        long measureIndex = beatInBlock / beatsPerMeasure;
        long relativeBeatInMeasure = beatInBlock % beatsPerMeasure;
        long currentTick = relativeBeatInMeasure * _stageData.TicksPerBeat;

        return GetPitchOffsetAt(_currentBlock, measureIndex, currentTick);
    }
}

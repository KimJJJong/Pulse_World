using UnityEngine;
using System.Collections.Generic;
using Shared.Data;
using Newtonsoft.Json;
using FMODUnity;

public class FMODDrumSequencer : MonoBehaviour
{
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
    private HashSet<string> _playedNotes = new HashSet<string>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (rhythmJsonAsset != null)
        {
            try
            {
                _stageData = JsonConvert.DeserializeObject<RhythmStageData>(rhythmJsonAsset.text);
                if (_stageData != null && _stageData.Blocks.Count > 0)
                {
                    _currentBlock = _stageData.Blocks[0];
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
        if (!enableSequencer || _stageData == null || _currentBlock == null) return;
        if (RhythmClient.Instance == null || RhythmClient.Instance.ServerSongStartMs == 0) return;

        double currentServerTimeSec = RhythmClient.Instance.GetCurrentServerTimeMs() / 1000.0;
        double lookAheadTimeSec = currentServerTimeSec + lookAheadSec;

        int beatsPerMeasure = _stageData.TimeSignatureNum;
        int totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;
        
        long currentBeatIndex = RhythmClient.Instance.GetCurrentBeatIndex();
        if (currentBeatIndex < 0) return; 

        // 1. 현재 블록의 끝나는 시간 판별
        double blockDurationSec = (totalBeatsInBlock * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0;
        double blockEndSec = (RhythmClient.Instance.ServerSongStartMs + _blockStartBeatIndex * RhythmClient.Instance.GetBeatDurationMs()) / 1000.0 + blockDurationSec;

        // 2. 블록 전환 (현재 재생 시간이 블록 끝을 실제로 지났을 때)
        if (currentServerTimeSec >= blockEndSec)
        {
            string nextId = _currentBlock.DefaultNextBlock;
            var nextBlock = _stageData.Blocks.Find(b => b.BlockId == nextId) ?? _currentBlock;
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
            string nextId = _currentBlock.DefaultNextBlock;
            var nextBlock = _stageData.Blocks.Find(b => b.BlockId == nextId) ?? _currentBlock;
            ScheduleNotesForBlock(nextBlock, _blockStartBeatIndex + totalBeatsInBlock, currentServerTimeSec, lookAheadTimeSec);
        }
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
            
            // 고유 식별자에 blockStartAbsoluteBeat를 포함시켜 루프를 100번 돌아도 겹치지 않게 함
            string noteId = $"{blockStartAbsoluteBeat}_{note.MeasureIndex}_{note.Tick}_{note.SoundKey}";
            
            // 아직 재생하지 않았고, 현재 시간 ~ LookAhead 시간 사이에 도달하는 노트인가?
            if (!_playedNotes.Contains(noteId) && noteTimeSec <= lookAheadTimeSec)
            {
                _playedNotes.Add(noteId);
                ScheduleFMODEvent(note, noteTimeSec, currentTimeSec);
            }
        }
    }

    private void ScheduleFMODEvent(BassNote note, double targetTimeSec, double currentTimeSec)
    {
        double delaySec = targetTimeSec - currentTimeSec;
        if (delaySec < 0) delaySec = 0; // 이미 늦었으면 즉시 재생

        // JSON 문자열(SoundKey)과 맵핑된 FMOD EventReference 찾기
        var mapping = drumMappings.Find(m => m.soundKey == note.SoundKey);
        if (mapping.fmodEvent.IsNull)
        {
            Debug.LogError($"[FMODDrum] Mapping for '{note.SoundKey}' is missing or EventReference is empty. Please set it in the Inspector.");
            return;
        }

        // FMOD Studio 이벤트를 생성
        FMOD.Studio.EventInstance instance;
        try 
        {
            instance = RuntimeManager.CreateInstance(mapping.fmodEvent);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODDrum] Event Create Failed for Key {note.SoundKey}: {e.Message}");
            return;
        }
        
        // 딜레이를 주려면 FMOD Core의 시간 동기화(DSP Clock)를 사용해야 합니다.
        RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup masterCG);
        masterCG.getDSPClock(out ulong dspClock, out ulong parentClock);
        
        RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out FMOD.SPEAKERMODE speakerMode, out int numRawSpeakers);

        ulong delaySamples = (ulong)(delaySec * sampleRate);
        ulong targetDSPClock = dspClock + delaySamples;

        // FMOD Studio API를 사용해 볼륨 파라미터 적용 (옵션)
        instance.setVolume(note.VolumeMultiplier > 0 ? note.VolumeMultiplier : 1.0f);

        if (delaySec > 0)
        {
            instance.setPaused(true);
            instance.start();

            // FMOD Studio는 기본적으로 비동기 처리되므로 즉시 ChannelGroup을 얻으려면 Flush가 필요합니다.
            RuntimeManager.StudioSystem.flushCommands(); 

            instance.getChannelGroup(out FMOD.ChannelGroup channelGroup);
            if (channelGroup.hasHandle())
            {
                channelGroup.setDelay(targetDSPClock, 0, false);
            }
            else
            {
                Debug.LogWarning($"[FMODDrum] Failed to get ChannelGroup! Fallback to instant play. (Note: {note.SoundKey})");
            }

            instance.setPaused(false);
        }
        else
        {
            // 지연이 없거나 이미 늦었으면 즉시 재생
            instance.start();
        }

        instance.release(); // 연주 종료 시 메모리 자동 반환
        
        // Debug.Log($"[FMODDrum] Scheduled {note.SoundKey} in {delaySec:F3}s");
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

        int currentOffset = 0;
        
        // 현재 위치를 넘지 않는 코드 중 가장 최신의 것을 찾음
        foreach (var chord in _currentBlock.ChordEvents)
        {
            if (chord.MeasureIndex < measureIndex || 
               (chord.MeasureIndex == measureIndex && chord.Tick <= currentTick))
            {
                currentOffset = chord.PitchOffset;
            }
            else
            {
                // 시간 순 정렬되어 있다고 가정하고 Break
                break;
            }
        }

        return currentOffset;
    }
}

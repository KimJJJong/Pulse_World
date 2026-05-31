using UnityEngine;
using Shared.Data;
using Newtonsoft.Json;

/// <summary>
/// 진행률 확인을 위한 클라이언트 전용 시각 디버깅 툴 (IMGUI 기반)
/// 빈 게임오브젝트 등에 부착하고 해당 스테이지의 에디터 뽑은 Json 에셋을 연결해서 사용합니다.
/// </summary>
public class RhythmVisualDebugger : MonoBehaviour
{
    [Header("Target Stage Data (JSON)")]
    public TextAsset rhythmJsonAsset;

    private RhythmStageData _stageData;
    
    // 로컬 추적용 상태
    private RhythmBlock _currentBlock;
    private long _blockStartBeatIndex = 0;

    private void Start()
    {
        if (rhythmJsonAsset != null)
        {
            ParseRhythmData(rhythmJsonAsset.text);
        }
    }

    private void ParseRhythmData(string json)
    {
        try
        {
            _stageData = JsonConvert.DeserializeObject<RhythmStageData>(json);
            if (_stageData != null && _stageData.Blocks.Count > 0)
            {
                _currentBlock = _stageData.Blocks[0];
                _blockStartBeatIndex = 0;
                Debug.Log($"[RhythmVisualDebugger] Loaded Stage: {_stageData.StageId} | Initial Block: {_currentBlock.BlockId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RhythmVisualDebugger] Failed to parse JSON: {e.Message}");
        }
    }

    private void OnGUI()
    {
        P2PDebugViewConfig.HandleRuntimeToggleEvent(Event.current);
        if (!P2PDebugViewConfig.ShowNetworkSyncOverlay)
            return;

        // 1. 기초 환경 검사
        if (RhythmClient.Instance == null || RhythmClient.Instance.ServerSongStartMs == 0)
        {
            DrawWarning("RhythmClient is not synced yet (Waiting for SC_BeatSync).");
            return;
        }

        if (_stageData == null || _currentBlock == null)
        {
            DrawWarning("No RhythmStageData loaded. Please assign rhythmJsonAsset.");
            return;
        }

        // 2. 현재 상태 계산 (Client Rhythm 시스템 추적 기준)
        long currentBeatIndex = RhythmClient.Instance.GetCurrentBeatIndex();
        
        // 서스펜디드 상태(음악 시작 전 대기중 등)
        if (currentBeatIndex < 0)
        {
            DrawWarning($"Song starts in... {Mathf.Abs((float)RhythmClient.Instance.GetBeatTimeMs(0) - RhythmClient.Instance.GetCurrentServerTimeMs()) / 1000f:F1}s");
            return;
        }

        // 3. 매니저 업데이트 (서버 로직 간이 시뮬레이션)
        long beatInBlock = currentBeatIndex - _blockStartBeatIndex;
        if (beatInBlock < 0) return;

        int beatsPerMeasure = _stageData.TimeSignatureNum;
        int totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;

        // 루프 처리
        if (beatInBlock >= totalBeatsInBlock)
        {
            // 다음 블록 예약 기능은 클라 디버거에서는 생략하고 무한 루프 우선 처리
            string nextId = _currentBlock.DefaultNextBlock;
            var nextBlock = _stageData.Blocks.Find(b => b.BlockId == nextId);
            if (nextBlock != null) _currentBlock = nextBlock;

            _blockStartBeatIndex = currentBeatIndex - (beatInBlock % totalBeatsInBlock);
            beatInBlock = currentBeatIndex - _blockStartBeatIndex;
            totalBeatsInBlock = _currentBlock.LengthMeasures * beatsPerMeasure;
        }

        // 상대적인 비트 진행률 (모듈러)
        beatInBlock = beatInBlock % totalBeatsInBlock;

        long measureIndex = (beatInBlock / beatsPerMeasure) + 1;
        long beatInMeasure = (beatInBlock % beatsPerMeasure) + 1;
        long targetTick = (beatInMeasure - 1) * _stageData.TicksPerBeat;

        // 4. 화음(Chord) 피치 오프셋 계산
        int currentPitchOffset = 0;
        var activeChord = _currentBlock.ChordEvents
            .FindLast(c => c.MeasureIndex < (measureIndex - 1) || (c.MeasureIndex == (measureIndex - 1) && c.Tick <= targetTick));

        if (activeChord != null)
        {
            currentPitchOffset = activeChord.PitchOffset;
        }
        else if (_currentBlock.ChordEvents.Count > 0)
        {
            currentPitchOffset = _currentBlock.ChordEvents[_currentBlock.ChordEvents.Count - 1].PitchOffset;
        }

        // 5. GUI 렌더링
        DrawOverlay(currentBeatIndex, measureIndex, beatInMeasure, _currentBlock.BlockId, currentPitchOffset);
    }

    private void DrawOverlay(long currentBeatIndex, long measureIndex, long beatInMeasure, string blockId, int pitchOffset)
    {
        // 배경 박스 스타일
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 18;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.normal.textColor = Color.white;

        // 내부 텍스트 스타일
        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 24;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.normal.textColor = Color.cyan;

        // GUILayout은 Layout/Repaint 이벤트가 어긋날 때 예외가 나서 고정 Rect GUI로만 그린다.
        float width = Mathf.Min(450f, Mathf.Max(260f, Screen.width - 20f));
        float height = 200f;
        float startX = Mathf.Max(10f, (Screen.width - width) / 2f);
        float startY = 20f; // 상단으로 고정

        Rect boxRect = new Rect(startX, startY, width, height);
        GUI.Box(boxRect, "Rhythm Debugger", boxStyle);

        float contentX = startX + 16f;
        float contentY = startY + 34f;
        float contentWidth = width - 32f;
        const float rowHeight = 30f;
        const float rowGap = 8f;

        GUI.Label(new Rect(contentX, contentY, contentWidth, rowHeight), $"BPM : {RhythmClient.Instance.Bpm:F1} | Stage : {_stageData.StageId}");
        contentY += rowHeight + rowGap;

        textStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(contentX, contentY, contentWidth, rowHeight), $"Current Block : {blockId}", textStyle);
        contentY += rowHeight + rowGap;

        textStyle.normal.textColor = Color.green;
        GUI.Label(new Rect(contentX, contentY, contentWidth, rowHeight), $"Measure {measureIndex} : Beat {beatInMeasure} (Abs: {currentBeatIndex})", textStyle);
        contentY += rowHeight + rowGap;

        textStyle.normal.textColor = new Color(1f, 0.5f, 1f); // Pink
        GUI.Label(new Rect(contentX, contentY, contentWidth, rowHeight), $"Pitch Offset : {(pitchOffset >= 0 ? "+" + pitchOffset : pitchOffset.ToString())}", textStyle);
    }

    private void DrawWarning(string msg)
    {
        GUIStyle warningStyle = new GUIStyle(GUI.skin.box);
        warningStyle.fontSize = 16;
        warningStyle.normal.textColor = Color.red;
        float width = Mathf.Min(450f, Mathf.Max(260f, Screen.width - 20f));
        float height = 80f;
        float startX = Mathf.Max(10f, (Screen.width - width) / 2f);
        float startY = 20f;
        GUI.Box(new Rect(startX, startY, width, height), "Rhythm Debugger\n" + msg, warningStyle);
    }
}

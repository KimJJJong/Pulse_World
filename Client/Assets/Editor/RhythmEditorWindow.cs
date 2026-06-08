using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Shared.Data;

public class RhythmEditorWindow : EditorWindow
{
    private RhythmStageData stageData = new RhythmStageData();
    private Vector2 scrollPos;
    private FMOD.Sound _previewSimulatorSound;
    private FMOD.Channel _previewSimulatorChannel;

    // 서버 Export 경로 (프로젝트 구조 기준으로 자동 계산, 필요 시 직접 수정)
    private static string _serverExportPath = string.Empty;

    private static string ResolvedServerPath
    {
        get
        {
            if (!string.IsNullOrEmpty(_serverExportPath)) return _serverExportPath;
            // Client/Assets 기준 상대 경로로 서버 Sound Json 폴더 탐색
            var assetsPath = Application.dataPath; // .../Client/Assets
            var serverPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(assetsPath, "..", "..", "Server", "GameServer", "Content", "01.Game", "Sound", "Json"));
            return serverPath;
        }
    }

    private readonly string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private readonly string[] chordTypes = { "Major", "Minor", "Diminished", "Augmented" };
    private readonly string[] chordShortcuts = { "M", "m", "dim", "aug" };

    [MenuItem("RhythmRPG/Editors/Audio/Rhythm Audio Data Editor")]
    public static void ShowWindow()
    {
        GetWindow<RhythmEditorWindow>("RhythmRPG Audio Editor").minSize = new Vector2(550, 650);
    }

    private void OnGUI()
    {
        GUILayout.Label("🎵 RhythmRPG 다이내믹 오디오 에디터", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.Space();

        GUILayout.BeginVertical("box");
        GUILayout.Label("1. 스테이지 음악 기본 설정 (Global Tempo \u0026 Time Signature)", EditorStyles.boldLabel);

        // 💡 음악 기초 지식 가이드 안내 (유저 편의성 증대)
        EditorGUILayout.HelpBox(
            "💡 [음악 기초 가이드]\n" +
            "• 박자(Time Signature): 곡의 리듬 뼈대입니다. 분자(Numerator)는 1마디에 박자가 몇 번 반복되는가를 나타내고, 분모(Denominator)는 그 기준 음표입니다. (현대 액션/대중가요 90%는 4/4 박자를 씁니다.)\n" +
            "• Ticks Per Beat (분해능): 1박자를 컴퓨터가 얼마의 해상도로 잘게 쪼갤 것인가를 결정합니다. MIDI 표준인 '480' 틱을 쓰면 16분 음표나 재즈풍의 셋잇단음표 등 모든 엇박의 위치를 수학적 오차 없이 완벽히 지정할 수 있습니다.",
            MessageType.Info);

        EditorGUILayout.Space();

        stageData.StageId = EditorGUILayout.TextField("스테이지 ID", stageData.StageId);
        stageData.Bpm = EditorGUILayout.IntField("기본 BPM (템포)", stageData.Bpm);
        EditorGUILayout.BeginHorizontal();
        stageData.TimeSignatureNum = EditorGUILayout.IntField("박자 (분자)", stageData.TimeSignatureNum);
        GUILayout.Label("/", GUILayout.Width(10));
        stageData.TimeSignatureDenom = EditorGUILayout.IntField("박자 (분모)", stageData.TimeSignatureDenom);
        EditorGUILayout.EndHorizontal();
        stageData.TicksPerBeat = EditorGUILayout.IntField("Ticks Per Beat (분해능)", stageData.TicksPerBeat);
        GUILayout.EndVertical();

        EditorGUILayout.Space();
        GUILayout.Label("2. 리듬 블록(패턴) 시퀀스", EditorStyles.boldLabel);

        int blockToRemove = -1;
        for (int i = 0; i < stageData.Blocks.Count; i++)
        {
            if (DrawBlockUI(stageData.Blocks[i], i))
            {
                blockToRemove = i;
            }
        }

        if (blockToRemove != -1)
        {
            stageData.Blocks.RemoveAt(blockToRemove);
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("새 리듬 블록 추가 (Add Block)", GUILayout.Height(30)))
        {
            stageData.Blocks.Add(new RhythmBlock { BlockId = "Phase_" + (stageData.Blocks.Count + 1), DefaultNextBlock = "Phase_" + (stageData.Blocks.Count + 1), LengthMeasures = 4 });
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("JSON 불러오기 (Import)", GUILayout.Height(35)))
        {
            ImportFromJson();
        }
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("⬆ 서버 폴더로 Export!", GUILayout.Height(35)))
        {
            ExportToServer();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        // 서버 Export 경로 표시 / 수정 UI
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("📁 서버 Export 경로:", GUILayout.Width(120));
        _serverExportPath = EditorGUILayout.TextField(_serverExportPath);
        if (GUILayout.Button("초기화", GUILayout.Width(50)))
            _serverExportPath = string.Empty;
        EditorGUILayout.EndHorizontal();
        GUILayout.Label(ResolvedServerPath, EditorStyles.miniLabel);
    }

    private void OnDestroy()
    {
        // Clean up preview audio on window close
        StopPreviewAudio();
        FMODUnity.EditorUtils.UnloadPreviewBanks();
    }

    private bool DrawBlockUI(RhythmBlock block, int index)
    {
        bool isRemoved = false;
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
        EditorGUILayout.BeginVertical("helpbox");
        GUI.backgroundColor = Color.white;

        EditorGUILayout.BeginHorizontal();
        block.BlockId = EditorGUILayout.TextField("Block ID (이름)", block.BlockId);
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("X", GUILayout.Width(25))) isRemoved = true;
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (isRemoved)
        {
            EditorGUILayout.EndVertical();
            return true;
        }

        block.LengthMeasures = EditorGUILayout.IntField("마디 길이 (Measures)", block.LengthMeasures);
        block.DefaultNextBlock = EditorGUILayout.TextField("루프 종료 시 전환될 Block ID", block.DefaultNextBlock);

        GUILayout.BeginHorizontal();
        block.OverrideBpm = EditorGUILayout.IntField("변조 타겟 BPM", block.OverrideBpm);
        GUILayout.Label("(0이면 스테이지 기본 BPM 사용)", EditorStyles.miniLabel);
        GUILayout.EndHorizontal();

        // 📊 새로 추가된 비주얼 타임라인(Timeline) 로직
        DrawTimelineVisualizer(block);

        EditorGUILayout.Space();
        GUILayout.Label("화음(Chord) 타임라인 상세 마커 설정", EditorStyles.boldLabel);

        int chordToRemove = -1;
        for (int j = 0; j < block.ChordEvents.Count; j++)
        {
            if (DrawChordEventUI(block.ChordEvents[j], j, block.LengthMeasures))
            {
                chordToRemove = j;
            }
        }
        if (chordToRemove != -1)
        {
            block.ChordEvents.RemoveAt(chordToRemove);
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("+ 마커 찍기 (Add Chord Marker)", EditorStyles.miniButton))
        {
            block.ChordEvents.Add(new ChordEvent { MeasureIndex = 0, Tick = 0, RootNote = "C", ChordType = "Major", PitchOffset = 0 });
        }

        // ──────────────────────────────────────────────────────
        // 🥁 베이스/드럼 시퀀서 (Bass & Drum Pattern)
        // ──────────────────────────────────────────────────────
        EditorGUILayout.Space();
        GUILayout.Label("🥁 베이스/드럼 패턴 시퀀서 (Bass Sequencer)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "[블록 독립성] 베이스 패턴은 이 블록에만 적용됩니다. \n" +
            "Phase1 뼔럙은 Phase1만의 Bass를, Phase2는 새로운 Bass를 독립적으로 설정하세요.\n" +
            "• SoundKey: FMOD Bank Event 이름 (예: Kick, Snare, HiHat, BassHit_Low)\n" +
            "• ToneKey: 선택 입력. 같은 FMOD 이벤트 안에서 세부 음색/악보 키를 구분할 때 사용\n" +
            "• Volume: 1.0=표준음량, 0.5=하프음 → 엕박을 띌게 만들 때 활용",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Measure", GUILayout.Width(55));
        GUILayout.Label("Beat", GUILayout.Width(120));
        GUILayout.Label("SoundKey", GUILayout.Width(100));
        GUILayout.Label("ToneKey", GUILayout.Width(115));
        GUILayout.Label("Pitch", GUILayout.Width(45));
        GUILayout.Label("Volume", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        int bassToRemove = -1;
        for (int k = 0; k < block.BassPattern.Count; k++)
        {
            if (DrawBassNoteUI(block.BassPattern[k], k, block))
                bassToRemove = k;
        }
        if (bassToRemove != -1)
        {
            block.BassPattern.RemoveAt(bassToRemove);
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("+ 베이스 노트 추가 (Add Bass Note)", EditorStyles.miniButton))
        {
            block.BassPattern.Add(new BassNote { MeasureIndex = 0, Tick = 0, SoundKey = "Kick", VolumeMultiplier = 1.0f });
        }

        // Bass 노트도 비주얼 타임라인으로 한눈에 확인
        DrawBassTimelineVisualizer(block);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        return false;
    }

    // 🥁 Bass 패턴 타임라인 렌더러
    private void DrawBassTimelineVisualizer(RhythmBlock block)
    {
        EditorGUILayout.Space();
        GUILayout.Label("📊 Bass 타임라인 플로우 뷰", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetRect(10, 45, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "", EditorStyles.helpBox);

        if (block.LengthMeasures <= 0 || stageData.TimeSignatureNum <= 0) return;

        float measureWidth = rect.width / block.LengthMeasures;

        // 마디/박자 그리드 (Chord 타임라인과 동일)
        for (int m = 0; m < block.LengthMeasures; m++)
        {
            Rect measureRect = new Rect(rect.x + (m * measureWidth), rect.y, measureWidth, rect.height);
            Color bg = (m % 2 == 0) ? new Color(0.15f, 0.1f, 0.05f, 0.2f) : new Color(0.3f, 0.2f, 0.1f, 0.2f);
            EditorGUI.DrawRect(measureRect, bg);

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(measureRect.x + 5, measureRect.y + 2, 40, 16), $"{(m + 1)}m", EditorStyles.miniBoldLabel);
            GUI.color = Color.white;

            float beatWidth = measureWidth / stageData.TimeSignatureNum;
            for (int b = 1; b < stageData.TimeSignatureNum; b++)
            {
                Rect beatLine = new Rect(measureRect.x + (b * beatWidth), rect.y, 1, rect.height);
                EditorGUI.DrawRect(beatLine, new Color(1, 1, 1, 0.15f));
            }
        }

        float totalTicks = block.LengthMeasures * stageData.TimeSignatureNum * stageData.TicksPerBeat;

        // Bass 노트 마커 (주황색 선 + SoundKey 라벨)
        foreach (var note in block.BassPattern)
        {
            float absTick = note.MeasureIndex * stageData.TimeSignatureNum * stageData.TicksPerBeat + note.Tick;
            float norm = absTick / totalTicks;
            if (norm < 0f || norm > 1f) continue;

            float markerX = rect.x + norm * rect.width;

            // 선 두께를 볼륨 배율에 비례시켜 강약을 시각화 (강세=굵음, 약박=얇음)
            float lineWidth = Mathf.Lerp(1f, 3f, note.VolumeMultiplier);
            EditorGUI.DrawRect(new Rect(markerX, rect.y, lineWidth, rect.height), new Color(1f, 0.6f, 0f));

            // SoundKey 라벨 (짧게 표시: "Kick", "HH" 등)
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 9, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 0.8f, 0.2f);
            GUI.Label(new Rect(markerX - 18, rect.y + 25, 38, 16), note.SoundKey, style);
        }
    }

    // 🌟 타임라인 그래픽 렌더러 함수
    private void DrawTimelineVisualizer(RhythmBlock block)
    {
        EditorGUILayout.Space();
        GUILayout.Label("📊 타임라인 플로우 뷰 (Timeline Visualizer)", EditorStyles.boldLabel);

        // 투명도 있는 배경 박스 확보 (너비는 최대한, 높이는 40픽셀)
        Rect rect = GUILayoutUtility.GetRect(10, 40, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "", EditorStyles.helpBox);

        if (block.LengthMeasures <= 0 || stageData.TimeSignatureNum <= 0) return;

        float measureWidth = rect.width / block.LengthMeasures;

        // 1. 마디(Measure) 단위로 배경 칠하기 및 마디 텍스트 그려주기
        for (int m = 0; m < block.LengthMeasures; m++)
        {
            Rect measureRect = new Rect(rect.x + (m * measureWidth), rect.y, measureWidth, rect.height);

            // 짝수, 홀수 마디에 따라 색상 교차 (얼룩말 패턴)
            Color measureColor = (m % 2 == 0) ? new Color(0.2f, 0.2f, 0.2f, 0.15f) : new Color(0.4f, 0.4f, 0.4f, 0.15f);
            EditorGUI.DrawRect(measureRect, measureColor);

            // "1m, 2m..." (몇 마디인지 글자 표시)
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(measureRect.x + 5, measureRect.y + 2, 40, 20), $"{(m + 1)}m", EditorStyles.miniBoldLabel);
            GUI.color = Color.white;

            // 박자(Beat) 구분선 그리기 (희미한 세로선)
            float beatWidth = measureWidth / stageData.TimeSignatureNum;
            for (int b = 1; b < stageData.TimeSignatureNum; b++)
            {
                Rect beatLine = new Rect(measureRect.x + (b * beatWidth), rect.y, 1, rect.height);
                EditorGUI.DrawRect(beatLine, new Color(1, 1, 1, 0.2f));
            }
        }

        // 2. 사용자가 찍어둔 화음(Chord) 마커 그려주기
        float totalBlockTicks = block.LengthMeasures * stageData.TimeSignatureNum * stageData.TicksPerBeat;

        for (int i = 0; i < block.ChordEvents.Count; i++)
        {
            var chord = block.ChordEvents[i];

            // 마커의 절대 Tick 위치 (해당 블록의 시작점 0을 기준으로 측정)
            float absoluteTick = (chord.MeasureIndex * stageData.TimeSignatureNum * stageData.TicksPerBeat) + chord.Tick;
            float normalizedValue = absoluteTick / totalBlockTicks;

            // 에디터 밖으로 튀어나가지 않게 안전 장치
            if (normalizedValue >= 0f && normalizedValue <= 1f)
            {
                float markerX = rect.x + (normalizedValue * rect.width);

                // 마커 수직선 (밝은 녹색)
                Rect markerLine = new Rect(markerX, rect.y, 2, rect.height);
                EditorGUI.DrawRect(markerLine, Color.green);

                // 마커 코드 이름 텍스트 (예: Cm, Gaug)
                int typeIdx = System.Array.IndexOf(chordTypes, chord.ChordType);
                string shortType = (typeIdx != -1) ? chordShortcuts[typeIdx] : "M";
                string chordName = chord.RootNote + shortType;

                GUIStyle markerStyle = new GUIStyle(EditorStyles.boldLabel) {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11
                };
                markerStyle.normal.textColor = new Color(0f, 0.4f, 1f); // 새파란 색

                Rect labelRect = new Rect(markerX - 20, rect.y + 18, 40, 20); // 선 아래쪽/중앙
                GUI.Label(labelRect, chordName, markerStyle);
            }
        }
    }

    private bool DrawBassNoteUI(BassNote note, int index, RhythmBlock block)
    {
        bool isRemoved = false;

        // 가시성을 위한 교차 배경색 적용 (주황 계열)
        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = (index % 2 == 0) ? new Color(1f, 0.95f, 0.85f) : new Color(1f, 0.9f, 0.75f);
        EditorGUILayout.BeginHorizontal("box");
        GUI.backgroundColor = defaultColor;

        // 마디 선택
        int displayMeasure = note.MeasureIndex + 1;
        displayMeasure = EditorGUILayout.IntField(displayMeasure, GUILayout.Width(50));
        note.MeasureIndex = Mathf.Clamp(displayMeasure - 1, 0, block.LengthMeasures - 1);

        // Beat 슬라이더 (16분 음표 스냅)
        float currentBeat = (float)note.Tick / stageData.TicksPerBeat + 1f;
        float newBeat = GUILayout.HorizontalSlider(currentBeat, 1f, stageData.TimeSignatureNum + 0.99f, GUILayout.Width(110));
        newBeat = Mathf.Round(newBeat * 4f) / 4f;
        note.Tick = Mathf.RoundToInt((newBeat - 1f) * stageData.TicksPerBeat);
        GUILayout.Label(newBeat.ToString("F2"), GUILayout.Width(30));

        // SoundKey (FMOD Event 이름)
        note.SoundKey = EditorGUILayout.TextField(note.SoundKey, GUILayout.Width(100));

        // ToneKey (선택적 세부 음색/악보 키)
        note.ToneKey = EditorGUILayout.TextField(note.ToneKey, GUILayout.Width(115));

        // FMOD PitchOffset 파라미터
        note.PitchOffset = EditorGUILayout.IntField(note.PitchOffset, GUILayout.Width(45));

        // 미리듣기 버튼 (▶)
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("▶", GUILayout.Width(25)))
        {
            PreviewNoteSound(block, note);
        }
        GUI.backgroundColor = Color.white;

        // Volume Multiplier (0.0 ~ 1.0 슬라이더)
        note.VolumeMultiplier = GUILayout.HorizontalSlider(note.VolumeMultiplier, 0f, 1f, GUILayout.Width(60));
        GUI.contentColor = note.VolumeMultiplier >= 0.8f ? Color.green : Color.yellow;
        GUILayout.Label(note.VolumeMultiplier.ToString("F1"), GUILayout.Width(25));
        GUI.contentColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("X", GUILayout.Width(20))) isRemoved = true;
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        return isRemoved;
    }

    private bool DrawChordEventUI(ChordEvent chord, int index, int maxMeasures)
    {
        bool isRemoved = false;

        // 가시성을 위한 교차 배경색 적용 (파랑 계열)
        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = (index % 2 == 0) ? new Color(0.9f, 0.95f, 1f) : new Color(0.8f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = defaultColor;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"마디 (1~{maxMeasures}):", GUILayout.Width(80));
        int displayMeasure = chord.MeasureIndex + 1;
        displayMeasure = EditorGUILayout.IntSlider(displayMeasure, 1, maxMeasures, GUILayout.Width(150));
        chord.MeasureIndex = displayMeasure - 1;

        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("X", GUILayout.Width(20))) isRemoved = true;
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("박자 (Beat):", GUILayout.Width(80));

        float currentBeat = (float)chord.Tick / stageData.TicksPerBeat + 1f;
        float newBeat = GUILayout.HorizontalSlider(currentBeat, 1f, stageData.TimeSignatureNum + 0.99f, GUILayout.ExpandWidth(true));
        newBeat = Mathf.Round(newBeat * 4f) / 4f;
        chord.Tick = Mathf.RoundToInt((newBeat - 1f) * stageData.TicksPerBeat);

        GUILayout.Label(newBeat.ToString("F2") + " 박", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🎵 화음 설정:", GUILayout.Width(80));

        int noteIndex = System.Array.IndexOf(notes, chord.RootNote);
        if (noteIndex == -1) noteIndex = 0;

        int newNoteIndex = EditorGUILayout.Popup(noteIndex, notes, GUILayout.Width(50));
        chord.RootNote = notes[newNoteIndex];
        chord.PitchOffset = newNoteIndex;

        int typeIndex = System.Array.IndexOf(chordTypes, chord.ChordType);
        if (typeIndex == -1) typeIndex = 0;
        chord.ChordType = chordTypes[EditorGUILayout.Popup(typeIndex, chordTypes, GUILayout.Width(80))];

        // 화성 피치 미리듣기 버튼 (▶)
        GUI.backgroundColor = new Color(0.7f, 1.0f, 0.7f);
        if (GUILayout.Button("▶", GUILayout.Width(25)))
        {
            string baseSoundKey = "Bass_Funk";
            string id = stageData.StageId.ToLowerInvariant();
            if (id.Contains("synth")) baseSoundKey = "Bass_Synthwave";
            else if (id.Contains("lofi")) baseSoundKey = "Bass_Lofi";
            else if (id.Contains("orch") || id.Contains("ethereal")) baseSoundKey = "Bass_Orchestral";
            else if (id.Contains("jazz")) baseSoundKey = "Bass_Jazz";

            PreviewSound(baseSoundKey, chord.PitchOffset, 1.0f);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        GUI.contentColor = Color.cyan;
        GUILayout.Label($"👉 결과: +{chord.PitchOffset} 반음", EditorStyles.boldLabel);
        GUI.contentColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        return isRemoved;
    }

    private void ExportToServer()
    {
        if (string.IsNullOrWhiteSpace(stageData.StageId))
        {
            EditorUtility.DisplayDialog("Export 실패", "스테이지 ID를 먼저 입력하세요.", "확인");
            return;
        }

        string dir = ResolvedServerPath;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, stageData.StageId + "_Rhythm.json");
        string json = JsonUtility.ToJson(stageData, true);
        File.WriteAllText(path, json);

        string clientMirrorPath = Path.Combine(Application.dataPath, stageData.StageId + ".json");
        File.WriteAllText(clientMirrorPath, json);
        AssetDatabase.Refresh();

        Debug.Log($"[RhythmEditor] Server Export Complete → {path}");
        Debug.Log($"[RhythmEditor] Client Rhythm Mirror Updated → {clientMirrorPath}");
        ShowNotification(new GUIContent("서버/클라이언트 저장 완료! ✔"));
    }

    private void ExportToJson()
    {
        string path = EditorUtility.SaveFilePanel("Save Rhythm Data", "Assets/", stageData.StageId + "_Rhythm", "json");
        if (!string.IsNullOrEmpty(path))
        {
            string json = JsonUtility.ToJson(stageData, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"Rhythm Data Saved to: {path}");
            ShowNotification(new GUIContent("JSON 내보내기 대성공!"));
        }
    }

    private void ImportFromJson()
    {
        string path = EditorUtility.OpenFilePanel("Load Rhythm Data", "Assets/", "json");
        if (!string.IsNullOrEmpty(path))
        {
            string json = File.ReadAllText(path);
            stageData = JsonUtility.FromJson<RhythmStageData>(json);
            Debug.Log($"Rhythm Data Loaded from: {path}");
            ShowNotification(new GUIContent("JSON 불러오기 성공!"));
        }
    }

    // ──────────────────────────────────────────────────────
    // 🔊 FMOD 에디터 사운드 미리듣기 헬퍼 함수군
    // ──────────────────────────────────────────────────────

    private void StopPreviewAudio()
    {
        FMODUnity.EditorUtils.StopAllPreviews();

        if (_previewSimulatorChannel.hasHandle())
        {
            _previewSimulatorChannel.stop();
            _previewSimulatorChannel.clearHandle();
        }

        if (_previewSimulatorSound.hasHandle())
        {
            _previewSimulatorSound.release();
            _previewSimulatorSound.clearHandle();
        }
    }

    private void PreviewNoteSound(RhythmBlock block, BassNote note)
    {
        if (note == null)
            return;

        if (FMODDrumSequencer.UsesBassMelodySimulator(note))
        {
            PreviewSimulatorSound(block, note);
            return;
        }

        PreviewSound(note.SoundKey, note.PitchOffset, note.VolumeMultiplier);
    }

    private void PreviewSimulatorSound(RhythmBlock block, BassNote note)
    {
        StopPreviewAudio();

        byte[] wavBytes = FMODDrumSequencer.RenderBassMelodySimulatorPreviewWav(block, note);
        if (wavBytes == null || wavBytes.Length == 0)
        {
            Debug.LogWarning($"[RhythmEditor] Simulator preview data not generated for Key: {note.SoundKey}");
            return;
        }

        FMOD.Studio.System studioSystem = FMODUnity.EditorUtils.System;
        studioSystem.getCoreSystem(out FMOD.System coreSystem);

        FMOD.CREATESOUNDEXINFO exInfo = new FMOD.CREATESOUNDEXINFO
        {
            cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
            length = (uint)wavBytes.Length
        };

        FMOD.MODE mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.CREATESAMPLE | FMOD.MODE._2D | FMOD.MODE.LOOP_OFF;
        FMOD.RESULT createResult = coreSystem.createSound(wavBytes, mode, ref exInfo, out _previewSimulatorSound);

        if (createResult != FMOD.RESULT.OK)
        {
            Debug.LogWarning($"[RhythmEditor] Simulator preview sound create failed: {createResult}");
            return;
        }

        FMOD.RESULT playResult = coreSystem.playSound(_previewSimulatorSound, default, false, out _previewSimulatorChannel);
        if (playResult != FMOD.RESULT.OK)
        {
            Debug.LogWarning($"[RhythmEditor] Simulator preview play failed: {playResult}");
            _previewSimulatorSound.release();
            _previewSimulatorSound.clearHandle();
            return;
        }

        float volume = note.VolumeMultiplier > 0 ? note.VolumeMultiplier : 1.0f;
        _previewSimulatorChannel.setVolume(volume);
        Debug.Log($"[RhythmEditor] Preview Simulator Sound: {note.SoundKey} / ToneKey: {FMODDrumSequencer.GetRuntimeToneKey(note)} (PitchOffset: {note.PitchOffset}, Volume: {volume:F2})");
    }

    private void PreviewSound(string soundKey, int pitchOffset = 0, float volume = 1.0f)
    {
        string eventPath = GetEditorFmodEventPath(soundKey);
        if (string.IsNullOrEmpty(eventPath))
        {
            Debug.LogWarning($"[RhythmEditor] FMOD Event Path not resolved for Key: {soundKey}");
            return;
        }

        // FMOD 에디터 뱅크 로드 보장
        if (!FMODUnity.EditorUtils.PreviewBanksLoaded)
        {
            FMODUnity.EditorUtils.LoadPreviewBanks();
        }

        FMODUnity.EditorEventRef eventRef = FMODUnity.EventManager.EventFromPath(eventPath);
        if (eventRef == null)
        {
            Debug.LogWarning($"[RhythmEditor] FMOD EventRef not found in Editor: {eventPath}");
            return;
        }

        // 이전 재생 프리뷰 정지
        StopPreviewAudio();

        // 파라미터 값 사전 구성 (PitchOffset)
        var paramValues = new Dictionary<string, float>();
        if (pitchOffset != 0)
            paramValues.Add("PitchOffset", (float)pitchOffset);

        float resolvedVolume = volume > 0 ? volume : 1.0f;
        FMODUnity.EditorUtils.PreviewEvent(eventRef, paramValues, resolvedVolume);
        Debug.Log($"[RhythmEditor] Preview Sound: {eventPath} (PitchOffset: {pitchOffset}, Volume: {resolvedVolume:F2})");
    }

    private string GetEditorFmodEventPath(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey)) return null;

        // 1:1 이름 매칭 검증
        if (soundKey.EndsWith("_Dagger", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.EndsWith("_Greatsword", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.EndsWith("_Bow", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.EndsWith("_Parry", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.EndsWith("_Staff", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/SFX/{soundKey}";
        }
        if (soundKey.StartsWith("Bass_", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/Bass/{soundKey}";
        }
        if (soundKey.StartsWith("Melody_", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/Melody/{soundKey}";
        }
        if (soundKey.Equals("Kick", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.Equals("HiHat", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.Equals("TestSmith", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/Drums/{soundKey}";
        }

        // 장르 매칭 fallback (현재 스테이지 ID 기준)
        string genre = "Synthwave";
        string id = stageData.StageId.ToLowerInvariant();
        if (id.Contains("lofi")) genre = "Lofi";
        else if (id.Contains("funk")) genre = "Funk";
        else if (id.Contains("ethereal") || id.Contains("orch")) genre = "Orchestral";
        else if (id.Contains("jazz")) genre = "Jazz";

        // 장르 접두어 매칭
        if (soundKey.StartsWith("SynthBass_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Synthwave";
        if (soundKey.StartsWith("LofiBass_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Lofi";
        if (soundKey.StartsWith("FunkBass_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("SlapBass_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Funk";
        if (soundKey.StartsWith("OrchBass_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Orchestral";
        if (soundKey.StartsWith("JazzBass_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Bass/Bass_Jazz";

        if (soundKey.StartsWith("Bass_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("BassKick", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/Bass/Bass_{genre}";
        }

        if (soundKey.StartsWith("SynthLead_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("SynthPluck_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Synthwave";

        if (soundKey.StartsWith("LofiFlute_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Rhodes_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("LofiPiano_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Lofi";

        if (soundKey.StartsWith("Clav_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Clavinet_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("FunkGuitar_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("FunkMute_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Funk";

        if (soundKey.StartsWith("OrchFlute_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Violin_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Harp_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Cello_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Flute_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Orchestral";

        if (soundKey.StartsWith("JazzSax_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Vibraphone_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("JazzGuitar_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Melody/Melody_Jazz";

        if (soundKey.StartsWith("Melody_", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Lead_", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"event:/Melody/Melody_{genre}";
        }

        if (soundKey.Equals("Kick", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.StartsWith("Timpani_", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Drums/Kick";
        if (soundKey.Equals("HiHat", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Drums/HiHat";
        if (soundKey.Equals("Smith", System.StringComparison.OrdinalIgnoreCase) ||
            soundKey.Equals("TestSmith", System.StringComparison.OrdinalIgnoreCase))
            return "event:/Drums/TestSmith";

        return null;
    }
}

using System;
using System.Collections.Generic;

namespace Shared.Data
{
    [Serializable]
    public class RhythmStageData
    {
        public string StageId = string.Empty;
        public int Bpm = 120;
        public int TimeSignatureNum = 4;
        public int TimeSignatureDenom = 4;
        public int TicksPerBeat = 480;
        
        public List<RhythmBlock> Blocks = new List<RhythmBlock>();
    }

    [Serializable]
    public class RhythmBlock
    {
        public string BlockId = string.Empty;
        public int LengthMeasures = 4;
        public string DefaultNextBlock = string.Empty;
        public int OverrideBpm = 0;
        
        // ─── 화성(Harmony) ───────────────────────────────────────────
        // 마디와 Tick 기준으로, 어떤 코드(화음)가 유지되는지 타임스탬프로 정의
        public List<ChordEvent> ChordEvents = new List<ChordEvent>();
        
        // ─── 베이스/리듬 패턴(Bass & Rhythm Pattern) ──────────────────
        // 사운드 파일(Key)을 Tick 단위로 직접 예약하는 드럼/베이스 시퀀서
        // 클라이언트는 이 목록을 받아 FMOD에서 해당 Tick에 소리를 예약 재생함
        public List<BassNote> BassPattern = new List<BassNote>();
    }

    // ─── 화성 마커 ────────────────────────────────────────────────────
    [Serializable]
    public class ChordEvent
    {
        public int MeasureIndex;    // 몇 번째 마디 (0-indexed)
        public int Tick;            // 마디 내 몇 Tick 위치 (0 ~ TicksPerBeat * TimeSignatureNum - 1)
        
        public string RootNote = "C";       // 기준음 (C, C#, D ...)
        public string ChordType = "Major";  // 코드 종류 (Major, Minor, ...)
        public int PitchOffset;             // 기준음(C=0)으로부터의 반음 거리 → 클라이언트 FMOD Pitch 파라미터에 직접 사용
    }

    // ─── 베이스/드럼 음표(시퀀서 단일 이벤트) ─────────────────────────
    [Serializable]
    public class BassNote
    {
        // 블록 안에서의 절대 위치 (MeasureIndex는 없고, 마디 통합 Tick으로 관리)
        // 예: 4/4박자, TicksPerBeat=480 이면 1마디=1920Tick, 2마디 첫 정박=1920
        public int MeasureIndex;    // 몇 번째 마디 (0-indexed)
        public int Tick;            // 마디 내 Tick 위치

        // FMOD 에서 재생할 사운드 이벤트의 Key (예: "Kick", "Snare", "HiHat", "BassHit_Low")
        // 이 문자열이 FMOD Bank의 Event 이름과 1:1 매핑됨
        public string SoundKey = "Kick";

        // SoundKey는 FMOD 이벤트 키로 유지하고, 세부 악보/음색 구분이 필요할 때만 사용.
        public string ToneKey = string.Empty;

        // FMOD PitchOffset 파라미터에 전달할 반음 오프셋. 피치 변주가 필요한 SoundKey에서 사용.
        public int PitchOffset = 0;

        // 0.0 ~ 1.0: 이 노트의 볼륨 배율 (강세 표현 / 약박 표현에 사용)
        public float VolumeMultiplier = 1.0f;
    }
}

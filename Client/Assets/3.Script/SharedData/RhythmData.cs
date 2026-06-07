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
        public List<ChordEvent> ChordEvents = new List<ChordEvent>();
        
        // ─── 베이스/리듬 패턴(Bass & Rhythm Pattern) ──────────────────
        public List<BassNote> BassPattern = new List<BassNote>();
    }

    [Serializable]
    public class ChordEvent
    {
        public int MeasureIndex;
        public int Tick;
        public string RootNote = "C";
        public string ChordType = "Major";
        public int PitchOffset;
    }

    [Serializable]
    public class BassNote
    {
        public int MeasureIndex;
        public int Tick;
        // FMOD Bank Event 이름과 1:1 매핑 (예: "Kick", "Snare", "BassHit_Low")
        public string SoundKey = "Kick";
        // FMOD PitchOffset 파라미터에 전달할 반음 오프셋. 피치 변주가 필요한 SoundKey에서 사용.
        public int PitchOffset = 0;
        // 0.0 ~ 1.0: 볼륨 배율 (강세/약박 표현용)
        public float VolumeMultiplier = 1.0f;
    }
}

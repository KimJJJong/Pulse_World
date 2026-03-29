using Shared.Data;
using System;
using System.Linq;

namespace GameServer.InGame.System.Rhythm
{
    /// <summary>
    /// 동적 오디오 악보 데이터(JSON)를 바탕으로 현재의 화음(Chord)과 음악 진행 상태를 추적하는 매니저
    /// </summary>
    public class DynamicRhythmManager
    {
        private RhythmStageData _stageData;
        
        // 상태 정보
        public RhythmBlock CurrentBlock { get; private set; }
        private RhythmBlock _nextBlockedQueued;
        
        private long _blockStartBeatIndex = 0; // 해당 블록이 시작된 절대 비트 인덱스 (서버 동기화 기준)
        private ChordEvent _lastLoggedChordEvent; // 진행률 로깅을 위한 추적변수
        private long _lastLoggedHeartbeatMeasure = -1; // 마디별 예비 로그 추적변수

        public DynamicRhythmManager(RhythmStageData stageData)
        {
            _stageData = stageData;
            // 최초 시작 시 첫 번째 블록으로 세팅
            if (_stageData != null && _stageData.Blocks.Count > 0)
            {
                CurrentBlock = _stageData.Blocks[0];
            }
        }

        /// <summary>
        /// 보스 페이즈 전환 등 이벤트 발생 시, 다음 마디(Measure) 시작점에 갈아탈 블록을 미리 예약(Queueing)합니다.
        /// 즉각 바뀌지 않기 때문에 불협화음을 완벽히 방지합니다 (Quantized Transition).
        /// </summary>
        public void QueueNextBlock(string blockId)
        {
            if (_stageData == null) return;
            
            var target = _stageData.Blocks.FirstOrDefault(b => b.BlockId == blockId);
            if (target != null)
            {
                _nextBlockedQueued = target;
            }
        }

        /// <summary>
        /// 서버의 메인 Update 혹은 OnBeat 이벤트 타이밍에 호출합니다.
        /// 블록의 마디(Measure) 단위 루프가 끝났는지 감시하고 큐에 예약된 블록으로 갈아끼웁니다.
        /// </summary>
        public void UpdateMusicState(long currentBeatIndex)
        {
            if (CurrentBlock == null) return;

            // 현재 블록에 진입한 이후 몇 비트가 지났는가?
            long beatInBlock = currentBeatIndex - _blockStartBeatIndex;
            
            if (beatInBlock < 0) return; // 아직 시작 전

            int beatsPerMeasure = _stageData.TimeSignatureNum;
            int totalBeatsInBlock = CurrentBlock.LengthMeasures * beatsPerMeasure;

            // 로깅용 현재 진행 마커 추적
            TrackAndLogCurrentChord(beatInBlock, currentBeatIndex);

            // 이 블록의 전체 길이가 다 끝났다면 (다음 루프 또는 다음 블록으로 넘어갈 타이밍)
            if (beatInBlock >= totalBeatsInBlock)
            {
                // 예약된 트랜지션 블록이 있으면 갈아타고, 없으면 DefaultNext 지정된 블록으로 무한 루프
                if (_nextBlockedQueued != null)
                {
                    CurrentBlock = _nextBlockedQueued;
                    _nextBlockedQueued = null;
                }
                else
                {
                    CurrentBlock = _stageData.Blocks.FirstOrDefault(b => b.BlockId == CurrentBlock.DefaultNextBlock) ?? CurrentBlock;
                }

                // 새로운 블록이 시작되는 기준점 갱신 (지연 등을 보정하기 위해 % 연산 적용)
                _blockStartBeatIndex = currentBeatIndex - (beatInBlock % totalBeatsInBlock); 
                _lastLoggedChordEvent = null; // 새 루프/블록이므로 추적 초기화
                
                Console.WriteLine($"[Rhythm Tracker] 🎵 Block Triggered: {CurrentBlock.BlockId} | Starting Measure Loop.");
            }
        }

        private void TrackAndLogCurrentChord(long beatInBlock, long currentBeatIndex)
        {
            if (CurrentBlock == null) return;
            
            long measureIndex = beatInBlock / _stageData.TimeSignatureNum;
            long beatInMeasure = beatInBlock % _stageData.TimeSignatureNum;
            long targetTick = beatInMeasure * _stageData.TicksPerBeat;
            
            // 예비 코드: 매 마디 첫 박(Beat=0)마다 현재 진행 상황을 쏘아주어 서버가 멈춰있지 않은지 검증
            if (beatInMeasure == 0 && measureIndex != _lastLoggedHeartbeatMeasure)
            {
                _lastLoggedHeartbeatMeasure = measureIndex;
                Console.WriteLine($"[Rhythm Tracker] ⏳ Heartbeat ... Measure: {measureIndex + 1} / Block: {CurrentBlock.BlockId}");
            }
            
            var activeChord = CurrentBlock.ChordEvents
                .Where(c => c.MeasureIndex < measureIndex || (c.MeasureIndex == measureIndex && c.Tick <= targetTick))
                .OrderByDescending(c => c.MeasureIndex)
                .ThenByDescending(c => c.Tick)
                .FirstOrDefault();

            if (activeChord != null && activeChord != _lastLoggedChordEvent)
            {
                _lastLoggedChordEvent = activeChord;
                Console.WriteLine($"[Rhythm Tracker] 🎹 Measure {measureIndex+1}, Beat {beatInMeasure+1} -> Chord changes to: {activeChord.RootNote} {activeChord.ChordType} (Pitch={activeChord.PitchOffset})");
            }
        }

        /// <summary>
        /// 유저의 Hit 입력이 판정(Judge)되었을 때, 타격 시점의 노래 분위기(Chord)를 찾아 Pitch 오프셋을 반환합니다.
        /// </summary>
        public int GetPitchOffsetAtBeat(long targetBeatIndex)
        {
            if (CurrentBlock == null || CurrentBlock.ChordEvents.Count == 0) 
            {
                return 0; // 악보 데이터가 안 찍혀있으면 무난한 기준음(+0) 반환
            }

            long beatInBlock = targetBeatIndex - _blockStartBeatIndex;
            if (beatInBlock < 0) beatInBlock = 0;
            
            int beatsPerMeasure = _stageData.TimeSignatureNum;
            int totalBeatsInBlock = CurrentBlock.LengthMeasures * beatsPerMeasure;
            
            // 모듈러 연산으로 블록 내의 상대적 위치를 계산 (반복 루프 대응)
            beatInBlock = beatInBlock % totalBeatsInBlock;

            long measureIndex = beatInBlock / beatsPerMeasure;
            long beatInMeasure = beatInBlock % beatsPerMeasure;
            
            // Tick으로 환산
            long targetTick = beatInMeasure * _stageData.TicksPerBeat;
            
            // 타임라인 상에서 해당 타격 시점보다 '가장 최근 과거'에 놓인 마커(ChordEvent)를 찾기
            var activeChord = CurrentBlock.ChordEvents
                .Where(c => c.MeasureIndex < measureIndex || (c.MeasureIndex == measureIndex && c.Tick <= targetTick))
                .OrderByDescending(c => c.MeasureIndex)
                .ThenByDescending(c => c.Tick)
                .FirstOrDefault();

            if (activeChord != null)
            {
                return activeChord.PitchOffset;
            }
            
            // 만약 해당 블록의 첫 마커보다도 더 앞선 타이밍에 때렸다면, 이전 싸이클의 마지막 화음 유지
            var lastFallback = CurrentBlock.ChordEvents.LastOrDefault();
            return lastFallback != null ? lastFallback.PitchOffset : 0;
        }
    }
}

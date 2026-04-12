using System;
using System.Collections.Generic;
using System.Text.Json.Serialization; // [NEW] For Server Serialization

#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

// 서버/클라이언트 공유 코드 (파일 복사본)
// 네임스페이스는 프로젝트 상황에 맞춰 조정 (예: GameShared.Data)

namespace GameShared.Data
{
    [Serializable]
    public class NewSkillDef
    {
        public string SkillId = "";
        public int TotalDurationTicks;  // 전체 스킬 길이 (Tick 단위, Input Lock 등에 사용)
        public List<SkillTrack> Tracks = new List<SkillTrack>();
    }

    [Serializable]
    public class SkillTrack
    {
        public string TrackName = "Base Track";
        public List<SkillEvent> Events = new List<SkillEvent>();
    }

    [Serializable]
    public class SkillEvent
    {
        public int TriggerTick;   // 시작 틱 (0부터 시작)
        public int DurationTicks; // 지속 틱
        
        // 다형성 처리를 위해 JSON 변환 시 TypeNameHandling 필요 (또는 별도 Type 필드 사용)
        // 여기서는 구조체 대신 클래스 기반의 상속 구조 사용
#if UNITY_5_3_OR_NEWER
        [SerializeReference]
#endif
        public BaseAction Action;
    }

    // -------------------------------------------------------------------------
    // Actions (Polymorphic)
    // -------------------------------------------------------------------------

    // JSON 시리얼라이라이저가 타입 정보를 알 수 있게 처리 필요 (System.Text.Json용 Attribute 등)
    // JSON 시리얼라이라이저가 타입 정보를 알 수 있게 처리 필요 (System.Text.Json용 Attribute 등)
    [Serializable]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(WarningAction), (int)SkillActionType.Warning)]
    [JsonDerivedType(typeof(DamageAction), (int)SkillActionType.Damage)]
    [JsonDerivedType(typeof(MoveAction), (int)SkillActionType.Move)]
    [JsonDerivedType(typeof(InputLockAction), (int)SkillActionType.InputLock)]
    [JsonDerivedType(typeof(WaitAction), (int)SkillActionType.Wait)]
    [JsonDerivedType(typeof(SoundAction), (int)SkillActionType.Sound)]
    public abstract class BaseAction
    {
        public abstract SkillActionType GetSkillActionType();
        public SkillActionType Type => GetSkillActionType();
    }

    public enum SkillActionType
    { 
        None = 0, 
        Warning, 
        Damage, 
        Move, 
        InputLock,
        Wait,
        Sound   // 특정 Tick에 FMOD 사운드 이벤트 재생
    }

    // 0. Wait (대기)
    [Serializable]
    public class WaitAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Wait;
    }

    // 1. Warning (전조)
    [Serializable]
    public class WarningAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Warning;

#if UNITY_5_3_OR_NEWER
        [SerializeReference]
#endif
        public IShapeDef Shape; // 범위 정의
        public List<WarningColorStep> ColorSteps = new List<WarningColorStep>();
    }

    [Serializable]
    public class WarningColorStep
    {
        public int DurationTicks;
        public string ColorHex = "#FF0000"; // 또는 Enum
    }

    // 2. Damage (타격)
    [Serializable]
    public class DamageAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Damage;

#if UNITY_5_3_OR_NEWER
        [SerializeReference]
#endif
        public IShapeDef Shape;
        public int Amount;
        public bool HitPlayers = true;
        public bool HitMonsters = false;
        
        // Status Effects (CC)
        public int StunDurationTicks = 0;
        public int KnockbackDistance = 0;
        
        // 타격 시점의 타겟팅 (Snapshot vs Realtime)
        public bool RecalculateTargets = false; 
    }

    // 3. Move (이동)
    [Serializable]
    public class MoveAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Move;

        public MoveType MoveType = MoveType.Dash;
        public int Distance;
        public int DirectionX; // +1, -1, 0 (상대적)
        public int DirectionY; // +1, -1, 0 (상대적)
        public bool StopOnObstacle = true;
    }

    public enum MoveType { Walk, Dash, Blink }

    // 4. InputLock (조작 잠금 - 플레이어용 / 몬스터는 AI 정지)
    [Serializable]
    public class InputLockAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.InputLock;
    }

    // 5. Sound (FMOD 사운드 이벤트 재생 - 클라이언트 전용, 서버에서는 Skip)
    [Serializable]
    public class SoundAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Sound;

        /// <summary>FMOD Studio의 Event Path 또는 식별 이름. 예: "event:/SFX/Attack/Sword"</summary>
        public string FmodEventPath = "";

        /// <summary>0.0 ~ 1.0. 기본값 1.0 (FMOD Master Volume 기준).</summary>
        public float Volume = 1.0f;

        /// <summary>true면 내 캐릭터 기준 사운드, false면 상대방 기준. PlayAttackSound(_isMine)과 동일한 역할.</summary>
        public bool UseOwnerPerspective = true;
    }


    // -------------------------------------------------------------------------
    // Shapes (Polymorphic)
    // -------------------------------------------------------------------------
    
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "ShapeType")]
    [JsonDerivedType(typeof(RectShape), (int)ShapeType.Rect)]
    [JsonDerivedType(typeof(DiamondShape), (int)ShapeType.Diamond)]
    [JsonDerivedType(typeof(CustomCellsShape), (int)ShapeType.CustomCells)]
    public abstract class IShapeDef 
    {
        public abstract ShapeType GetShapeType();
        public ShapeType ShapeType => GetShapeType();

        public int CasterSize = 1;         // 시전자 크기 (1x1, 3x3 등)
        public bool RotateWithCaster = true; // 시전자 방향에 맞춰 회전 여부
    }

    public enum ShapeType
    {
        None = 0,
        Rect,
        Diamond,
        CustomCells
    }

    [Serializable]
    public class RectShape : IShapeDef
    {
        public override ShapeType GetShapeType() => ShapeType.Rect;
        public int Width;
        public int Height;
    }

    [Serializable]
    public class DiamondShape : IShapeDef
    {
        public override ShapeType GetShapeType() => ShapeType.Diamond;
        public int Radius;
    }

    [Serializable]
    public class CustomCellsShape : IShapeDef
    {
        public override ShapeType GetShapeType() => ShapeType.CustomCells;
        // (0,0) 기준 상대 좌표 목록
        public List<GridPoint> Cells = new List<GridPoint>();
    }

    [Serializable]
    public struct GridPoint 
    { 
        public int X; 
        public int Y;
        public GridPoint(int x, int y) { X = x; Y = y; }
    }
}

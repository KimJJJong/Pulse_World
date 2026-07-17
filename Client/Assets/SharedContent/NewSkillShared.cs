using System;
using System.Collections.Generic;

#if UNITY_5_3_OR_NEWER
using UnityEngine;
#else
using System.Text.Json.Serialization;
#endif

namespace GameShared.Data
{
    [Serializable]
    public class NewSkillDef
    {
        public string SkillId = "";
        public int TotalDurationTicks;
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
        public int TriggerTick;
        public int DurationTicks;

#if UNITY_5_3_OR_NEWER
        [SerializeReference]
#endif
        public BaseAction Action;
    }

#if !UNITY_5_3_OR_NEWER
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(WarningAction), (int)SkillActionType.Warning)]
    [JsonDerivedType(typeof(DamageAction), (int)SkillActionType.Damage)]
    [JsonDerivedType(typeof(MoveAction), (int)SkillActionType.Move)]
    [JsonDerivedType(typeof(InputLockAction), (int)SkillActionType.InputLock)]
    [JsonDerivedType(typeof(WaitAction), (int)SkillActionType.Wait)]
    [JsonDerivedType(typeof(SoundAction), (int)SkillActionType.Sound)]
    [JsonDerivedType(typeof(SummonDecoyAction), (int)SkillActionType.SummonDecoy)]
#endif
    [Serializable]
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
        Sound,
        SummonDecoy
    }

    [Serializable]
    public class WaitAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Wait;
    }

    [Serializable]
    public class WarningAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Warning;

#if UNITY_5_3_OR_NEWER
        [SerializeReference]
#endif
        public IShapeDef Shape;
        public List<WarningColorStep> ColorSteps = new List<WarningColorStep>();
    }

    [Serializable]
    public class WarningColorStep
    {
        public int DurationTicks;
        public string ColorHex = "#FF0000";
    }

    [Serializable]
    public class DamageAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Damage;

#if UNITY_5_3_OR_NEWER
        [SerializeReference]
#endif
        public IShapeDef Shape;
        public int Amount;
        public bool HitPlayers = false;
        public bool HitMonsters = true;
        public int StunDurationTicks = 0;
        public int KnockbackDistance = 0;
        public bool RecalculateTargets = false;
    }

    [Serializable]
    public class MoveAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Move;

        public MoveType MoveType = MoveType.Dash;
        public int Distance;
        public int DirectionX;
        public int DirectionY;
        public bool StopOnObstacle = true;
    }

    public enum MoveType { Walk, Dash, Blink }

    [Serializable]
    public class InputLockAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.InputLock;
    }

    [Serializable]
    public class SoundAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.Sound;

        public string FmodEventPath = "";
        public float Volume = 1.0f;
        public bool UseOwnerPerspective = true;
    }

    [Serializable]
    public class SummonDecoyAction : BaseAction
    {
        public override SkillActionType GetSkillActionType() => SkillActionType.SummonDecoy;

        public int AppearanceId = 12;
        public int Hp = 36;
        public int DurationTicks = 1440;
        public int OffsetX = 0;
        public int OffsetY = -1;
        public bool RotateWithCaster = true;
    }

#if !UNITY_5_3_OR_NEWER
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "ShapeType")]
    [JsonDerivedType(typeof(RectShape), (int)ShapeType.Rect)]
    [JsonDerivedType(typeof(DiamondShape), (int)ShapeType.Diamond)]
    [JsonDerivedType(typeof(CustomCellsShape), (int)ShapeType.CustomCells)]
#endif
    public abstract class IShapeDef
    {
        public abstract ShapeType GetShapeType();
        public ShapeType ShapeType => GetShapeType();

        public int CasterSize = 1;
        public bool RotateWithCaster = true;
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

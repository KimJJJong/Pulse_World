using System;
using System.Collections;
using GameServer.InGame.Director.Data;
using UnityEngine;

public sealed partial class P2PContentDirector
{
    private const int EliteGateAppearanceId = 504;
    private const int EliteGateSceneGroupId = 2190;
    private const int EliteGateFinaleCompletedStateId = 105;
    private const int FinaleCenterCrystalX = 70;
    private const int FinaleCenterCrystalY = 39;
    private const int FinaleLinkHideMs = 260;
    private const int FinaleCrystalSinkMs = 850;
    private const int FinaleTowerSinkMs = 1250;
    private const float FinaleRedPulseDelaySeconds = 1.05f;
    private const float FinaleGateSpawnDelaySeconds = 1.68f;
    private static readonly int[] FinaleTowerGroups = { 31, 32, 33, 34 };

    private Coroutine _eliteGateFinaleCoroutine;
    private bool _spawningEliteGateAfterFinale;

    private bool TryStartEliteGateFinale(SpawnObjectData data)
    {
        if (_spawningEliteGateAfterFinale || !IsEliteGateFinaleSpawn(data))
            return false;

        if (!isActiveAndEnabled)
            return false;

        if (_eliteGateFinaleCoroutine != null)
            return true;

        _eliteGateFinaleCoroutine = StartCoroutine(CoPlayEliteGateFinale(CloneSpawnObjectData(data)));
        return true;
    }

    private void CancelEliteGateFinale()
    {
        if (_eliteGateFinaleCoroutine != null)
        {
            StopCoroutine(_eliteGateFinaleCoroutine);
            _eliteGateFinaleCoroutine = null;
        }

        _spawningEliteGateAfterFinale = false;
    }

    private IEnumerator CoPlayEliteGateFinale(SpawnObjectData gateData)
    {
        SendSceneObjectSignal("RunicTowerLink", 0, false, FinaleLinkHideMs, 0);
        SendSceneObjectSignal("RunicTowerCrystal", 0, false, FinaleCrystalSinkMs, 90);
        SendSceneObjectSignal("RunicTower", 0, false, FinaleTowerSinkMs, 180);

        yield return new WaitForSeconds(FinaleRedPulseDelaySeconds);
        PlayStageVfx(new StageVfxData
        {
            VfxKey = StageVfxKeys.CrystalPulseRed,
            X = FinaleCenterCrystalX,
            Y = 0,
            Z = FinaleCenterCrystalY,
            DurationMs = 1600
        });

        float remainingDelay = Mathf.Max(0f, FinaleGateSpawnDelaySeconds - FinaleRedPulseDelaySeconds);
        if (remainingDelay > 0f)
            yield return new WaitForSeconds(remainingDelay);

        RemoveFinaleTowerGroups();
        TriggerFinaleCameraShake();

        _spawningEliteGateAfterFinale = true;
        try
        {
            SpawnObjectNow(gateData);
            SetObjectState(EliteGateFinaleCompletedStateId, 1);
        }
        finally
        {
            _spawningEliteGateAfterFinale = false;
            _eliteGateFinaleCoroutine = null;
        }
    }

    private static bool IsEliteGateFinaleSpawn(SpawnObjectData data)
    {
        return data != null
               && data.EntityId == EliteGateAppearanceId
               && data.GroupId == EliteGateSceneGroupId;
    }

    private static SpawnObjectData CloneSpawnObjectData(SpawnObjectData data)
    {
        return new SpawnObjectData
        {
            EntityId = data.EntityId,
            EntityType = data.EntityType,
            X = data.X,
            Y = data.Y,
            Z = data.Z,
            GroupId = data.GroupId,
            SizeX = Math.Max(1, data.SizeX),
            SizeY = Math.Max(1, data.SizeY),
            Rotation = data.Rotation,
            Pattern = data.Pattern ?? string.Empty
        };
    }

    private void SendSceneObjectSignal(string targetKey, int groupId, bool visible, int durationMs, int delayMs)
    {
        SetSceneObjectActive(new StageSceneObjectData
        {
            TargetKey = targetKey ?? string.Empty,
            GroupId = groupId,
            Visible = visible,
            DurationMs = Mathf.Max(0, durationMs),
            DelayMs = Mathf.Max(0, delayMs)
        });
    }

    private void RemoveFinaleTowerGroups()
    {
        foreach (int groupId in FinaleTowerGroups)
            RemoveEntityGroupNow(groupId);
    }

    private static void TriggerFinaleCameraShake()
    {
        CameraFollow follow = CameraBinder.Instance != null ? CameraBinder.Instance.Follow : null;
        if (follow == null && Camera.main != null)
            follow = Camera.main.GetComponent<CameraFollow>();
        if (follow == null)
            follow = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();

        follow?.Shake(0.65f, 0.075f, 22f);
    }
}
